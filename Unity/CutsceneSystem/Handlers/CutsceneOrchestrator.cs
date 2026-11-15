using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.DatabaseSystem;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.LogicChainsSystem.Model;
using Vortex.Unity.CutsceneSystem.Bus;
using Vortex.Unity.CutsceneSystem.Controllers;
using Vortex.Unity.CutsceneSystem.Models;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.DatabaseSystem.Presets;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.Misc;
using Vortex.Unity.UI.StateSwitcher;
using ChainsBus = Vortex.Core.LogicChainsSystem.Bus.LogicChains;

namespace Vortex.Unity.CutsceneSystem.Handlers
{
    /// <summary>
    /// Оркестратор катсцен на сцене: держит единственный слот <see cref="CutsceneBus"/> и ведёт показ целиком —
    /// принимает обращения, загружает контент, запускает цепочку, двигает фазы, закрывает показ.
    ///
    /// Обращение шины оркестратор не исполняет в момент прихода, а только запоминает: «просили открыть»,
    /// «просили дальше», «просили пропустить». Исполняет он их сам, на своём <c>LateUpdate</c>. Поэтому
    /// обращения никогда не исполняются вложенно друг в друга, а повторные просьбы одного кадра сливаются
    /// в одну.
    ///
    /// Контент — префаб за <see cref="AssetHandle{T}"/>, выбирается по ключу катсцены (запись <c>LogicChain</c>).
    /// Экземпляру передаются управляющие данные показа через <c>DataStorage</c> на его корне
    /// </summary>
    public class CutsceneOrchestrator : MonoBehaviour
    {
        [Serializable, ClassLabel("$Label")]
        private class Entry
        {
            [SerializeField, DbRecord(typeof(LogicChain), RecordTypes.MultiInstance)]
            private string chain;

            [SerializeReference] private AssetHandle<GameObject> prefab;

            public string Chain => chain;
            public AssetHandle<GameObject> Prefab => prefab;

            /// <summary>
            /// Подпись элемента в инспекторе — имя цепочки. Берётся из её пресета (как в выпадашке
            /// <see cref="DbRecordAttribute"/>): <c>GetNewRecord</c> на каждую перерисовку создавал бы копию цепочки
            /// </summary>
            private string Label()
            {
#if UNITY_EDITOR
                if (string.IsNullOrEmpty(chain))
                    return "(null)";
                return (Database.GetDriver() as IDriverEditor)?.GetPresetForRecord(chain) is IRecordPreset preset
                    ? preset.Name
                    : "(null)";
#else
                return string.Empty;
#endif
            }
        }

        [SerializeField, Tooltip("Катсцены сцены: цепочка-описание и префаб её контента.")]
        private Entry[] cutscenes = new Entry[0];

        [SerializeField, StateSwitcher(typeof(CutsceneStageState))]
        private UIStateSwitcher switcher;

        [SerializeField, Tooltip("Куда ставится контент. Не задано — под сам оркестратор.")]
        private Transform target;

        [SerializeField, Min(0f), Tooltip(
             "Защита от дребезга: сколько секунд после входа в фазу команда «дальше» игнорируется.")]
        private float nextDelay = 0.2f;

        private bool _registered;
        private CutsceneStageState _state;

        // ── Запросы: запоминаются в момент обращения, исполняются на LateUpdate ──

        private bool _openRequested;
        private string _openKey;

        /// <summary>
        /// Просили «дальше». Держится до исполнения: повторные просьбы той же фазы ничего не добавляют
        /// </summary>
        private bool _nextRequested;

        private bool _skipRequested;

        /// <summary>
        /// Открытия, которые не состоятся (вытеснены более поздним, отменены, оркестратор выключен).
        /// Каждому положен свой «показ завершён»
        /// </summary>
        private readonly List<string> _refused = new();

        // ── Показ ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Ключ идущей катсцены — с начала загрузки и до закрытия. <c>null</c> — показа нет
        /// </summary>
        private string _key;

        private Entry _entry;
        private LogicChain _chain;

        /// <summary>
        /// Идентификатор запущенной цепочки. <c>null</c> — цепочка не запущена (контент ещё грузится)
        /// </summary>
        private string _chainGuid;

        private GameObject _instance;
        private CancellationTokenSource _loading;

        /// <summary>
        /// Загрузка контента закончилась, её результат ждёт исполнения
        /// </summary>
        private bool _loadDone;

        private GameObject _loadedPrefab;
        private Exception _loadError;

        /// <summary>
        /// Номер фазы, набранный звеньями. Контенту уходит, когда цепочка вернула управление
        /// </summary>
        private int _phase;

        private float _phaseStartedAt = float.NegativeInfinity;

        /// <summary>
        /// Завершающее звено отработало: показ закрывается, как только цепочка вернёт управление
        /// </summary>
        private bool _completed;

        /// <summary>
        /// Цепочка исполняется прямо сейчас — этот оркестратор её и двигает
        /// </summary>
        private bool _chainRunning;

        /// <summary>
        /// Первая фаза запущена: контент создан, первый этап цепочки отработал. До этого «дальше»
        /// не принимается
        /// </summary>
        private bool _started;

        private void OnEnable()
        {
            _registered = CutsceneBus.Register(this);
            if (!_registered)
                return;

            CutsceneBus.OnOpenRequested += RequestOpen;
            CutsceneBus.OnNextRequested += RequestNext;
            CutsceneBus.OnAutoNextRequested += RequestAutoNext;
            CutsceneBus.OnSkipRequested += RequestSkip;
            SetState(_state);
        }

        private void OnDisable()
        {
            if (!_registered)
                return;

            _registered = false;
            CutsceneBus.OnOpenRequested -= RequestOpen;
            CutsceneBus.OnNextRequested -= RequestNext;
            CutsceneBus.OnAutoNextRequested -= RequestAutoNext;
            CutsceneBus.OnSkipRequested -= RequestSkip;

            //Слот освобождается вместе с отпиской, до закрытия показа: обращение, пришедшее из обработчика
            //«показ завершён», уже некому принять — шина должна видеть слот пустым и ответить отказом сама
            CutsceneBus.Unregister(this);

            try
            {
                //Показ не переживает оркестратор: оставшиеся этапы цепочки проходят до финала пролётом.
                //Если цепочка исполняется прямо сейчас (оркестратор выключило её звено), она дойдёт уже
                //в режиме пролёта, а показ закроет тот, кто её запустил, — когда она вернёт управление
                if (_chainRunning)
                    CutsceneBus.SetSkipping(true);
                else
                    Finish();
            }
            finally
            {
                //Запросы, которые исполнять уже некому
                if (_openRequested)
                    _refused.Add(_openKey);
                _openRequested = false;
                _openKey = null;
                _skipRequested = false;
                _nextRequested = false;
                FlushRefused();
            }
        }

        // ── Приём обращений ───────────────────────────────────────────────────

        private void RequestOpen(string key)
        {
            //Более позднее открытие вытесняет прежнее, ещё не исполненное
            if (_openRequested)
                _refused.Add(_openKey);

            _openRequested = true;
            _openKey = key;

            //«Дальше» относилось к катсцене, которую это открытие закроет
            _nextRequested = false;
        }

        private void RequestNext()
        {
            if (CanAdvance && Time.unscaledTime - _phaseStartedAt >= nextDelay)
                _nextRequested = true;
        }

        private void RequestAutoNext(int phase)
        {
            //Контент называет фазу, которую показывает: с ушедшей фазы сигнал не принимается
            if (CanAdvance && phase == _phase)
                _nextRequested = true;
        }

        private void RequestSkip()
        {
            //Пропуск относится и к открытию, которое ещё не исполнено
            if (_openRequested)
                _refused.Add(_openKey);

            _openRequested = false;
            _openKey = null;
            _nextRequested = false;
            _skipRequested = true;
        }

        /// <summary>
        /// «Дальше» есть к чему отнести: первая фаза запущена, и показ не собираются закрывать. Пока контент
        /// грузится и первый этап цепочки не отработал, любая просьба «дальше» игнорируется
        /// </summary>
        private bool CanAdvance =>
            _started && !_openRequested && !_skipRequested && !CutsceneBus.IsSkipping;

        // ── Исполнение ────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_registered)
                return;

            //«Дальше» исполняется последним и только то, что просили до этого прохода — и только для того
            //показа, для которого просили: если за проход открылась другая катсцена, просьба пропадает
            var next = _nextRequested;
            var show = _chainGuid;
            _nextRequested = false;

            if (_skipRequested || _openRequested)
            {
                _skipRequested = false;
                Step(Finish);
            }

            if (_refused.Count > 0)
                Step(FlushRefused);

            if (_openRequested)
                Step(StartShow);

            if (_loadDone)
                Step(CompleteLoad);

            if (next && show == _chainGuid)
                Step(Advance);
        }

        /// <summary>
        /// Сбой одного шага пишется в лог и остальные шаги не останавливает
        /// </summary>
        private static void Step(Action step)
        {
            try
            {
                step();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Начать открытие: найти запись, проверить цепочку, запустить загрузку контента. К этому моменту
        /// прежняя катсцена закрыта и её «показ завершён» разослан
        /// </summary>
        private void StartShow()
        {
            //Просьба могла прийти из обработчика события уже после шага закрытия — тогда идущая катсцена
            //закрывается здесь. Ключ берём после: обработчик её «показ завершён» может попросить другую
            Finish();

            var key = _openKey;
            _openRequested = false;
            _openKey = null;

            if (string.IsNullOrEmpty(key))
            {
                Refuse(key, "пустой ключ катсцены");
                return;
            }

            var entry = Find(key);
            if (entry == null)
            {
                Refuse(key, $"ключа '{key}' нет в индексе оркестратора");
                return;
            }

            if (entry.Prefab == null)
            {
                Refuse(key, $"у катсцены '{key}' не задан префаб контента");
                return;
            }

            LogicChain chain;
            string error;
            try
            {
                chain = Database.GetNewRecord<LogicChain>(key);
                if (chain == null)
                {
                    Refuse(key, $"цепочка '{key}' не найдена в базе");
                    return;
                }

                if (!CutsceneChainValidator.Validate(chain, out error))
                {
                    Refuse(key, $"цепочка «{chain.Name}» не годится для катсцены: {error}");
                    return;
                }
            }
            catch (Exception exception)
            {
                //Испорченная запись цепочки, сбой драйвера базы — тот же отказ, только с исключением
                Debug.LogException(exception, this);
                Refuse(key, $"сбой чтения цепочки '{key}'");
                return;
            }

            _key = key;
            _entry = entry;
            _chain = chain;
            _loading = new CancellationTokenSource();

            //Токен берём до смены состояния: свитчер на входе в Loading может исполнить чужой код,
            //а тот — выключить оркестратор и закрыть показ
            var token = _loading.Token;
            SetState(CutsceneStageState.Loading);
            if (token.IsCancellationRequested)
                return;

            //Катсцена открывается. Обработчик события тоже может выключить оркестратор — проверяем снова
            var failed = CutsceneBus.NotifyOpening(key);
            if (token.IsCancellationRequested)
                return;

            if (failed)
                Interrupt();

            Load(entry, token).Forget();
        }

        /// <summary>
        /// Загрузка контента. Сама ничего не запускает: результат откладывается до ближайшего
        /// <c>LateUpdate</c>
        /// </summary>
        private async UniTaskVoid Load(Entry entry, CancellationToken token)
        {
            GameObject prefab = null;
            Exception error = null;
            try
            {
                prefab = await entry.Prefab.LoadAsync(token);
            }
            catch (Exception exception)
            {
                error = exception;
            }

            //Загрузку отменила выгрузка: показ уже закрыт, а на сцене может идти другой. Результат — и успех,
            //и сбой — относится к закрытому показу и никому не нужен
            if (token.IsCancellationRequested)
                return;

            _loadedPrefab = prefab;
            _loadError = error;
            _loadDone = true;
        }

        /// <summary>
        /// Запустить показ по результату загрузки
        /// </summary>
        private void CompleteLoad()
        {
            var prefab = _loadedPrefab;
            var error = _loadError;
            _loadDone = false;
            _loadedPrefab = null;
            _loadError = null;

            var key = _key;
            var chain = _chain;
            if (error != null)
            {
                Fail($"контент «{chain.Name}» не загружен. {error}");
                return;
            }

            if (prefab == null)
            {
                Fail($"у катсцены «{chain.Name}» пустой префаб контента");
                return;
            }

            try
            {
                //Создание контента, привязка данных и смена состояния исполняют чужой код (Awake контента,
                //его хэндлеры, состояния свитчера). Если он выключил оркестратор, показ уже закрыт —
                //запуск дальше не идёт
                var instance = Instantiate(prefab, target != null ? target : transform);
                if (_key == null)
                {
                    //Выгрузка прошла, пока экземпляр ещё создавался, и убрать его не могла
                    instance.SetActive(false);
                    Destroy(instance);
                    return;
                }

                _instance = instance;
                var storage = instance.GetComponent<DataStorage>();
                if (storage == null)
                {
                    Fail($"на корне контента «{chain.Name}» нет DataStorage — управляющие данные передать некуда");
                    return;
                }

                storage.SetData(CutsceneBus.Control);
                if (_key == null)
                    return;

                SetState(CutsceneStageState.On);
                if (_key == null)
                    return;

                //В реестр цепочка попадает прямо перед запуском: сбой до этого места её там не оставит
                var guid = ChainsBus.AddChain(chain);
                _chainGuid = guid;
                _completed = false;
                _phase = 0;
                CutsceneBus.SetChain(guid);
                CutsceneBus.OnPhaseEntered += EnterPhase;
                CutsceneBus.OnCompleted += Complete;
                RunChain(guid);

                //Первая фаза запущена — с этого момента принимается «дальше»
                _started = true;
            }
            catch (Exception exception)
            {
                //Сбой запуска (исключение контента на привязке данных либо звена первого этапа): показ
                //открытым не оставляем
                Debug.LogException(exception, this);
                Fail($"сбой запуска показа «{chain.Name}»");
                return;
            }

            Settle();
            if (_key == null)
                return;

            //Показ состоялся: первая фаза уже у контента
            if (CutsceneBus.NotifyStarted(key) && _key != null)
                Interrupt();
        }

        private void RunChain(string guid)
        {
            _chainRunning = true;
            try
            {
                ChainsBus.RunChain(guid);
            }
            finally
            {
                _chainRunning = false;
            }
        }

        /// <summary>
        /// Подать цепочке сигнал «можно дальше»: она идёт, пока снова не встанет на условии
        /// </summary>
        private void Signal()
        {
            _chainRunning = true;
            try
            {
                CutsceneBus.SignalNext();
            }
            finally
            {
                _chainRunning = false;
            }
        }

        /// <summary>
        /// Исполнить просьбу «дальше»
        /// </summary>
        private void Advance()
        {
            if (_chainGuid == null)
                return;

            Signal();
            Settle();
        }

        /// <summary>
        /// Цепочка вернула управление: закрыть показ либо сообщить контенту набранную фазу
        /// </summary>
        private void Settle()
        {
            if (_key == null)
                return;

            if (_completed)
            {
                Close();
                return;
            }

            if (CutsceneBus.IsSkipping)
            {
                //Оркестратор выключили посреди исполнения цепочки, а до завершающего этапа она не дошла
                Debug.LogError(
                    $"[CutsceneOrchestrator] Пролёт катсцены '{_key}' не дошёл до завершающего этапа — показ закрыт принудительно.",
                    this);
                Close();
                return;
            }

            if (!CutsceneBus.HasWaiter)
            {
                //Цепочку остановило исключение в звене: такой показ уже не сдвинуть
                Debug.LogError(
                    $"[CutsceneOrchestrator] Цепочка катсцены '{_key}' остановилась и не ждёт сигнала «дальше» — показ закрыт.",
                    this);
                Close();
                return;
            }

            try
            {
                CutsceneBus.SetPhase(_phase);
            }
            catch (Exception exception)
            {
                //Исключение контента (рассогласование фаз и состояний в редакторе) показ не останавливает
                Debug.LogException(exception, this);
            }
        }

        /// <summary>
        /// Довести идущую катсцену до конца: загрузка отменяется, цепочка пролетает до завершающего этапа
        /// </summary>
        private void Finish()
        {
            if (_key == null)
                return;

            if (_chainGuid != null)
            {
                CutsceneBus.SetSkipping(true);
                Signal();
                if (_key == null)
                    return;

                if (!_completed)
                    Debug.LogError(
                        $"[CutsceneOrchestrator] Пролёт катсцены '{_key}' не дошёл до завершающего этапа — показ закрыт принудительно.",
                        this);
            }

            Close();
        }

        /// <summary>
        /// Единственное место, где показ закрывается: контент выгружается, данные показа сбрасываются,
        /// рассылается «показ завершён»
        /// </summary>
        private void Close()
        {
            var key = _key;
            if (key == null)
                return;

            //Состояние сбрасывается первым: показ считается закрытым до выгрузки, и повторное закрытие
            //(контент при выгрузке выключил оркестратор) ничего не сделает
            _key = null;
            _chainGuid = null;
            _completed = false;
            _started = false;
            _phase = 0;
            _phaseStartedAt = float.NegativeInfinity;
            _nextRequested = false;
            CutsceneBus.OnPhaseEntered -= EnterPhase;
            CutsceneBus.OnCompleted -= Complete;

            try
            {
                CutsceneBus.SetChain(null);
                CutsceneBus.SetSkipping(false);
                Unload();
            }
            finally
            {
                //Фаза сбрасывается и событие рассылается при любом исходе выгрузки. Контент уже выключен
                //и от данных отписан — сброс фазы до него не дойдёт
                try
                {
                    CutsceneBus.SetPhase(0);
                }
                finally
                {
                    CutsceneBus.NotifyFinished(key);
                }
            }
        }

        /// <summary>
        /// Выгрузить контент: снять загрузку, уничтожить экземпляр, отпустить префаб
        /// </summary>
        private void Unload()
        {
            //Поля сбрасываются первыми, состояние Off выставляется при любом исходе: сбой при освобождении
            //ресурсов не оставит показ наполовину закрытым
            var loading = _loading;
            var instance = _instance;
            var entry = _entry;
            _loading = null;
            _instance = null;
            _entry = null;
            _chain = null;
            _loadDone = false;
            _loadedPrefab = null;
            _loadError = null;

            try
            {
                if (instance != null)
                {
                    //Уничтожение отложено до конца кадра. Выключаем сразу: контент отписывается от управляющих
                    //данных и не получит фазу следующего показа
                    instance.SetActive(false);
                    Destroy(instance);
                }

                if (loading != null)
                {
                    loading.Cancel();
                    loading.Dispose();
                }

                entry?.Prefab.Release();
            }
            finally
            {
                SetState(CutsceneStageState.Off);
            }
        }

        // ── Сигналы звеньев цепочки ───────────────────────────────────────────

        /// <summary>
        /// Звено «Фаза»: номер фазы растёт. При пролёте не исполняется; звено чужой цепочки не исполняется
        /// </summary>
        private void EnterPhase(string chain)
        {
            if (chain != _chainGuid || CutsceneBus.IsSkipping)
                return;

            _phase++;
            _phaseStartedAt = Time.unscaledTime;
        }

        /// <summary>
        /// Звено «Завершение»: показ закроется, когда цепочка вернёт управление
        /// </summary>
        private void Complete(string chain)
        {
            if (chain == _chainGuid)
                _completed = true;
        }

        // ── Служебное ─────────────────────────────────────────────────────────

        /// <summary>
        /// Разослать «показ завершён» несостоявшимся открытиям. Только тем, что накопились к этому моменту:
        /// обработчик события может тут же попросить открытие и вытеснить им прежнюю просьбу — такой отказ
        /// ждёт следующего прохода, иначе рассылка не закончилась бы никогда
        /// </summary>
        private void FlushRefused()
        {
            var keys = _refused.ToArray();
            _refused.Clear();
            foreach (var key in keys)
                CutsceneBus.NotifyFinished(key);
        }

        private Entry Find(string key)
        {
            foreach (var entry in cutscenes)
                if (entry.Chain == key)
                    return entry;

            return null;
        }

        /// <summary>
        /// Отказ до начала загрузки
        /// </summary>
        private void Refuse(string key, string reason)
        {
            Debug.LogError($"[CutsceneOrchestrator] Катсцена не открыта: {reason}.", this);
            CutsceneBus.NotifyFinished(key);
        }

        /// <summary>
        /// Сбой после начала загрузки: показ закрывается, контент выгружается
        /// </summary>
        private void Fail(string reason)
        {
            Debug.LogError($"[CutsceneOrchestrator] Показ прерван: {reason}.", this);
            Close();
        }

        /// <summary>
        /// Подписчик события показа бросил исключение: идущая катсцена прерывается, как по пропуску
        /// </summary>
        private void Interrupt()
        {
            Debug.LogError(
                $"[CutsceneOrchestrator] Подписчик события показа бросил исключение — катсцена '{_key}' прервана.",
                this);
            RequestSkip();
        }

        private void SetState(CutsceneStageState state)
        {
            _state = state;
            if (switcher != null)
                switcher.Set(state);
        }
    }
}
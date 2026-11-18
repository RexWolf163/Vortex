using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.StateGraphSystem.Controllers;
using Vortex.Core.StateGraphSystem.Model;
using Vortex.Unity.CutsceneSystem.Bus;
using Vortex.Unity.CutsceneSystem.Models;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.StateGraphSystem.Views;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.CutsceneSystem.Handlers
{
    /// <summary>
    /// Оркестратор катсцен на сцене: держит единственный слот <see cref="CutsceneBus"/> и ведёт показ целиком —
    /// принимает обращения, загружает контент, ведёт граф, закрывает показ.
    ///
    /// Ход показа — граф состояний катсцены (<see cref="StateGraph"/>): его держит и показывает
    /// <see cref="StateGraphView"/> на корне контента. Оркестратор подаёт графу сигналы — «дальше» (<c>Next</c>)
    /// и выставление узла (<c>ForceNode</c>); вход графа в конечный узел (<c>OnFinished</c>) закрывает показ.
    ///
    /// Обращение шины оркестратор не исполняет в момент прихода, а только запоминает: «просили открыть»,
    /// «просили дальше», «просили узел», «просили пропустить». Завершение графа тоже лишь запоминается. Исполняет
    /// он их сам, на своём <c>LateUpdate</c>. Поэтому обращения никогда не исполняются вложенно друг в друга,
    /// а повторные просьбы одного кадра сливаются в одну.
    ///
    /// Контент — префаб за <see cref="AssetHandle{T}"/>, выбирается по ключу катсцены
    /// </summary>
    public class CutsceneOrchestrator : MonoBehaviour
    {
        [Serializable, ClassLabel("$Label")]
        private class Entry
        {
            [SerializeField, Tooltip("Ключ катсцены: его передают в CutsceneBus.Open и команде сценария.")]
            private string key;

            [SerializeReference] private AssetHandle<GameObject> prefab;

            public string Key => key;
            public AssetHandle<GameObject> Prefab => prefab;

            private string Label() => string.IsNullOrEmpty(key) ? "(null)" : key;
        }

        [SerializeField, Tooltip("Катсцены сцены: ключ и префаб её контента. Ключи не повторяются.")]
        private Entry[] cutscenes = new Entry[0];

        [SerializeField, StateSwitcher(typeof(CutsceneStageState))]
        private UIStateSwitcher switcher;

        [SerializeField, Tooltip("Куда ставится контент. Не задано — под сам оркестратор.")]
        private Transform target;

        private bool _registered;
        private CutsceneStageState _state;

        // ── Запросы: запоминаются в момент обращения, исполняются на LateUpdate ──

        private bool _openRequested;
        private string _openKey;

        /// <summary>
        /// Просили «дальше». Держится до исполнения: повторные просьбы того же узла ничего не добавляют
        /// </summary>
        private bool _nextRequested;

        /// <summary>
        /// Просили выставить узел (Id или имя); null — не просили. Пришла до старта показа — ждёт его и заменяет
        /// стартовый узел
        /// </summary>
        private string _nodeRequested;

        private bool _skipRequested;

        /// <summary>
        /// Граф катсцены завершился — показ закрывается на ближайшем проходе
        /// </summary>
        private bool _finishRequested;

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

        /// <summary>
        /// Номер показа: растёт на каждом открытии. По нему проход узнаёт, что просьба относится к тому же
        /// показу, для которого её приняли
        /// </summary>
        private int _show;

        private Entry _entry;
        private GameObject _instance;
        private CancellationTokenSource _loading;

        /// <summary>
        /// Загрузка контента закончилась, её результат ждёт исполнения
        /// </summary>
        private bool _loadDone;

        private GameObject _loadedPrefab;
        private Exception _loadError;

        /// <summary>
        /// Граф идущей катсцены — копия, которую держит <see cref="StateGraphView"/> контента
        /// </summary>
        private StateGraph _graph;

        /// <summary>
        /// Показ запущен: контент создан, граф показывает первый узел. До этого «дальше» не принимается
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
            CutsceneBus.OnNodeRequested += RequestNode;
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
            CutsceneBus.OnNodeRequested -= RequestNode;
            CutsceneBus.OnSkipRequested -= RequestSkip;

            //Слот освобождается вместе с отпиской, до закрытия показа: обращение, пришедшее из обработчика
            //«показ завершён», уже некому принять — шина должна видеть слот пустым и ответить отказом сама
            CutsceneBus.Unregister(this);

            try
            {
                //Показ не переживает оркестратор
                Close();
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
                _nodeRequested = null;
                FlushRefused();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var keys = new HashSet<string>();
            foreach (var entry in cutscenes)
                if (entry != null && !string.IsNullOrEmpty(entry.Key) && !keys.Add(entry.Key))
                    Debug.LogWarning(
                        $"[CutsceneOrchestrator] Ключ '{entry.Key}' повторяется в индексе — открывается первая запись.",
                        this);
        }
#endif

        // ── Приём обращений ───────────────────────────────────────────────────

        private void RequestOpen(string key)
        {
            //Более позднее открытие вытесняет прежнее, ещё не исполненное
            if (_openRequested)
                _refused.Add(_openKey);

            _openRequested = true;
            _openKey = key;

            //«Дальше» и узел относились к катсцене, которую это открытие закроет
            _nextRequested = false;
            _nodeRequested = null;
        }

        private void RequestNext()
        {
            if (CanAdvance)
                _nextRequested = true;
        }

        private void RequestAutoNext(string nodeId)
        {
            //Контент называет узел, который показывает: с ушедшего узла сигнал не принимается
            if (CanAdvance && _graph != null && nodeId == _graph.CurrentNode.Value)
                _nextRequested = true;
        }

        private void RequestNode(string node)
        {
            //Узел есть к чему отнести: катсцена открывается (тогда узел ждёт старта) либо идёт
            if (_skipRequested || (!_openRequested && _key == null))
                return;

            if (string.IsNullOrEmpty(node))
            {
                Debug.LogError("[CutsceneOrchestrator] Пустое имя узла. Команда пропущена.", this);
                return;
            }

            _nodeRequested = node;
        }

        private void RequestSkip()
        {
            //Пропуск относится и к открытию, которое ещё не исполнено
            if (_openRequested)
                _refused.Add(_openKey);

            _openRequested = false;
            _openKey = null;
            _nextRequested = false;
            _nodeRequested = null;
            _skipRequested = true;
        }

        /// <summary>
        /// «Дальше» есть к чему отнести: показ запущен, и его не собираются закрывать. Пока контент
        /// грузится, любая просьба «дальше» игнорируется
        /// </summary>
        private bool CanAdvance => _started && !_openRequested && !_skipRequested && !_finishRequested;

        // ── Сигналы графа ─────────────────────────────────────────────────────

        private void OnNodeChanged(string nodeId)
        {
            //Пустой узел — граф завершился: показ закроет OnGraphFinished
            if (string.IsNullOrEmpty(nodeId))
                return;

            CutsceneBus.PublishNode(_graph?.GetNode(nodeId)?.Name);
        }

        /// <summary>
        /// Граф вошёл в конечный узел. Закрытие — на проходе оркестратора, не вложенно в сигнал графа
        /// </summary>
        private void OnGraphFinished() => _finishRequested = true;

        // ── Исполнение ────────────────────────────────────────────────────────

        private void LateUpdate()
        {
            if (!_registered)
                return;

            //Просьбы о ходе графа исполняются последними и только те, что пришли до этого прохода, — и только
            //для того показа, для которого их приняли: если за проход открылась другая катсцена, они пропадают.
            //Узел, попрошенный для катсцены, которая ещё не начата, остаётся ждать её старта
            var show = _show;
            var next = _nextRequested;
            string node = null;
            _nextRequested = false;
            if (_started && !_openRequested)
            {
                node = _nodeRequested;
                _nodeRequested = null;
            }

            if (_skipRequested || _openRequested || _finishRequested)
            {
                _skipRequested = false;
                Step(Close);
            }

            if (_refused.Count > 0)
                Step(FlushRefused);

            if (_openRequested)
                Step(StartShow);

            if (_loadDone)
                Step(CompleteLoad);

            //Граф мог завершиться на старте (выставленный узел — конечный)
            if (_finishRequested)
                Step(Close);

            if (show != _show || !_started)
                return;

            //Выставление узла точнее, чем «дальше»: пришли оба — исполняется оно
            if (node != null)
                Step(() => GoToNode(node));
            else if (next)
                Step(Advance);

            //«Дальше» привело граф в конечный узел — показ закрывается в этом же проходе
            if (_finishRequested)
                Step(Close);
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
        /// Начать открытие: найти запись и запустить загрузку контента. К этому моменту прежняя катсцена
        /// закрыта и её «показ завершён» разослан
        /// </summary>
        private void StartShow()
        {
            //Просьба могла прийти из обработчика события уже после шага закрытия — тогда идущая катсцена
            //закрывается здесь. Ключ берём после: обработчик её «показ завершён» может попросить другую
            Close();

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

            _key = key;
            _show++;
            _entry = entry;
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
            if (error != null)
            {
                Fail($"контент '{key}' не загружен. {error}");
                return;
            }

            if (prefab == null)
            {
                Fail($"у катсцены '{key}' пустой префаб контента");
                return;
            }

            try
            {
                //Создание контента и смена состояния исполняют чужой код (Awake контента, его хэндлеры,
                //состояния свитчера). Если он выключил оркестратор, показ уже закрыт — запуск дальше не идёт.
                //View графа на своём OnEnable берёт копию графа из базы и запускает её со стартового узла
                var instance = Instantiate(prefab, target != null ? target : transform);
                if (_key == null)
                {
                    //Выгрузка прошла, пока экземпляр ещё создавался, и убрать его не могла
                    instance.SetActive(false);
                    Destroy(instance);
                    return;
                }

                _instance = instance;
                var view = instance.GetComponent<StateGraphView>();
                if (view == null)
                {
                    Fail($"на корне контента '{key}' нет {nameof(StateGraphView)} — графа катсцены нет");
                    return;
                }

                var graph = view.GetData<StateGraph>();
                if (graph == null)
                {
                    Fail($"граф контента '{key}' не получен — запись графа не найдена либо база не загружена");
                    return;
                }

                if (string.IsNullOrEmpty(graph.CurrentNode.Value))
                {
                    Fail($"граф контента '{key}' не запустился — ошибки графа в логе");
                    return;
                }

                _graph = graph;
                graph.CurrentNode.OnUpdate += OnNodeChanged;
                graph.OnFinished += OnGraphFinished;

                SetState(CutsceneStageState.On);
                if (_key == null)
                    return;

                //Стартовый узел граф уже показывает — сообщаем его
                OnNodeChanged(graph.CurrentNode.Value);

                //Узел, попрошенный во время загрузки, заменяет стартовый
                var requested = _nodeRequested;
                _nodeRequested = null;
                if (requested != null)
                    GoToNode(requested);
                if (_key == null)
                    return;

                //Показ запущен — с этого момента принимается «дальше»
                _started = true;
            }
            catch (Exception exception)
            {
                //Сбой запуска (исключение контента на создании либо на входе в узел): показ открытым не оставляем
                Debug.LogException(exception, this);
                Fail($"сбой запуска показа '{key}'");
                return;
            }

            //Показ состоялся: первый узел уже у контента
            if (CutsceneBus.NotifyStarted(key) && _key != null)
                Interrupt();
        }

        /// <summary>
        /// Исполнить просьбу «дальше»: сигнал <c>Next</c> графу. Вход в конечный узел закроет показ
        /// </summary>
        private void Advance() => _graph?.Next();

        /// <summary>
        /// Исполнить просьбу выставить узел. Текущий узел — ничего не происходит
        /// </summary>
        private void GoToNode(string node)
        {
            if (_graph == null)
                return;

            var current = _graph.GetCurrentNode();
            if (current != null && (current.Id == node || current.Name == node))
                return;

            _graph.ForceNode(node);
        }

        /// <summary>
        /// Единственное место, где показ закрывается: контент выгружается, данные показа сбрасываются,
        /// рассылается «показ завершён». Показа нет — ничего не делает
        /// </summary>
        private void Close()
        {
            _finishRequested = false;
            var key = _key;
            if (key == null)
                return;

            //Состояние сбрасывается первым: показ считается закрытым до выгрузки, и повторное закрытие
            //(контент при выгрузке выключил оркестратор) ничего не сделает
            _key = null;
            _started = false;
            _nextRequested = false;
            if (_graph != null)
            {
                _graph.CurrentNode.OnUpdate -= OnNodeChanged;
                _graph.OnFinished -= OnGraphFinished;
                _graph = null;
            }

            try
            {
                Unload();
            }
            finally
            {
                //Узел сбрасывается и событие рассылается при любом исходе выгрузки
                try
                {
                    CutsceneBus.PublishNode(string.Empty);
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
            _loadDone = false;
            _loadedPrefab = null;
            _loadError = null;

            try
            {
                if (instance != null)
                {
                    //Уничтожение отложено до конца кадра. Выключаем сразу: View графа гасит условия графа,
                    //а отложенные сигналы контента в следующий показ не попадут
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
                if (entry != null && entry.Key == key)
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

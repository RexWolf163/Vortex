using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.Quests.QuestsLogics;

namespace Vortex.Sdk.Quests
{
    /// <summary>
    /// Контроллер работает с квестами как с multyinstance записями
    /// </summary>
    public static partial class QuestController
    {
        /// <summary>
        /// Обновление в данных 
        /// </summary>
        public static event Action OnUpdateData;

        /// <summary>
        /// Событие запрошенного обновления проверки на случай если QuestConditions
        /// используются вне системы квестов 
        /// </summary>
        public static event Action OnCheckConditions;

        /// <summary>
        /// индекс квестов с запущенной логикой
        /// </summary>
        private static readonly Dictionary<QuestModel, UniTask> ActiveQuests = new();

        /// <summary>
        /// Индекс выполненных квестов с любым результатом (Failed или иным)
        /// </summary>
        private static readonly Dictionary<string, QuestModel> CompletedQuests = new();

        /// <summary>
        /// токен-ресурс прерывания
        /// </summary>
        private static CancellationTokenSource _cancelTokenSource = new();

        /// <summary>
        /// Пер-квестовые связанные CTS: отмена логики одного квеста (при блокировке) без остановки
        /// остальных. Каждый линкуется на глобальный <see cref="_cancelTokenSource"/>, поэтому массовый
        /// teardown (ResetController) гасит их каскадом.
        /// </summary>
        private static readonly Dictionary<QuestModel, CancellationTokenSource> ActiveCts = new();

        /// <summary>
        /// Список событий для подписки на контроль изменений 
        /// </summary>
        private static readonly Dictionary<IReactiveData, HashSet<object>> Listeners = new();

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            GameController.OnNewGame -= NewGameLogic;
            GameController.OnNewGame += NewGameLogic;
            GameController.OnLoadGame -= LoadGameLogic;
            GameController.OnLoadGame += LoadGameLogic;
            GameController.OnGameStateChanged -= CheckState;
            GameController.OnGameStateChanged += CheckState;
        }

        private static QuestModels _data;

        private static void NewGameLogic()
        {
            ResetController();

            //Инициализация квестов
            var list = _data.Index.Values.Where(q => q.State == QuestState.Unset);
            //безусловное переключение, но сам по себе этап может быть
            //полезен для отлова новых квестов на сейве, например
            foreach (var quest in list)
                quest.State = QuestState.Locked;

            CheckQuestStartConditions();
        }

        private static void LoadGameLogic()
        {
            ResetController();
            //RestorePresetData();

            // Гард на осиротевшие записи. Квест, удалённый из Database, но оставшийся в сейве, при
            // десериализации словаря попадает в индекс битой записью: ключа нет в целевом наборе (собранном
            // из пресетов), поэтому UploadDictionary создаёт QuestModel с нуля — восстановить определение из
            // пресета нечем, параметры == null Сам факт
            // null-записи корректен, но в обработку её пускать нельзя — иначе любое обращение падает по null.
            var orphans = _data.Index
                .Where(p => p.Value?.Name == null)
                .Select(p => p.Key)
                .ToList();
            foreach (var key in orphans)
                _data.Index.Remove(key);

            foreach (var quest in _data.Index.Values)
            {
                switch (quest.State)
                {
                    case QuestState.Unset:
                        //безусловное переключение, но сам по себе этап может быть
                        //полезен для отлова новых квестов на сейве, например
                        quest.State = QuestState.Locked;
                        break;
                    case QuestState.Locked:
                        break;
                    case QuestState.Ready:
                        // Ready латчится (условия старта одноразовы, назад — только через Interrupt), поэтому
                        // условия НЕ перечитываем. Но autorun обязан стартовать на LoadGame (док-гарантия):
                        // Run() на Ready → RunQuest с нуля. Не-autorun остаётся Ready — армирован для ручного
                        // запуска (RunQuestHandler). Interrupt-приоритет держит CheckQuestStartConditions ниже.
                        if (quest.Autorun)
                            Run(quest);
                        break;
                    case QuestState.InProgress:
                        //порядок запуска не гарантирован
                        Run(quest);
                        break;
                    case QuestState.Reward:
                    case QuestState.Completed:
                    case QuestState.Failed:
                        CompletedQuests.Add(quest.GuidPreset, quest);
                        break;
                    case QuestState.Blocked:
                        // Обратимый блок: если условия прерывания снялись — назад в турникет, дальше его
                        // перегейтит CheckQuestStartConditions ниже. Иначе остаётся Blocked, а exit-watch
                        // навесит interrupt-пасс того же вызова (проход B, ветка else). Липкий блок
                        // (BlockRemovable=false) держится как есть, условия не перечитываются.
                        if (quest.BlockRemovable && !quest.AnyInterruptGroupTrue())
                            quest.State = QuestState.Locked;

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                quest.CallOnUpdated();
            }

            CheckQuestStartConditions();
        }

        /// <summary>
        /// Сброс подписок квестов в точках останова игры
        /// </summary>
        private static void CheckState()
        {
            if (_data == null) return;
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Win:
                case GameStates.Fail:
                    //Сброс квестов не делаем. данные могут быть нужны для статистики
                    break;
                case GameStates.Off:
                case GameStates.Loading:
                    ResetController();
                    foreach (var quest in _data.Index.Values)
                        quest.Reset();
                    break;
            }
        }

        private static void ResetController()
        {
            CompletedQuests.Clear();
            if (ActiveQuests.Count > 0)
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
                _cancelTokenSource = new CancellationTokenSource();
                ActiveQuests.Clear();
            }

            // Глобальный Cancel каскадом гасит связанные токены; сами объекты CTS диспоузим отдельно.
            foreach (var cts in ActiveCts.Values)
                cts.Dispose();
            ActiveCts.Clear();

            _data = GameController.Get<QuestModels>();
        }

        /// <summary>
        /// Проверка на выполнение условий старта
        /// </summary>
        public static void CheckQuestStartConditions() => CheckQuestStartConditions(0);

        /// <summary>
        /// Проверка на выполнение условий старта
        /// С предохранителем
        /// </summary>
        private static void CheckQuestStartConditions(int counter)
        {
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Off:
                case GameStates.Loading:
                    return;
            }

            CheckQuestInterruptConditions();

            var list = _data.Index.Values.Where(q => q.State == QuestState.Locked);
            var updated = false;
            foreach (var quest in list)
                if (CheckQuestStart(quest))
                {
                    updated = true;
                    quest.State = QuestState.Ready;
                    quest.CallOnUpdated();
                    if (!quest.Autorun)
                        continue;
                    if (ActiveQuests.ContainsKey(quest))
                    {
                        Debug.LogError("[QuestController] Нарушение логики. Попытка перезапуска активного квеста!");
                        continue;
                    }

                    ActiveQuests[quest] = RunQuest(quest, CreateQuestToken(quest));
                }

            if (updated && ++counter < 10)
            {
                OnUpdateData?.Invoke();
                //Рекурсивная проверка, на случай если что-то меняется
                CheckQuestStartConditions(counter);
            }

            OnUpdateData?.Invoke();
            OnCheckConditions?.Invoke();
        }

        private static async UniTask RunQuest(QuestModel quest, CancellationToken token)
        {
            try
            {
                foreach (var condition in quest.StartConditions)
                    condition.DisposeListeners();

                quest.State = QuestState.InProgress;
                quest.CallOnUpdated();

                foreach (var logic in quest.Logics)
                {
                    if (token.IsCancellationRequested)
                        return;

                    //Обработка уникальных маркерных логик
                    switch (logic)
                    {
                        //Сохранение этапа квеста
                        case SavePoint sp:
                            quest.Step = sp.Key;
                            break;
                    }

                    var b = await logic.Run(token);
                    if (b) continue;
                    quest.State = !quest.UnFailable
                        ? QuestState.Failed
                        : QuestState.Locked;
                    return;
                }

                quest.State = QuestState.Reward;
                //Если пустая награда, то сразу завершаем
                if (quest.Rewards.Length == 0)
                {
                    await UniTask.Yield();
                    quest.State = QuestState.Completed;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    switch (quest.State)
                    {
                        case QuestState.Unset:
                        case QuestState.Locked:
                            break;
                        case QuestState.Ready:
                        case QuestState.InProgress:
                            Debug.LogError(
                                $"[Quest Controller] Ошибка в логике. Квест завершен в состоянии «{quest.State}». Принудительно переведен в «Completed»");
                            quest.State = QuestState.Completed;
                            CompletedQuests.Add(quest.GuidPreset, quest);
                            break;
                        case QuestState.Reward:
                        case QuestState.Completed:
                        case QuestState.Failed:
                            CompletedQuests.Add(quest.GuidPreset, quest);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    ReleaseQuest(quest);
                    quest.CallOnUpdated();
                    OnUpdateData?.Invoke();
                    CheckQuestStartConditions();
                }
            }
        }

        private static async UniTask RestoreQuest(QuestModel quest, CancellationToken token)
        {
            if (quest.State != QuestState.InProgress)
            {
                Debug.LogError(
                    $"[QuestController] Некорректная попытка восстановления квеста «{quest.Name}» is «{quest.State}»");
                return;
            }

            try
            {
                var search = quest.Step != 0;
                quest.CallOnUpdated();
                foreach (var logic in quest.Logics)
                {
                    if (search)
                    {
                        if (logic is SavePoint s && quest.Step == s.Key)
                            search = false;
                        continue;
                    }

                    if (token.IsCancellationRequested)
                        return;
                    //Обработка уникальных маркерных логик
                    switch (logic)
                    {
                        //Сохранение этапа квеста
                        case SavePoint sp:
                            quest.Step = sp.Key;
                            break;
                    }

                    var b = await logic.Run(token);
                    if (b) continue;
                    quest.State = !quest.UnFailable
                        ? QuestState.Failed
                        : QuestState.Locked;
                    return;
                }

                quest.State = QuestState.Reward;
                //Если пустая награда, то сразу завершаем
                if (quest.Rewards.Length == 0)
                {
                    await UniTask.Yield();
                    quest.State = QuestState.Completed;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    switch (quest.State)
                    {
                        case QuestState.Unset:
                        case QuestState.Locked:
                            break;
                        case QuestState.Ready:
                        case QuestState.InProgress:
                            Debug.LogError(
                                $"[Quest Controller] Ошибка в логике. Квест завершен в состоянии «{quest.State}». Принудительно переведен в «Completed»");
                            quest.State = QuestState.Completed;
                            CompletedQuests.Add(quest.GuidPreset, quest);
                            break;
                        case QuestState.Reward:
                        case QuestState.Completed:
                        case QuestState.Failed:
                            CompletedQuests.Add(quest.GuidPreset, quest);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    ReleaseQuest(quest);
                    quest.CallOnUpdated();
                    OnUpdateData?.Invoke();
                    CheckQuestStartConditions();
                }
            }
        }

        /// <summary>
        /// Проверка на условия запуска квеста
        /// </summary>
        /// <param name="quest"></param>
        /// <returns></returns>
        // AND между группами (All) с ленивым short-circuit: активна одна группа-блокер за раз — тот же
        // принцип атомарного детерминированного отслеживания, что и внутри группы (см. QuestConditions.Check).
        private static bool CheckQuestStart(this QuestModel quest) =>
            quest.StartConditions.Length == 0 || quest.StartConditions.All(c => c.Check());

        #region Прерывание

        /// <summary>
        /// Interrupt-пасс: блокировка живых квестов, у которых сработало прерывание, и разблокировка
        /// обратимо-заблокированных, у которых оно снялось. Вызывается первой строкой
        /// <see cref="CheckQuestStartConditions(int)"/>, поэтому запрет всегда приоритетнее старта
        /// (INV-3), в том числе на каждой итерации settle-рекурсии.
        /// </summary>
        private static void CheckQuestInterruptConditions()
        {
            // Проход A — детект блокировки. Снимок перед итерацией: Block меняет состояние по ходу.
            foreach (var quest in _data.Index.Values.ToArray())
            {
                if (quest.State != QuestState.Locked &&
                    quest.State != QuestState.Ready &&
                    quest.State != QuestState.InProgress)
                    continue;
                if (quest.AnyInterruptGroupTrue())
                    quest.BlockQuest();
            }

            // Проход B — детект разблокировки (только обратимые).
            foreach (var quest in _data.Index.Values.ToArray())
            {
                if (quest.State != QuestState.Blocked || !quest.BlockRemovable)
                    continue;
                if (!quest.AnyInterruptGroupTrue())
                    quest.UnblockQuest();
                else
                    // Check() истинной группы снёс exit-watch — восстанавливаем, иначе снятие условия
                    // не разбудит проверку и квест застрянет в Blocked.
                    quest.InitAllInterruptListeners();
            }
        }

        /// <summary>
        /// Свёртка условий прерывания: OR между группами. Пустой набор ⇒ непрерываем.
        /// <see cref="QuestConditions.Check"/> на false-группе подписывает блокер — так живой квест с
        /// ложным прерыванием остаётся под наблюдением до его срабатывания.
        /// </summary>
        private static bool AnyInterruptGroupTrue(this QuestModel quest)
        {
            if (quest.InterruptConditions.Length == 0)
                return false;
            foreach (var group in quest.InterruptConditions)
                if (group.Check())
                    return true;
            return false;
        }

        /// <summary>
        /// Перевод квеста в <see cref="QuestState.Blocked"/>. Из InProgress — отмена его логики (свой
        /// CTS), снятие из активных; сброс прогресса (<see cref="QuestModel.Step"/> = 0 ⇒ повторный вход
        /// строго с нуля через RunQuest). Старт-подписки снимаются. Для обратимого блока — exit-watch
        /// на все условия прерывания; для липкого — подписки не нужны.
        /// </summary>
        private static void BlockQuest(this QuestModel quest)
        {
            if (quest.State == QuestState.InProgress)
            {
                if (ActiveCts.TryGetValue(quest, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                    ActiveCts.Remove(quest);
                }

                ActiveQuests.Remove(quest);
            }

            quest.Step = 0;

            foreach (var condition in quest.StartConditions)
                condition.DisposeListeners();

            quest.State = QuestState.Blocked;

            if (quest.BlockRemovable)
                quest.InitAllInterruptListeners();
            else
                foreach (var group in quest.InterruptConditions)
                    group.DisposeListeners();

            quest.CallOnUpdated();
        }

        /// <summary>
        /// Выход из <see cref="QuestState.Blocked"/> в <see cref="QuestState.Locked"/>: exit-watch
        /// снимается, а перегейтит квест старт-пасс того же цикла (interrupt раньше, старт — следом по
        /// ленивому Where над актуальным состоянием).
        /// </summary>
        private static void UnblockQuest(this QuestModel quest)
        {
            foreach (var group in quest.InterruptConditions)
                group.DisposeListeners();
            quest.State = QuestState.Locked;
            quest.CallOnUpdated();
        }

        /// <summary>Безусловная подписка всех условий всех групп прерывания — exit-watch обратимого блока.</summary>
        private static void InitAllInterruptListeners(this QuestModel quest)
        {
            foreach (var group in quest.InterruptConditions)
                group.InitAllListeners();
        }

        #endregion

        #region Пер-квестовый CTS

        /// <summary>
        /// Заводит пер-квестовый CTS, связанный с глобальным, и возвращает его токен. Существующий (при
        /// нештатном перезапуске) диспоузится, чтобы не утёк.
        /// </summary>
        private static CancellationToken CreateQuestToken(QuestModel quest)
        {
            if (ActiveCts.TryGetValue(quest, out var existing))
                existing.Dispose();

            var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancelTokenSource.Token);
            ActiveCts[quest] = cts;
            return cts.Token;
        }

        /// <summary>
        /// Снятие завершённого квеста: диспоуз interrupt-подписок и пер-квестового CTS, удаление из
        /// активных. Зовётся из штатного терминала в finally RunQuest/RestoreQuest (не при отмене —
        /// там guard <c>!token.IsCancellationRequested</c>; при блокировке всё это делает BlockQuest).
        /// </summary>
        private static void ReleaseQuest(QuestModel quest)
        {
            foreach (var group in quest.InterruptConditions)
                group.DisposeListeners();

            if (ActiveCts.TryGetValue(quest, out var cts))
            {
                cts.Dispose();
                ActiveCts.Remove(quest);
            }

            ActiveQuests.Remove(quest);
        }

        #endregion

        /// <summary>
        /// Запуск готового квеста
        /// </summary>
        /// <param name="quest"></param>
        public static void Run(this QuestModel quest)
        {
            if (quest.State != QuestState.Ready && quest.State != QuestState.InProgress)
            {
                Debug.LogError(
                    $"[QuestController] Попытка запуска неготового квеста «{quest.Name}» is «{quest.State}»");
                return;
            }

            if (quest.State == QuestState.InProgress)
                ActiveQuests[quest] = RestoreQuest(quest, CreateQuestToken(quest));
            else
                ActiveQuests[quest] = RunQuest(quest, CreateQuestToken(quest));
        }

        /// <summary>
        /// Регистрация модели для прослушивания
        /// </summary>
        /// <param name="reactiveData"></param>
        /// <param name="source">Источник запроса на подписку</param>
        public static void SetListener(IReactiveData reactiveData, object source)
        {
            if (!Listeners.TryGetValue(reactiveData, out var listener))
            {
                reactiveData.OnUpdateData += CheckQuestStartConditions;
                Listeners.Add(reactiveData, new HashSet<object> { source });
                return;
            }

            listener.Add(source);
        }

        /// <summary>
        /// Снятие модели с прослушивания
        /// </summary>
        /// <param name="source">Источник запроса на подписку</param>
        public static void RemoveListener(object source)
        {
            var toRemove = new List<IReactiveData>();
            foreach (var listener in Listeners)
            {
                listener.Value.Remove(source);
                if (listener.Value.Count == 0)
                    toRemove.Add(listener.Key);
            }

            foreach (var key in toRemove)
            {
                key.OnUpdateData -= CheckQuestStartConditions;
                Listeners.Remove(key);
            }
        }

        /// <summary>
        /// Возвращает список квестов готовых к выдаче награды
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyList<QuestModel> GetRewardsReady() => CompletedQuests
            .Where(d => d.Value.State == QuestState.Reward)
            .Select(d => d.Value).ToArray();

        /// <summary>
        /// Запускает логику выдачи наград
        /// </summary>
        /// <param name="quest"></param>
        public static void GiveRewards(this QuestModel quest)
        {
            if (quest.State != QuestState.Reward)
            {
                Debug.LogError(
                    $"[QuestController] Невалидная попытка получить награду квеста #{quest.Name}:{quest.State}");
                return;
            }

            try
            {
                foreach (var reward in quest.Rewards)
                    reward.GiveReward();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                quest.State = QuestState.Completed;
                quest.CallOnUpdated();
            }
        }
    }
}
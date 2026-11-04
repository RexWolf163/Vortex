using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.Quests.Conditions;
using Vortex.Sdk.Quests.Diagnostics;
using Vortex.Sdk.Quests.QuestsLogics;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Sdk.Quests
{
    /// <summary>
    /// Контроллер работает с квестами как с multyinstance записями
    /// </summary>
    public static partial class QuestController
    {
        private static object _owner = new();

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
        /// Единственный реестр квестов с запущенной логикой: запись заводится ДО запуска прогона
        /// (<see cref="CreateQuestToken"/>) и снимается на каждом выходе — <see cref="ReleaseQuest"/> (штатный терминал),
        /// <see cref="CancelQuestLogic"/> (блокировка, дебаг), <see cref="ResetController"/> (новая игра/загрузка/Off/Loading).
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
            QuestDiagnostics.RecordSeparator("NewGame");
            FillQuestNames();

            //Инициализация квестов
            var list = _data.Index.Values.Where(q => q.State == QuestState.Unset);
            //безусловное переключение, но сам по себе этап может быть
            //полезен для отлова новых квестов на сейве, например
            foreach (var quest in list)
                quest.SetState(QuestState.Locked, TransitionReason.Init);

            CheckQuestStartConditions();
        }

        private static void LoadGameLogic()
        {
            ResetController();
            QuestDiagnostics.RecordSeparator("LoadGame");

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

            FillQuestNames();

            foreach (var quest in _data.Index.Values)
            {
                switch (quest.State)
                {
                    case QuestState.Unset:
                        //безусловное переключение, но сам по себе этап может быть
                        //полезен для отлова новых квестов на сейве, например
                        quest.SetState(QuestState.Locked, TransitionReason.Init);
                        break;
                    case QuestState.Locked:
                        break;
                    case QuestState.Ready:
                        // Ready латчится (условия старта одноразовы, назад — только через Interrupt), поэтому
                        // условия НЕ перечитываем. Но autorun обязан стартовать на LoadGame (док-гарантия):
                        // Run() на Ready → RunQuest с нуля. Не-autorun остаётся Ready — армирован для ручного
                        // запуска (RunQuestHandler).
                        // Приоритет прерывания здесь НЕ соблюдается: Run() стартует сразу, а interrupt-пасс
                        // CheckQuestStartConditions ниже отложен на конец кадра (TimeController). Синхронная часть
                        // логик (до первого незавершённого await) отработает до проверки прерывания; если оно
                        // истинно — квест заблокируется в конце кадра с отменой логики, но побочные эффекты
                        // уже выполненных логик не откатываются.
                        if (quest.Autorun)
                            Run(quest);
                        break;
                    case QuestState.InProgress:
                        // Порядок запуска между квестами не гарантирован. Приоритет прерывания не соблюдается —
                        // как у Ready+autorun выше: восстановление стартует до отложенного interrupt-пасса.
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
                            quest.SetState(QuestState.Locked, TransitionReason.Unblocked, detail: "LoadGame");

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                quest.CallOnUpdated();
            }

            CheckQuestStartConditions();
        }

        /// <summary>
        /// Проставляет <see cref="QuestModel.QuestName"/> — читаемое имя из пресета с тем же GUID. Записи индекса
        /// собраны из пресетов (<c>QuestModels</c>) и имя получают там же; здесь оно восстанавливается после
        /// загрузки сейва, записанного до появления поля, — там в него пишется пустое значение.
        /// </summary>
        private static void FillQuestNames()
        {
            foreach (var quest in _data.Index.Values)
                quest.QuestName = quest.Name;
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
            TimeController.RemoveCall(_owner);

            CompletedQuests.Clear();
            if (ActiveCts.Count > 0)
            {
                _cancelTokenSource.Cancel();
                _cancelTokenSource.Dispose();
                _cancelTokenSource = new CancellationTokenSource();
            }

            // Глобальный Cancel каскадом гасит связанные токены; сами объекты CTS диспоузим отдельно.
            foreach (var cts in ActiveCts.Values)
                cts.Dispose();
            ActiveCts.Clear();
            QuestDiagnostics.ClearTraces();

            _data = GameController.Get<QuestModels>();
        }

        /// <summary>
        /// Проверка на выполнение условий старта
        /// </summary>
        public static void CheckQuestStartConditions() =>
            TimeController.Call(() => CheckQuestStartConditions(0), _owner);

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
                    // Перепроверка прерывания на момент перехода (INV-3): interrupt-пасс выше видел состояние на
                    // начало прохода, а синхронные логики квестов, запущенных раньше в этом цикле, могли его изменить.
                    if (quest.AnyInterruptGroupTrue())
                    {
                        quest.BlockQuest();
                        continue;
                    }

                    quest.SetState(QuestState.Ready, TransitionReason.ConditionsMet);
                    quest.CallOnUpdated();
                    if (!quest.Autorun)
                        continue;
                    if (ActiveCts.ContainsKey(quest))
                    {
                        Debug.LogError("[QuestController] Нарушение логики. Попытка перезапуска активного квеста!");
                        continue;
                    }

                    RunQuest(quest, CreateQuestToken(quest)).Forget();
                }

            if (updated && ++counter < 10)
            {
                OnUpdateData?.Invoke();
                //Рекурсивная проверка, на случай если что-то меняется
                CheckQuestStartConditions(counter);
            }
            else if (updated)
                QuestDiagnostics.RecordSettleOverflow();

            OnUpdateData?.Invoke();
            OnCheckConditions?.Invoke();
        }

        private static async UniTask RunQuest(QuestModel quest, CancellationToken token)
        {
            var logicIndex = -1;
            Exception exception = null;
            try
            {
                foreach (var condition in quest.StartConditions)
                    condition.DisposeListeners();

                quest.SetState(QuestState.InProgress, TransitionReason.Started);
                QuestDiagnostics.SetRestoreStart(quest, -1);
                quest.CallOnUpdated();

                for (logicIndex = 0; logicIndex < quest.Logics.Length; logicIndex++)
                {
                    var logic = quest.Logics[logicIndex];
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

                    QuestDiagnostics.SetCurrentLogic(quest, logicIndex);
                    var b = await logic.Run(token);
                    // Отменённый прогон состояние не пишет: им уже распорядился отменивший (блокировка, сброс,
                    // дебаг-действие). Логика не обязана бросать при отмене — может вернуть true/false.
                    if (token.IsCancellationRequested)
                        return;
                    if (b) continue;
                    quest.SetState(!quest.UnFailable
                        ? QuestState.Failed
                        : QuestState.Locked, TransitionReason.LogicFailed, logicIndex);
                    return;
                }

                QuestDiagnostics.SetCurrentLogic(quest, -1);
                quest.SetState(QuestState.Reward, TransitionReason.LogicsDone);
                //Если пустая награда, то сразу завершаем
                if (quest.Rewards.Length == 0)
                {
                    await UniTask.Yield();
                    if (token.IsCancellationRequested)
                        return;
                    quest.SetState(QuestState.Completed, TransitionReason.AutoCompleteNoRewards);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                Debug.LogException(ex);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    FinishRun(quest, logicIndex, exception);
            }
        }

        /// <summary>
        /// Продолжение InProgress-квеста с точки сохранения. Состояние проверяет <see cref="Run"/>: ранний выход
        /// здесь миновал бы <see cref="FinishRun"/> и оставил квест в реестре активных навсегда.
        /// </summary>
        private static async UniTask RestoreQuest(QuestModel quest, CancellationToken token)
        {
            var logicIndex = -1;
            Exception exception = null;
            try
            {
                var search = quest.Step != 0;
                QuestDiagnostics.SetRestoreStart(quest, search ? FindRestoreStart(quest) : 0);
                QuestDiagnostics.RecordNote(quest, TransitionReason.Restored, $"Step {quest.Step}");
                quest.CallOnUpdated();
                for (logicIndex = 0; logicIndex < quest.Logics.Length; logicIndex++)
                {
                    var logic = quest.Logics[logicIndex];
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

                    QuestDiagnostics.SetCurrentLogic(quest, logicIndex);
                    var b = await logic.Run(token);
                    // Отменённый прогон состояние не пишет — см. RunQuest
                    if (token.IsCancellationRequested)
                        return;
                    if (b) continue;
                    quest.SetState(!quest.UnFailable
                        ? QuestState.Failed
                        : QuestState.Locked, TransitionReason.LogicFailed, logicIndex);
                    return;
                }

                QuestDiagnostics.SetCurrentLogic(quest, -1);
                quest.SetState(QuestState.Reward, TransitionReason.LogicsDone);
                //Если пустая награда, то сразу завершаем
                if (quest.Rewards.Length == 0)
                {
                    await UniTask.Yield();
                    if (token.IsCancellationRequested)
                        return;
                    quest.SetState(QuestState.Completed, TransitionReason.AutoCompleteNoRewards);
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                Debug.LogException(ex);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    FinishRun(quest, logicIndex, exception);
            }
        }

        /// <summary>
        /// Штатный терминал прогона логик (не при отмене): фиксация результата, освобождение квеста,
        /// перепроверка условий. Общий для <see cref="RunQuest"/> и <see cref="RestoreQuest"/>.
        /// </summary>
        /// <param name="logicIndex">Номер логики, на которой остановился прогон (для журнала)</param>
        /// <param name="exception">Исключение прогона, если было</param>
        private static void FinishRun(QuestModel quest, int logicIndex, Exception exception)
        {
            // Освобождение первым: исключение ниже (напр. дубль в CompletedQuests) не должно оставить квест
            // в реестре активных — иначе он навсегда считается «бегущим» и не перезапускается.
            ReleaseQuest(quest);
            QuestDiagnostics.SetCurrentLogic(quest, -1);
            switch (quest.State)
            {
                case QuestState.Unset:
                case QuestState.Locked:
                    break;
                case QuestState.Ready:
                case QuestState.InProgress:
                    Debug.LogError(
                        $"[Quest Controller] Ошибка в логике. Квест завершен в состоянии «{quest.State}». Принудительно переведен в «Completed»");
                    quest.SetState(QuestState.Completed, TransitionReason.LogicException,
                        logicIndex < quest.Logics.Length ? logicIndex : -1,
                        exception?.Message ?? $"Завершён в состоянии «{quest.State}»");
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

            quest.CallOnUpdated();
            OnUpdateData?.Invoke();
            CheckQuestStartConditions();
        }

        /// <summary>
        /// Номер логики, с которой продолжится восстановление: следующая за SavePoint с Key == Step.
        /// SavePoint не найден — пропускаются все логики.
        /// </summary>
        private static int FindRestoreStart(QuestModel quest)
        {
            for (var i = 0; i < quest.Logics.Length; i++)
                if (quest.Logics[i] is SavePoint s && quest.Step == s.Key)
                    return i + 1;
            return quest.Logics.Length;
        }

        /// <summary>
        /// Проверка на условия запуска квеста
        /// </summary>
        /// <param name="quest"></param>
        /// <returns></returns>
        // AND между группами с ленивым short-circuit: активна одна группа-блокер за раз — тот же
        // принцип атомарного детерминированного отслеживания, что и внутри группы (см. QuestConditions.Check).
        private static bool CheckQuestStart(this QuestModel quest)
        {
            if (quest.StartConditions.Length == 0)
                return true;

            QuestDiagnostics.BeginCheck(quest, ConditionSetKind.Start);
            try
            {
                for (var i = 0; i < quest.StartConditions.Length; i++)
                    if (!CheckGroup(quest.StartConditions[i], i))
                        return false;
                return true;
            }
            finally
            {
                QuestDiagnostics.EndCheck();
            }
        }

        /// <summary>
        /// Проверка группы условий с фиксацией результата в открытом контексте монитора.
        /// </summary>
        private static bool CheckGroup(QuestConditions group, int index)
        {
            QuestDiagnostics.EnterNode(index);
            var result = group.Check();
            QuestDiagnostics.ExitNode(result);
            return result;
        }

        #region Прерывание

        /// <summary>
        /// Interrupt-пасс: блокировка живых квестов, у которых сработало прерывание, и разблокировка
        /// обратимо-заблокированных, у которых оно снялось. Вызывается первой строкой
        /// <see cref="CheckQuestStartConditions(int)"/>, поэтому запрет приоритетнее старта (INV-3), в том числе на
        /// каждой итерации settle-рекурсии. Внутри одного прохода квест, прошедший старт, перед переходом в Ready
        /// перепроверяется отдельно — состояние могли изменить синхронные логики квестов, запущенных раньше.
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

            QuestDiagnostics.BeginCheck(quest, ConditionSetKind.Interrupt);
            try
            {
                for (var i = 0; i < quest.InterruptConditions.Length; i++)
                    if (CheckGroup(quest.InterruptConditions[i], i))
                        return true;
                return false;
            }
            finally
            {
                QuestDiagnostics.EndCheck();
            }
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
                CancelQuestLogic(quest);

            quest.Step = 0;

            foreach (var condition in quest.StartConditions)
                condition.DisposeListeners();

            quest.SetState(QuestState.Blocked, TransitionReason.Interrupted);

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
        private static void UnblockQuest(this QuestModel quest,
            TransitionReason reason = TransitionReason.Unblocked, string detail = null)
        {
            foreach (var group in quest.InterruptConditions)
                group.DisposeListeners();
            quest.SetState(QuestState.Locked, reason, detail: detail);
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
        /// Заводит пер-квестовый CTS, связанный с глобальным, и возвращает его токен. Вызывающий обязан
        /// убедиться, что квест не в реестре активных (<see cref="ActiveCts"/>).
        /// </summary>
        private static CancellationToken CreateQuestToken(QuestModel quest)
        {
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
        }

        /// <summary>
        /// Отмена выполняющейся логики квеста: Cancel пер-квестового CTS (прогон выйдет без штатного терминала —
        /// guard <c>!token.IsCancellationRequested</c>), снятие из активных.
        /// </summary>
        private static void CancelQuestLogic(QuestModel quest)
        {
            if (ActiveCts.TryGetValue(quest, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                ActiveCts.Remove(quest);
            }

            QuestDiagnostics.SetCurrentLogic(quest, -1);
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

            if (ActiveCts.ContainsKey(quest))
            {
                Debug.LogError($"[QuestController] Квест «{quest.Name}» уже выполняется — повторный запуск отклонён");
                return;
            }

            if (quest.State == QuestState.InProgress)
                RestoreQuest(quest, CreateQuestToken(quest)).Forget();
            else
                RunQuest(quest, CreateQuestToken(quest)).Forget();
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

            Exception exception = null;
            try
            {
                foreach (var reward in quest.Rewards)
                    reward.GiveReward();
            }
            catch (Exception ex)
            {
                exception = ex;
                Debug.LogException(ex);
            }
            finally
            {
                quest.SetState(QuestState.Completed,
                    exception == null ? TransitionReason.RewardsGiven : TransitionReason.RewardException,
                    detail: exception?.Message);
                quest.CallOnUpdated();
            }
        }
    }
}
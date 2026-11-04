#if UNITY_EDITOR
using UnityEngine;
using Vortex.Sdk.Quests.Diagnostics;

namespace Vortex.Sdk.Quests
{
    /// <summary>
    /// Дебаг-действие монитора квестов.
    /// </summary>
    internal enum QuestDebugAction
    {
        /// <summary>Ready: штатный <c>QuestController.Run</c></summary>
        Run,

        /// <summary>Reward: штатный <c>QuestController.GiveRewards</c></summary>
        GiveRewards,

        /// <summary>Любое состояние: штатная перепроверка условий всех квестов</summary>
        Recheck,

        /// <summary>Остановить логику и завершить как штатно: Reward при наградах, иначе Completed</summary>
        CompleteSuccess,

        /// <summary>Остановить логику и завершить как провал логики: UnFailable → Locked, иначе Failed</summary>
        CompleteFail,

        /// <summary>Остановить логику, Step = 0, убрать из завершённых, → Locked</summary>
        Reset,

        /// <summary>Blocked → Locked независимо от BlockRemovable</summary>
        Unblock
    }

    /// <summary>
    /// Дебаг-действия монитора квестов. Окно только передаёт запрос — решение и изменение здесь.
    /// Принудительные действия идут по штатным переходам, поэтому инвариант контроллера сохраняется (INV-D4):
    /// Reward/Completed/Failed ⇔ квест в <see cref="CompletedQuests"/>; в активных — только квесты с
    /// выполняющейся логикой.
    /// </summary>
    public static partial class QuestController
    {
        /// <summary>
        /// Индекс квестов, с которым работает контроллер; null — до первой новой игры или загрузки. Монитор
        /// читает его, а не <c>GameController.Get</c>: тот лениво создаёт модель игры, а монитор не должен
        /// менять поведение (INV-D1).
        /// </summary>
        internal static QuestModels DebugData => _data;

        internal static bool IsDebugActionAllowed(QuestModel quest, QuestDebugAction action) =>
            action switch
            {
                QuestDebugAction.Run => quest.State == QuestState.Ready,
                QuestDebugAction.GiveRewards => quest.State == QuestState.Reward,
                QuestDebugAction.Recheck => true,
                QuestDebugAction.CompleteSuccess or QuestDebugAction.CompleteFail => quest.State is QuestState.Locked
                    or QuestState.Ready or QuestState.InProgress or QuestState.Blocked,
                QuestDebugAction.Reset => true,
                QuestDebugAction.Unblock => quest.State == QuestState.Blocked,
                _ => false
            };

        internal static void ApplyDebugAction(QuestModel quest, QuestDebugAction action)
        {
            if (!IsDebugActionAllowed(quest, action))
            {
                Debug.LogError(
                    $"[QuestController] Дебаг-действие «{action}» недоступно для квеста «{quest.Name}» в состоянии «{quest.State}»");
                return;
            }

            var detail = action.ToString();
            switch (action)
            {
                case QuestDebugAction.Run:
                    QuestDiagnostics.RecordNote(quest, TransitionReason.DebugAction, detail);
                    Run(quest);
                    return;

                case QuestDebugAction.GiveRewards:
                    QuestDiagnostics.RecordNote(quest, TransitionReason.DebugAction, detail);
                    GiveRewards(quest);
                    return;

                case QuestDebugAction.Recheck:
                    QuestDiagnostics.RecordNote(null, TransitionReason.DebugAction, detail);
                    CheckQuestStartConditions();
                    return;

                case QuestDebugAction.CompleteSuccess:
                    StopQuest(quest);
                    ForceState(quest, quest.Rewards.Length > 0 ? QuestState.Reward : QuestState.Completed, detail);
                    CompletedQuests[quest.GuidPreset] = quest;
                    break;

                case QuestDebugAction.CompleteFail:
                    StopQuest(quest);
                    if (quest.UnFailable)
                        ForceState(quest, QuestState.Locked, detail);
                    else
                    {
                        ForceState(quest, QuestState.Failed, detail);
                        CompletedQuests[quest.GuidPreset] = quest;
                    }

                    break;

                case QuestDebugAction.Reset:
                    StopQuest(quest);
                    CompletedQuests.Remove(quest.GuidPreset);
                    quest.Step = 0;
                    ForceState(quest, QuestState.Locked, detail);
                    break;

                case QuestDebugAction.Unblock:
                    quest.UnblockQuest(TransitionReason.DebugAction, detail);
                    break;
            }

            quest.CallOnUpdated();
            OnUpdateData?.Invoke();
            CheckQuestStartConditions();
        }

        /// <summary>
        /// Принудительный переход с причиной <see cref="TransitionReason.DebugAction"/>. Если состояние не
        /// меняется (напр. провал UnFailable-квеста в Locked), действие всё равно фиксируется в журнале записью.
        /// </summary>
        private static void ForceState(QuestModel quest, QuestState state, string detail)
        {
            if (quest.State == state)
                QuestDiagnostics.RecordNote(quest, TransitionReason.DebugAction, detail);
            else
                quest.SetState(state, TransitionReason.DebugAction, detail: detail);
        }

        /// <summary>
        /// Снятие квеста с исполнения перед принудительным переходом: отмена логики, снятие подписок старта
        /// и прерывания. Подписки, нужные новому состоянию, навесит перепроверка условий.
        /// </summary>
        private static void StopQuest(QuestModel quest)
        {
            CancelQuestLogic(quest);
            foreach (var condition in quest.StartConditions)
                condition.DisposeListeners();
            foreach (var group in quest.InterruptConditions)
                group.DisposeListeners();
        }
    }
}
#endif

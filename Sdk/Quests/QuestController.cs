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
        /// Список событий для подписки на контроль изменений 
        /// </summary>
        private static readonly Dictionary<IReactiveData, HashSet<object>> Listeners = new();

        /// <summary>
        /// Токен прерывания
        /// </summary>
        private static CancellationToken Token => _cancelTokenSource.Token;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            GameController.OnNewGame -= NewGameLogic;
            GameController.OnNewGame += NewGameLogic;
            GameController.OnLoadGame -= LoadGameLogic;
            GameController.OnLoadGame += LoadGameLogic;
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
                    case QuestState.Ready:
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
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Win:
                case GameStates.Fail:
                    //Сброс квестов не делаем. данные могут быть нужны для статистики
                    break;
                case GameStates.Off:
                case GameStates.Loading:
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
                {
                    foreach (var quest in _data.Index.Values) quest.Reset();
                    return;
                }
                case GameStates.Loading:
                    return;
            }

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

                    ActiveQuests[quest] = RunQuest(quest, Token);
                }

            if (updated && ++counter < 10)
            {
                OnUpdateData?.Invoke();
                //Рекурсивная проверка, на случай если что-то меняется
                CheckQuestStartConditions(counter);
            }

            OnUpdateData?.Invoke();
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

                    ActiveQuests.Remove(quest);
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

                    ActiveQuests.Remove(quest);
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
        private static bool CheckQuestStart(this QuestModel quest) =>
            quest.StartConditions.Length == 0 || quest.StartConditions.All(c => c.Check());

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
                ActiveQuests[quest] = RestoreQuest(quest, Token);
            else
                ActiveQuests[quest] = RunQuest(quest, Token);
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
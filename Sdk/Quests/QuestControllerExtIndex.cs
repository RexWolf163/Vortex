namespace Vortex.Sdk.Quests
{
    /// <summary>
    /// Расширение для доступа к индексу квестов
    /// </summary>
    public static partial class QuestController
    {
        public static bool IsComplete(string id) => CompletedQuests.ContainsKey(id);

        /// <summary>
        /// Квест текущей игры по GUID пресета. Нет индекса (до новой игры или загрузки) или квеста — false.
        /// </summary>
        internal static bool TryGetQuest(string id, out QuestModel quest)
        {
            quest = null;
            return _data != null && id != null && _data.Index.TryGetValue(id, out quest);
        }
    }
}
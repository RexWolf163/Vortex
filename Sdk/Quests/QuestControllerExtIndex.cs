namespace Vortex.Sdk.Quests
{
    /// <summary>
    /// Расширение для доступа к индексу квестов
    /// </summary>
    public static partial class QuestController
    {
        public static bool IsComplete(string id) => CompletedQuests.ContainsKey(id);
    }
}
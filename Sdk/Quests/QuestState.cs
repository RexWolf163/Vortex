namespace Vortex.Sdk.Quests
{
    public enum QuestState
    {
        Unset, //Не инициирован
        Locked, //Неготов к запуску по условиям
        Ready, //Готов к запуску
        InProgress, //В процессе
        Reward, //Завершен. доступен для снятия награды (опционально)
        Completed, //Награда получена
        Failed //Завершен как проваленный
    }
}
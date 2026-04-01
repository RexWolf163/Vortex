namespace Vortex.Core.UIProviderSystem.Model
{
    /// <summary>
    /// Типы ответа при проверке условий
    /// </summary>
    public enum ConditionAnswer
    {
        Idle, //неопределнно
        Open, //требует открыть UI
        Close //требует закрыть UI
    }
}
namespace Vortex.NaniExtensions.NaniScriptLinks
{
    /// <summary>
    /// Контракт «запускает сценарий Naninovel»: объект квеста (логика) сообщает валидатору связей
    /// (<c>Tools/Vortex/Debug/Nani Script Links</c>), какой сценарий он запускает. Только объект, реализующий контракт,
    /// делает сценарий достижимым; объект, который лишь ссылается на сценарий (ожидание окончания, отладка), контракт
    /// не реализует. Поведение объекта в игре контракт не меняет.
    /// </summary>
    public interface INaniScriptLaunch
    {
        /// <summary>
        /// Путь сценария в формате реестра Naninovel — от Scenario Root, без расширения
        /// (<c>DLC1/MiniGames/Prologue_NeonBar</c>). Пусто — сценарий не задан.
        /// </summary>
        string ScriptPath { get; }
    }
}

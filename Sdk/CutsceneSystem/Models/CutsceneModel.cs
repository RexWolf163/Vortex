namespace Vortex.Sdk.CutsceneSystem.Models
{
    /// <summary>
    /// Состояние показа: какая катсцена открыта и на какой она фазе. Меняет только <c>CutsceneController</c>
    /// </summary>
    public class CutsceneModel
    {
        /// <summary>
        /// Открытая катсцена. <c>null</c> — катсцена не открыта
        /// </summary>
        public CutsceneData Cutscene { get; internal set; }

        /// <summary>
        /// Номер текущей фазы открытой катсцены
        /// </summary>
        public int PhaseIndex { get; internal set; }
    }
}

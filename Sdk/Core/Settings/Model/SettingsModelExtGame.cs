namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        /// <summary>Потеря фокуса приложением ставит идущую игру на паузу (<c>GameSettings</c>).</summary>
        public bool PauseOnUnfocus { get; private set; } = true;
    }
}

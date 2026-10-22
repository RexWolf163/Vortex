using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Sdk.Core.Presets
{
    /// <summary>
    /// Настройки игровой сессии (<c>GameController</c>). Пресет настроек: значения копируются в <c>SettingsModel</c>,
    /// откуда их читает контроллер игры. Ассет создаётся автоматически в <c>Resources/Settings</c>
    /// </summary>
    public class GameSettings : SettingsPreset
    {
        [InfoBox("Потеря фокуса приложением (свёрнуто, переключение окна) ставит идущую игру на паузу. " +
                 "Снимается пауза только явно — автовозобновления по возврату фокуса нет. В редакторе потерю фокуса " +
                 "дополнительно глушит ignorePauseInEditor в DebugSettings.")]
        [SerializeField]
        private bool pauseOnUnfocus = true;

        public bool PauseOnUnfocus => pauseOnUnfocus;
    }
}

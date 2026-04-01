using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.SettingsSystem.Presets
{
    public class StartSettings : SettingsPreset
    {
        [InfoBox("Стартовая сцена. Используется только в редакторе")]
        [ValueDropdown("@DropDawnHandler.GetScenes()")]
        [SerializeField]
        private string startScene;

        public string StartScene => startScene;
    }
}
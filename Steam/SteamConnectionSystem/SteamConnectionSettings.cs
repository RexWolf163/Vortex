#if UNITY_EDITOR
using System.IO;
#endif
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Steam.SteamConnectionSystem
{
    public class SteamConnectionSettings : ScriptableObject
    {
        /// <summary>
        /// ID проекта на платформе стим
        /// </summary>
        [OnChanged("OnAppUdChanged")] [SerializeField]
        private uint steamAppId = 480;

        /// <summary>
        /// ID проекта на платформе стим
        /// </summary>
        public uint SteamAppId => steamAppId;

        /// <summary>
        /// Активация пакетов коннектора Steam
        /// </summary>
        [OnChanged("OnSteamEnabledChanged")] [ToggleButton(isSingleButton: true)] [SerializeField]
        private bool isEnabled;

        /// <summary>
        /// Активация пакетов коннектора Steam
        /// </summary>
        [ToggleButton(isSingleButton: true)] [SerializeField]
        private bool isTestBuild;

        public bool IsTestBuild => isTestBuild;

        /// <summary>
        /// Активация пакетов коннектора Steam
        /// </summary>
        public bool IsEnabled => isEnabled;

#if UNITY_EDITOR
        private void OnSteamEnabledChanged(bool isEnabled)
        {
            DefineSymbolManager.Refresh();
        }

        internal void OnAppUdChanged()
        {
            File.WriteAllText("steam_appid.txt", SteamAppId.ToString());
        }

        /// <summary>
        /// Выставление AppId в редакторе
        /// </summary>
        /// <param name="id"></param>
        internal void SetAppId(uint id) => steamAppId = id;
#endif
    }
}
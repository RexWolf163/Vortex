#if USING_STEAM
#if UNITY_EDITOR
using System.IO;
#endif
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Steam.SteamConnectionSystem
{
    public class SteamConnectionSettings : ScriptableObject
    {
        /// <summary>
        /// ID проекта на платформе стим
        /// </summary>
        [OnValueChanged("OnAppUdChanged")] [SerializeField]
        private uint steamAppId = 480;

        /// <summary>
        /// ID проекта на платформе стим
        /// </summary>
        public uint SteamAppId => steamAppId;

        /// <summary>
        /// Пропуск RestartAppIfNecessary — запуск вне Steam-клиента (debug из редактора).
        /// </summary>
        [ToggleButton(isSingleButton: true)] [SerializeField]
        private bool isTestBuild;

        public bool IsTestBuild => isTestBuild;

#if UNITY_EDITOR
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
#endif

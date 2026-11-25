using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.CoreAssetsSystem;

namespace Vortex.Unity.NetworkSystem.Drivers.Uwr
{
    /// <summary>
    /// Настройки связи драйвера UnityWebRequest: общие поля <see cref="NetworkSettingsAsset"/> + лимит редиректов.
    /// Создаётся автоматически в <c>Resources/Settings</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "UwrNetworkSettings", menuName = "Vortex/Settings/Network (UnityWebRequest)")]
    public class UwrNetworkSettings : NetworkSettingsAsset, ICoreAsset
    {
        [BoxGroup("UnityWebRequest")]
        [SerializeField, Min(0), Tooltip("Лимит редиректов. На WebGL не применяется: действует лимит браузера.")]
        private int redirectLimit = 32;

        [BoxGroup("UnityWebRequest")]
        [SerializeField, Min(1f), SuffixLabel("ч", true)]
        [Tooltip("Временные файлы тел запросов других запусков старше этого срока удаляются при старте. Папку делят " +
                 "редактор, сборки и клоны проекта, поэтому свежие папки не трогаются.")]
        private float staleTempFolderHours = 24f;

        public int RedirectLimit => redirectLimit;

        public float StaleTempFolderHours => staleTempFolderHours;
    }
}

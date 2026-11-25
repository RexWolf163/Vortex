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

        public int RedirectLimit => redirectLimit;
    }
}

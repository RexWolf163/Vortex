#if UNITY_EDITOR

using UnityEditor;
using Vortex.Unity.AssetCacheSystem.Config;
using Vortex.Unity.Extensions;
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.AssetCacheSystem.Editor
{
    /// <summary>
    /// Меню-команды пакета:
    /// <c>Tools/Vortex/AssetsCache/Runtime Index</c> — открыть runtime-инспектор индекса;
    /// <c>Tools/Vortex/AssetsCache/Config</c> и <c>Tools/Vortex/Configs/AssetCache Settings</c> — подсветить
    /// единственный <see cref="AssetCacheSettings"/>-ассет в Project window. Два входа в одну команду:
    /// первый — рядом с окном пакета, второй — в общем списке конфигов проекта.
    /// </summary>
    public static class MenuController
    {
        [MenuItem("Tools/Vortex/AssetsCache/Runtime Index")]
        private static void OpenRuntimeIndex() => AssetCacheIndexWindow.Open();

        [MenuItem("Tools/Vortex/AssetsCache/Config")]
        private static void FindConfigInPackageMenu() => FindConfig();

        /// <summary>
        /// Подсветить ассет настроек пакета в Project window. Не private: этой же командой пользуется
        /// кнопка «Конфиг» в <see cref="AssetCacheIndexWindow"/> — реализация одна на все входы.
        /// </summary>
        [MenuItem("Tools/Vortex/Configs/AssetCache Settings")]
        internal static void FindConfig()
        {
            var res = AssetDatabaseExt.GetSingletonAsset<AssetCacheSettings>();
            MenuConfigSearchController.FindAsset(res);
        }
    }
}
#endif
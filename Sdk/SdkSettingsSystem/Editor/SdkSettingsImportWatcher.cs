#if UNITY_EDITOR
using UnityEditor;

namespace Vortex.Sdk.SdkSettingsSystem.Editor
{
    /// <summary>
    /// Импортирован ассет <see cref="SdkSettings"/> — дефайны приводятся к его галочкам. Закрывает смену ассета без
    /// перезагрузки домена: подмена файла (AssetSwap), <c>git checkout/pull</c>, внешняя правка. Перезагрузку домена
    /// и смену платформы <see cref="SdkSettings"/> ловит сам.
    ///
    /// Срабатывает только на импорт: перекомпиляция скриптов сюда не попадает (импортируются <c>.cs</c>, ассет
    /// отсекается фильтром). Повтор безвреден — запись в PlayerSettings идёт, только если набор дефайнов изменился.
    /// Синхронизация откладывается через <c>delayCall</c>: во время импорта <c>EditorApplication.isUpdating</c>, и
    /// <see cref="SdkSettings.OnPlatformChanged"/> сам переносит себя до конца импорта/компиляции.
    /// </summary>
    internal sealed class SdkSettingsImportWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                // Заготовки в Editor-папках (варианты AssetSwap) дефайнами не управляют — их импорт не повод.
                if (!path.EndsWith(".asset") || SdkSettings.IsInEditorFolder(path)
                    || AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(SdkSettings))
                    continue;

                // Повторная подписка в пределах одного кадра не плодит вызовы.
                EditorApplication.delayCall -= SdkSettings.OnPlatformChanged;
                EditorApplication.delayCall += SdkSettings.OnPlatformChanged;
                return;
            }
        }
    }
}
#endif

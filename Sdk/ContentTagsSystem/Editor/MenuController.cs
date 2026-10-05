#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Sdk.ContentTagsSystem.Settings;

namespace Vortex.Sdk.ContentTagsSystem.Editor
{
    /// <summary>
    /// Пункты меню пакета <c>ContentTagsSystem</c>. Отдельно от <see cref="ContentTagsWindow"/>:
    /// тут быстрый доступ к ассету настроек, там — ревизия разметки.
    /// </summary>
    public static class MenuController
    {
        [MenuItem("Tools/Vortex/Content Tags/Settings")]
        public static void PingSettings()
        {
            // Ассет не ICoreAsset и может лежать в любой папке Resources — ищем по типу, а не по пути.
            var guids = AssetDatabase.FindAssets($"t:{nameof(ContentSettings)}");
            if (guids.Length == 0)
            {
                Debug.LogError("[ContentTags] Ассет настроек не найден. Создайте его через " +
                               "'Create/Vortex/Settings/Content Tags' и положите в папку Resources.");
                return;
            }

            if (guids.Length > 1)
                Debug.LogWarning($"[ContentTags] В проекте несколько ассетов настроек ({guids.Length}) — " +
                                 "в сборке работает тот, что лежит в Resources.");

            var settings = AssetDatabase.LoadAssetAtPath<ContentSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            if (settings == null)
                return;

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/Vortex/Configs/ContentTags Settings")]
        public static void PingSettingsAlt() => PingSettings();
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Sdk.RecordMarksSystem.Config;

namespace Vortex.Sdk.RecordMarksSystem.Editor
{
    /// <summary>
    /// Пункты меню пакета <c>RecordMarksSystem</c>. Отдельно от
    /// <see cref="RecordMarksIndexWindow"/>: тут доступ к конфигу и утилиты быстрого доступа.
    /// </summary>
    public static class MenuController
    {
        private const string SettingsResourcePath = "Assets/Resources/Settings/RecordMarksSettings.asset";

        [MenuItem("Tools/Vortex/Record Marks/Settings")]
        public static void PingSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<RecordMarksSettings>(SettingsResourcePath);
            if (settings == null)
            {
                Debug.LogWarning(
                    $"[RecordMarks] Settings asset not found at '{SettingsResourcePath}'. " +
                    "Run 'Tools/Vortex/Debug/Check Core Assets' to auto-create it (ICoreAsset), " +
                    "or 'Create/Vortex/Settings/RecordMarks' manually.");
                return;
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/Vortex/Configs/RecordMarks Settings")]
        public static void PingSettingsAlt() => PingSettings();
    }
}
#endif

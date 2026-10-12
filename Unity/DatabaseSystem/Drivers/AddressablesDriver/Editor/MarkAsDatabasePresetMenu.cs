#if UNITY_EDITOR && ENABLE_ADDRESSABLES
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Unity.DatabaseSystem.Drivers.AddressablesDriver.Editor
{
    /// <summary>
    /// Пункт «Mark as DatabasePreset» — в контекстном меню ассетов (окно Project) и в меню инспектора ассета.
    /// Делает выделенные пресеты базы (<see cref="IRecordPreset"/>) Addressable и вешает на них лейбл базы данных
    /// из <c>DatabaseSettings</c>. Доступен, только когда активен Addressables-драйвер базы и лейбл задан.
    /// Ассет, уже лежащий в Addressables, остаётся в своей группе; новый попадает в группу по умолчанию.
    /// </summary>
    public static class MarkAsDatabasePresetMenu
    {
        private const string ItemName = "Mark as DatabasePreset";
        private const string AssetsMenuPath = "Assets/" + ItemName;
        private const string InspectorMenuPath = "CONTEXT/ScriptableObject/" + ItemName;

        /// <summary>Окно Project: пометить все выделенные пресеты базы.</summary>
        [MenuItem(AssetsMenuPath, false, 2000)]
        private static void MarkSelected() => Mark(Selection.objects.Where(o => o is IRecordPreset));

        [MenuItem(AssetsMenuPath, true)]
        private static bool CanMarkSelected() => IsAvailable() && Selection.objects.Any(o => o is IRecordPreset);

        /// <summary>Меню инспектора ассета: пометить этот пресет базы.</summary>
        [MenuItem(InspectorMenuPath)]
        private static void MarkContext(MenuCommand command) => Mark(new[] { command.context });

        [MenuItem(InspectorMenuPath, true)]
        private static bool CanMarkContext(MenuCommand command) =>
            IsAvailable() && command.context is IRecordPreset && EditorUtility.IsPersistent(command.context);

        /// <summary>Активен Addressables-драйвер базы, и в настройках задан лейбл базы.</summary>
        private static bool IsAvailable() =>
            Database.GetDriver() is DatabaseDriver && !string.IsNullOrEmpty(GetLabel());

        /// <summary>Лейбл базы данных из <c>DatabaseSettings</c> (первый из списка); null — не задан.</summary>
        private static string GetLabel()
        {
            var labels = Settings.Data().DatabaseLabels;
            return labels is { Length: > 0 } ? labels[0] : null;
        }

        /// <summary>Сделать ассеты Addressable и повесить лейбл базы.</summary>
        private static void Mark(IEnumerable<Object> assets)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[DatabaseDriver] Addressable Asset Settings не созданы — пометить пресет нельзя.");
                return;
            }

            var label = GetLabel();
            foreach (var asset in assets)
            {
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
                if (string.IsNullOrEmpty(guid))
                    continue;

                var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                entry.SetLabel(label, true, true);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif

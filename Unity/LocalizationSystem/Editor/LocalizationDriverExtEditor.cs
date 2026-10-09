#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Unity.FileSystem.Bus;
using Vortex.Unity.LocalizationSystem.Presets;

namespace Vortex.Unity.LocalizationSystem
{
    public partial class LocalizationDriver
    {
        private static bool _isSet;

        [InitializeOnLoadMethod]
        private static void EditorRegister() => Defer();

        /// <summary>
        /// Отложить регистрацию до устойчивого состояния редактора. <c>InitializeOnLoadMethod</c>
        /// выполняется в середине загрузки домена, а при смене платформы следом идёт массовый
        /// реимпорт: выборка ассетов в этот момент неполна, существующий пресет посчитался бы
        /// отсутствующим и был бы затёрт пустым. Ждём, пока AssetDatabase перестанет обновляться,
        /// перевешивая вызов на следующий тик.
        ///
        /// В Play Mode (и на входе в него) редакторская регистрация переносится на возврат в Edit Mode:
        /// индекс в игре грузит рантайм-<see cref="RunAsync"/>. Иначе отложенная загрузка индекса
        /// срабатывает посреди его загрузки (тот уступает кадр каждые 20 записей), очищает и заполняет
        /// общий индекс, а рантайм затем дописывает хвост повторно — дубли ключей в логе.
        /// Перенос, а не отмена: на выходе из Play Mode домен не перезагружается, и без него
        /// редакторская регистрация не случилась бы до следующей перекомпиляции.
        /// </summary>
        private static void Defer() => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= DeferUntilEditMode;
                EditorApplication.playModeStateChanged += DeferUntilEditMode;
                return;
            }

            if (EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                Defer();
                return;
            }

            _isSet = false;
            if (!Localization.SetDriver(Instance))
            {
                Dispose();
                return;
            }

            FileBus.CreateFolders($"{Application.dataPath}/Resources/{Path}");
            if (FindPreset() == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LocalizationPreset>(),
                    $"Assets/Resources/{Path}/LocalizationData.asset");
                Debug.Log("Create new settings preset LocalizationData");
            }

            _isSet = true;
            Instance.LoadData();
        };

        private static void DeferUntilEditMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= DeferUntilEditMode;
            Defer();
        }

        /// <summary>
        /// Поиск пресета по всему проекту, а не по <c>Resources/{Path}</c>: ассет могли перенести в
        /// другую папку, и загрузка по фиксированному пути его бы не нашла — рядом лёг бы дубликат.
        /// Фильтр <c>t:</c> захватит и одноимённый класс Nani-локализации, но <c>LoadAssetAtPath</c>
        /// вернёт для него <c>null</c>: классы не связаны наследованием.
        /// </summary>
        private static LocalizationPreset FindPreset() =>
            AssetDatabase.FindAssets($"t:{nameof(LocalizationPreset)}")
                .Select(guid => AssetDatabase.LoadAssetAtPath<LocalizationPreset>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(x => x != null);

        [MenuItem("Tools/Vortex/Localization/Load data", false, 1)]
        private static async void LoadLocalizationData()
        {
            var resources = Resources.LoadAll<LocalizationPreset>(Path);
            if (resources == null || resources.Length == 0)
            {
                Debug.LogError("[Localization] Localization Preset not found]");
                return;
            }

            _resource = resources[0];
            await _resource.LoadData();
            RefreshIndex();
        }

        private void LoadData()
        {
            var resources = Resources.LoadAll<LocalizationPreset>(Path);
            if (resources == null || resources.Length == 0)
            {
                Debug.LogError("Localization Data asset not found");
                return;
            }

            _resource = resources[0];
            RefreshIndex();
        }

        [MenuItem("Tools/Vortex/Localization/Update index", false, 1)]
        private static void RefreshIndex()
        {
            if (_localeData == null)
                return;
            _localeData.Clear();
            foreach (var data in _resource.localeData)
            {
                var translateData = data.Texts.First(x => x.Language == Localization.GetCurrentLanguage());
                _localeData.AddNew(data.Key, translateData.Text);
            }
        }

        [MenuItem("Tools/Vortex/Localization/Update index", true)]
        [MenuItem("Tools/Vortex/Localization/Load data", true)]
        public static bool CheckDriver() => _isSet;
    }
}
#endif
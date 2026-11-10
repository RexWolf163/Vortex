using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif
using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Attribute;
using Vortex.Unity.CoreAssetsSystem;

namespace Vortex.Sdk.SdkSettingsSystem
{
    /// <summary>
    /// Конфиг подключения пакетов SDK
    ///
    /// Берет партиалы из пакетов и проверяет наличие указанных через атрибут DefineSymbol ключей в
    /// настройках проекта. Обновляет данные дефайнов согласно состояния полей в этом ассете  
    /// </summary>
    [Serializable]
    public partial class SdkSettings : ScriptableObject, ICoreAsset
    {
#if UNITY_EDITOR
        /// <summary>
        /// Признак того, что ассет уже синхронизировался с PlayerSettings хотя бы раз.
        /// При первом запуске (свеже-созданный ассет) выставляется через ReloadStates,
        /// чтобы тогглы соответствовали уже существующим defines, а не затёрли их дефолтами.
        /// </summary>
        [SerializeField, HideInInspector] private bool _initialized;

        /// <summary>
        /// Проверка соответствия дефайнов. <c>false</c> — синхронизация не выполнена: неизвестная платформа или
        /// ошибка записи PlayerSettings (в лог). Совпадение без записи — <c>true</c>.
        /// </summary>
        private bool RefreshDefines()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (group == BuildTargetGroup.Unknown) return false;

            var defines = new Dictionary<string, bool>();
            var fields = GetFields();
            foreach (var fieldInfo in fields)
            {
                if (fieldInfo.FieldType != typeof(bool))
                    continue;
                var attr = fieldInfo.GetCustomAttribute<DefineSymbolAttribute>();
                if (attr == null) continue;
                if (!defines.TryAdd(attr.Define, (bool)fieldInfo.GetValue(this)))
                {
                    Debug.LogError(
                        $"[SdkSettings] Дубликат DefineSymbol \"{attr.Define}\" на поле \"{fieldInfo.Name}\" — пропущен.", this);
                }
            }

            var modifiedCount = 0;
            try
            {
                var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                var currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);

                // Безопасное разбиение и очистка для .NET Standard 2.0
                var definesSet = new HashSet<string>(
                    currentDefines
                        .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                );

                foreach (var (defineString, shouldEnable) in defines)
                {
                    if (defineString.IsNullOrWhitespace())
                        continue;

                    var wasChanged = false;
                    if (shouldEnable && definesSet.Add(defineString))
                        wasChanged = true;
                    else if (!shouldEnable && definesSet.Contains(defineString))
                    {
                        definesSet.Remove(defineString);
                        wasChanged = true;
                    }

                    if (wasChanged)
                    {
                        modifiedCount++;
                        Debug.Log(
                            $"[SdkSettings] {(shouldEnable ? "Добавлен" : "Удалён")} {defineString} для {group}");
                    }
                }

                if (modifiedCount <= 0) return true;

                var newDefines = string.Join(";", definesSet.OrderBy(x => x));
                var currentSorted = string.Join(";",
                    currentDefines
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .OrderBy(x => x));
                if (newDefines == currentSorted) return true;

                AssetDatabase.SaveAssets();
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SdkSettings] Ошибка при обработке {group} — {ex.Message}", this);
                return false;
            }
        }

        [Button, HorizontalGroup("btns")]
        private void ReloadStates()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (group == BuildTargetGroup.Unknown) return;

            var fields = GetFields();

            try
            {
                var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                var currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);

                var definesSet = new HashSet<string>(
                    currentDefines
                        .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                );

                // Запись через SerializedObject — Unity корректно регистрирует изменения,
                // SetDirty по рефлексии тут ненадёжен (в инспекторе видно, на диск не доходит).
                var so = new SerializedObject(this);
                so.Update();

                var initProp = so.FindProperty("_initialized");
                if (initProp != null) initProp.boolValue = true;

                foreach (var fieldInfo in fields)
                {
                    if (fieldInfo.FieldType != typeof(bool))
                        continue;
                    var attr = fieldInfo.GetCustomAttribute<DefineSymbolAttribute>();
                    if (attr == null) continue;

                    var prop = so.FindProperty(fieldInfo.Name);
                    if (prop != null)
                        prop.boolValue = definesSet.Contains(attr.Define);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PersistAsset();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SdkSettings] Ошибка при обработке {group} — {ex.Message}", this);
            }
        }

        /// <summary>
        /// Сохранение ассета на диск через SaveAssetIfDirty — точечнее общего SaveAssets
        /// и сам обновляет SourceAssetDB, без диспатча в импорт-воркеры.
        /// </summary>
        private void PersistAsset()
        {
            try
            {
                AssetDatabase.SaveAssetIfDirty(this);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SdkSettings] Ошибка при сохранении ассета — {ex.Message}");
            }
        }

        [Button, HorizontalGroup("btns")]
        private void ApplyChanges()
        {
            RefreshDefines();
        }

        private FieldInfo[] GetFields() =>
            GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);


        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorUserBuildSettings.activeBuildTargetChanged -= OnPlatformChanged;
            EditorUserBuildSettings.activeBuildTargetChanged += OnPlatformChanged;

            // delayCall — чтобы первый прогон случился ПОСЛЕ всех остальных
            // [InitializeOnLoadMethod] (в т.ч. CoreAssetsController, который и создаёт ассет).
            // Прямой вызов OnPlatformChanged() здесь race с порядком инициализации:
            // при старте на пустом проекте ассета может ещё не существовать.
            EditorApplication.delayCall += OnPlatformChanged;
        }

        /// <summary>
        /// Синхронизация ассета с PlayerSettings. Точки входа: перезагрузка домена, смена платформы и импорт самого
        /// ассета (<c>SdkSettingsImportWatcher</c>) — последний покрывает подмену файла (AssetSwap,
        /// git) без перезагрузки домена.
        /// </summary>
        internal static void OnPlatformChanged()
        {
            // Во время сборки или активного импорта/компиляции запись в ассет конфликтует
            // с AssetDatabase (mtime-рассинхрон → "Build asset version error"). Откладываем.
            if (BuildPipeline.isBuildingPlayer
                || EditorApplication.isUpdating
                || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += OnPlatformChanged;
                return;
            }

            var settings = FindActive();
            if (settings == null) return;

            if (!settings._initialized)
            {
                settings.ReloadStates(); // внутри выставит _initialized=true, поля из defines и сохранит
                return;
            }

            settings.RefreshDefines();
        }

        /// <summary>
        /// Немедленно привести дефайны PlayerSettings (текущая <c>selectedBuildTargetGroup</c>) к галочкам ассета — для
        /// скриптов сборки и CI: внутри одного <c>-executeMethod</c> отложенный <c>delayCall</c> не успел бы до
        /// <c>BuildPipeline.BuildPlayer</c>. Дефайны плеерная сборка берёт из PlayerSettings в момент сборки — отдельная
        /// перекомпиляция редактора не нужна.
        ///
        /// <c>false</c> (с ошибкой в лог): идёт сборка/импорт/компиляция, ассета нет или их несколько, ассет не
        /// инициализирован (первый прогон прочитал бы текущие дефайны в ассет, а не наоборот), запись не удалась.
        /// </summary>
        public static bool SyncDefinesNow()
        {
            if (BuildPipeline.isBuildingPlayer || EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                Debug.LogError("[SdkSettings] SyncDefinesNow: идёт сборка, импорт или компиляция — синхронизация отклонена.");
                return false;
            }

            var settings = FindActive();
            if (settings == null)
            {
                Debug.LogError("[SdkSettings] SyncDefinesNow: действующий ассет настроек не найден.");
                return false;
            }

            if (!settings._initialized)
            {
                Debug.LogError($"[SdkSettings] SyncDefinesNow: ассет {AssetDatabase.GetAssetPath(settings)} не " +
                               "инициализирован — галочки не отражают проект, синхронизация отклонена.", settings);
                return false;
            }

            return settings.RefreshDefines();
        }

        /// <summary>Действующий ассет настроек (вне Editor-папок). Несколько — ошибка в лог и <c>null</c>.</summary>
        private static SdkSettings FindActive()
        {
            var paths = FindActivePaths();
            if (paths.Count > 1)
            {
                Debug.LogError("[SdkSettings] Обнаружено несколько ассетов конфигурации вне Editor-папок! " +
                               $"Должен быть только один: {string.Join(", ", paths)}");
                return null;
            }

            return paths.Count == 0 ? null : AssetDatabase.LoadAssetAtPath<SdkSettings>(paths[0]);
        }

        /// <summary>
        /// Пути действующих ассетов настроек. Ассеты в Editor-папках не учитываются: это заготовки (напр. варианты
        /// AssetSwap), в сборку они не попадают и дефайнами не управляют.
        /// </summary>
        private static List<string> FindActivePaths()
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(SdkSettings)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsInEditorFolder(path))
                    paths.Add(path);
            }

            return paths;
        }

        /// <summary>Путь лежит внутри папки с именем <c>Editor</c> на любой глубине.</summary>
        internal static bool IsInEditorFolder(string path) =>
            path.StartsWith("Editor/") || path.Contains("/Editor/");
#endif
    }
}
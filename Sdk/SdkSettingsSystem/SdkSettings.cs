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
        /// Проверка соответствия дефайнов
        /// </summary>
        private void RefreshDefines()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (group == BuildTargetGroup.Unknown) return;

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
                        $"[SdkSettings] Дубликат DefineSymbol \"{attr.Define}\" на поле \"{fieldInfo.Name}\" — пропущен.");
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

                if (modifiedCount <= 0) return;

                var newDefines = string.Join(";", definesSet.OrderBy(x => x));
                var currentSorted = string.Join(";",
                    currentDefines
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .OrderBy(x => x));
                if (newDefines == currentSorted) return;

                AssetDatabase.SaveAssets();
                PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SdkSettings] Ошибка при обработке {group} — {ex.Message}");
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

                foreach (var fieldInfo in fields)
                {
                    if (fieldInfo.FieldType != typeof(bool))
                        continue;
                    var attr = fieldInfo.GetCustomAttribute<DefineSymbolAttribute>();
                    if (attr == null) continue;
                    fieldInfo.SetValue(this, definesSet.Contains(attr.Define));
                }

                EditorUtility.SetDirty(this);
                PersistAsset();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SdkSettings] Ошибка при обработке {group} — {ex.Message}");
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

        private static void OnPlatformChanged()
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

            var guids = AssetDatabase.FindAssets("t:SdkSettings");
            if (guids.Length > 1)
            {
                Debug.LogError("[SdkSettings] Обнаружено несколько ассетов конфигурации! Должен быть только один!");
                return;
            }

            if (guids.Length == 0) return;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var settings = AssetDatabase.LoadAssetAtPath<SdkSettings>(path);
            if (settings == null) return;

            if (!settings._initialized)
            {
                settings._initialized = true;
                settings.ReloadStates(); // внутри сделает SetDirty + PersistAsset — флаг попадёт в сохранение
                return;
            }

            settings.RefreshDefines();
        }
#endif
    }
}
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Vortex.Sdk.SdkSettingsSystem.Editor.Attribute;
using Vortex.Unity.CoreAssetsSystem;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Sdk.SdkSettingsSystem.Editor
{
    /// <summary>
    /// Конфиг подключения пакетов SDK
    ///
    /// Берет партиалы из пакетов и проверяет наличие указанных через атрибут DefineSymbol ключей в
    /// настройках проекта. Обновляет данные дефайнов согласно состояния полей в этом ассете  
    /// </summary>
    public partial class SdkSettings : SettingsPreset, ICoreAsset
    {
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
                defines.Add(attr.Define, (bool)fieldInfo.GetValue(this));
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

                AssetDatabase.SaveAssets();
                PlayerSettings.SetScriptingDefineSymbols(namedTarget,
                    string.Join(";", definesSet.OrderBy(x => x)));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SdkSettings] Ошибка при обработке {group} — {ex.Message}");
            }
        }

        [Button, HorizontalGroup("btns")]
        private void ApplyChanges()
        {
            RefreshDefines();
        }

        [Button, HorizontalGroup("btns")]
        private void ResetChanges()
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SdkSettings] Ошибка при обработке {group} — {ex.Message}");
            }

            try
            {
                AssetDatabase.SaveAssets();
            }
            catch (Exception)
            {
                //Ignore
            }
        }

        private FieldInfo[] GetFields() =>
            GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);


        private bool _isValidate;

        private void OnValidate()
        {
            if (_isValidate)
                return;
            try
            {
                _isValidate = true;
                ResetChanges();
            }
            catch (Exception)
            {
                _isValidate = false;
            }
        }
    }
}

#endif
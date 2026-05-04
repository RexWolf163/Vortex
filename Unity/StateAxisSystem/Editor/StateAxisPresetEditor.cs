using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Vortex.Core.StateAxisSystem.Abstractions;
using Vortex.Unity.StateAxisSystem.Presets;

namespace Vortex.Unity.StateAxisSystem.EditorTools
{
    /// <summary>
    /// Custom-инспектор для <see cref="StateAxisPreset"/>: live-чистка точек в полях,
    /// кнопки Save (сгенерировать <c>.cs</c>) и Load (синхронизировать ассет с существующим классом).
    /// </summary>
    [CustomEditor(typeof(StateAxisPreset))]
    public class StateAxisPresetEditor : Editor
    {
        private SerializedProperty _axisNameProp;
        private SerializedProperty _namespaceProp;
        private SerializedProperty _keysProp;
        private SerializedProperty _lastPathProp;

        private void OnEnable()
        {
            _axisNameProp = serializedObject.FindProperty("axisName");
            _namespaceProp = serializedObject.FindProperty("targetNamespace");
            _keysProp = serializedObject.FindProperty("keys");
            _lastPathProp = serializedObject.FindProperty("lastGeneratedPath");
        }

        public override void OnInspectorGUI()
        {
            var preset = (StateAxisPreset)target;

            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_axisNameProp);
            EditorGUILayout.PropertyField(_namespaceProp);
            EditorGUILayout.PropertyField(_keysProp, true);
            if (EditorGUI.EndChangeCheck())
                StripDotsLive();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Generated path",
                    string.IsNullOrEmpty(_lastPathProp.stringValue) ? "(not generated yet)" : _lastPathProp.stringValue);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawValidationBox(preset);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save", GUILayout.Height(28)))
                    Save(preset);
                if (GUILayout.Button("Load", GUILayout.Height(28)))
                    Load(preset);
            }
        }

        private void StripDotsLive()
        {
            if (_axisNameProp.stringValue.Contains('.'))
                _axisNameProp.stringValue = StateAxisCodeGenerator.StripDots(_axisNameProp.stringValue);

            for (int i = 0; i < _keysProp.arraySize; i++)
            {
                var elem = _keysProp.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(elem.stringValue) && elem.stringValue.Contains('.'))
                    elem.stringValue = StateAxisCodeGenerator.StripDots(elem.stringValue);
            }
        }

        private void DrawValidationBox(StateAxisPreset preset)
        {
            var errors = Validate(preset);
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Готов к Save.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Warning);
        }

        private List<string> Validate(StateAxisPreset preset)
        {
            var errors = new List<string>();

            if (!StateAxisCodeGenerator.IsValidIdentifier(preset.AxisName))
                errors.Add($"Axis Name '{preset.AxisName}' — невалидный C#-идентификатор.");

            if (!StateAxisCodeGenerator.IsValidNamespace(preset.TargetNamespace))
                errors.Add($"Namespace '{preset.TargetNamespace}' — невалиден.");

            if (preset.Keys.Count == 0)
                errors.Add("Keys: список пуст.");

            var seen = new HashSet<string>();
            for (int i = 0; i < preset.Keys.Count; i++)
            {
                var k = preset.Keys[i];
                if (!StateAxisCodeGenerator.IsValidIdentifier(k))
                    errors.Add($"Keys[{i}] '{k}' — невалидный C#-идентификатор.");
                else if (!seen.Add(k))
                    errors.Add($"Keys: дубликат '{k}'.");
            }

            return errors;
        }

        private void Save(StateAxisPreset preset)
        {
            var errors = Validate(preset);
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("StateAxis — ошибки валидации",
                    string.Join("\n", errors), "OK");
                return;
            }

            var presetPath = AssetDatabase.GetAssetPath(preset);
            if (string.IsNullOrEmpty(presetPath))
            {
                EditorUtility.DisplayDialog("StateAxis", "Пресет не сохранён в проекте.", "OK");
                return;
            }

            var folder = Path.GetDirectoryName(presetPath)?.Replace('\\', '/');
            var targetPath = $"{folder}/{preset.AxisName}.cs";

            // Удалить старый сгенерированный файл при ренейме.
            var oldPath = preset.LastGeneratedPath;
            if (!string.IsNullOrEmpty(oldPath) && oldPath != targetPath && File.Exists(oldPath))
                AssetDatabase.DeleteAsset(oldPath);

            var content = StateAxisCodeGenerator.Generate(preset.AxisName, preset.TargetNamespace, preset.Keys);

            try
            {
                File.WriteAllText(targetPath, content);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("StateAxis — ошибка записи", e.Message, "OK");
                return;
            }

            var so = new SerializedObject(preset);
            so.FindProperty("lastGeneratedPath").stringValue = targetPath;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StateAxis] Сгенерирован {targetPath}", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath));
        }

        private void Load(StateAxisPreset preset)
        {
            if (!StateAxisCodeGenerator.IsValidIdentifier(preset.AxisName) ||
                !StateAxisCodeGenerator.IsValidNamespace(preset.TargetNamespace))
            {
                EditorUtility.DisplayDialog("StateAxis", "Сначала задайте валидные Axis Name и Namespace.", "OK");
                return;
            }

            var fullName = $"{preset.TargetNamespace}.{preset.AxisName}";
            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(fullName);
                if (type != null) break;
            }

            if (type == null)
            {
                EditorUtility.DisplayDialog("StateAxis",
                    $"Тип '{fullName}' не найден. Сначала Save для создания .cs.", "OK");
                return;
            }

            if (!typeof(StateAxis).IsAssignableFrom(type))
            {
                EditorUtility.DisplayDialog("StateAxis",
                    $"Тип '{fullName}' не наследует StateAxis.", "OK");
                return;
            }

            try
            {
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("StateAxis",
                    $"Ошибка инициализации '{fullName}': {e.Message}", "OK");
                return;
            }

            var all = StateAxis.GetAll(type);
            var keys = all.Select(s => s.Key).ToArray();

            var so = new SerializedObject(preset);
            var keysProp = so.FindProperty("keys");
            keysProp.arraySize = keys.Length;
            for (int i = 0; i < keys.Length; i++)
                keysProp.GetArrayElementAtIndex(i).stringValue = keys[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();

            Debug.Log($"[StateAxis] Загружено {keys.Length} ключей из {fullName}", preset);
        }
    }
}

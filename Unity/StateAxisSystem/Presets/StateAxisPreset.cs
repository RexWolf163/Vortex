using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Vortex.Core.StateAxisSystem.Abstractions;
using Vortex.Unity.StateAxisSystem.Editor;

namespace Vortex.Unity.StateAxisSystem.Presets
{
    /// <summary>
    /// Парный ассет генерируемого StateAxis-класса.
    /// Хранит имя оси, целевой namespace и упорядоченный список ключей
    ///
    /// Workflow:
    /// 1. Создать ассет через <c>Create → Vortex → StateAxis Preset</c>.
    /// 2. Заполнить Axis Name, Target Namespace, Keys.
    /// 3. Нажать Save в инспекторе → генерируется <c>{папка_ассета}/{AxisName}.cs</c>.
    ///    При смене Axis Name старый <c>.cs</c> удаляется.
    /// 4. Load — обратное действие: считать ключи из существующего сгенерированного класса
    ///    и записать в SO (для синхронизации после слияния веток).
    /// </summary>
    [CreateAssetMenu(fileName = "StateAxisPreset", menuName = "Vortex/StateAxis Preset")]
    [Serializable]
    public class StateAxisPreset : ScriptableObject
    {
        [SerializeField, InfoBox("Имя сгенерированного класса. PascalCase, валидный C#-идентификатор, без точек.")]
        [OnValueChanged("OnNameChanged")]
        private string axisName;

        [SerializeField, InfoBox("Namespace сгенерированного класса.")]
        private string targetNamespace;

        [SerializeField, InfoBox("Упорядоченный список ключей оси. Каждый ключ — валидный C#-идентификатор без точек.")]
        private string[] keys = new string[0];

        [SerializeField, HideInInspector] private string lastGeneratedPath;

        public string AxisName => axisName;
        public string TargetNamespace => targetNamespace;
        public IReadOnlyList<string> Keys => keys;
        public string LastGeneratedPath => lastGeneratedPath;

        private bool _isValidated;

#if UNITY_EDITOR
        [Button]
        private void Save()
        {
            var errors = Validate();
            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("StateAxis — ошибки валидации",
                    string.Join("\n", errors), "OK");
                return;
            }

            var presetPath = AssetDatabase.GetAssetPath(this);
            if (string.IsNullOrEmpty(presetPath))
            {
                EditorUtility.DisplayDialog("StateAxis", "Пресет не сохранён в проекте.", "OK");
                return;
            }

            var folder = Path.GetDirectoryName(presetPath)?.Replace('\\', '/');
            var targetPath = $"{folder}/{AxisName}.cs";

            var content = StateAxisCodeGenerator.Generate(AxisName, TargetNamespace, Keys);

            try
            {
                File.WriteAllText(targetPath, content);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("StateAxis — ошибка записи", e.Message, "OK");
                return;
            }

            var so = new SerializedObject(this);
            so.FindProperty("lastGeneratedPath").stringValue = targetPath;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[StateAxis] Сгенерирован {targetPath}");

            Validate();
        }

        [Button]
        private void Load()
        {
            if (!StateAxisCodeGenerator.IsValidIdentifier(AxisName) ||
                !StateAxisCodeGenerator.IsValidNamespace(TargetNamespace))
            {
                EditorUtility.DisplayDialog("StateAxis", "Сначала задайте валидные Axis Name и Namespace.", "OK");
                return;
            }

            var fullName = $"{TargetNamespace}.{AxisName}";
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

            var so = new SerializedObject(this);
            var keysProp = so.FindProperty("keys");
            keysProp.arraySize = keys.Length;
            for (int i = 0; i < keys.Length; i++)
                keysProp.GetArrayElementAtIndex(i).stringValue = keys[i];
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();

            Debug.Log($"[StateAxis] Загружено {keys.Length} ключей из {fullName}", this);
        }

        private List<string> Validate()
        {
            var errors = new List<string>();

            if (!StateAxisCodeGenerator.IsValidIdentifier(AxisName))
                errors.Add($"Axis Name '{AxisName}' — невалидный C#-идентификатор.");

            if (!StateAxisCodeGenerator.IsValidNamespace(TargetNamespace))
                errors.Add($"Namespace '{TargetNamespace}' — невалиден.");

            if (Keys.Count == 0)
                errors.Add("Keys: список пуст.");

            var seen = new HashSet<string>();
            for (int i = 0; i < Keys.Count; i++)
            {
                var k = Keys[i];
                if (!StateAxisCodeGenerator.IsValidIdentifier(k))
                    errors.Add($"Keys[{i}] '{k}' — невалидный C#-идентификатор.");
                else if (!seen.Add(k))
                    errors.Add($"Keys: дубликат '{k}'.");
            }

            return errors;
        }

        private void CheckLost()
        {
            var presetGuids = AssetDatabase.FindAssets("t:StateAxisPreset");
            var list = new List<string>();
            foreach (var guid in presetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<StateAxisPreset>(assetPath);
                list.Add($"{preset.TargetNamespace}.{preset.AxisName}");
            }

            var ass = AppDomain.CurrentDomain.GetAssemblies();
            var types = ass.SelectMany(s => s.GetTypes()).Where(p => typeof(StateAxis).IsAssignableFrom(p));
            foreach (var type in types)
            {
                if (list.Contains(type.FullName)) continue;
                Delete(type.FullName);
            }
        }

        private static void Delete(string className)
        {
            // Находим все ассеты с типом MonoScript и нужным именем
            string[] guids = AssetDatabase.FindAssets($"{className} t:StateAxis");

            foreach (string guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                // Проверяем, что класс действительно называется ScriptName
                if (script != null && script.GetClass()?.FullName == className)
                {
                    AssetDatabase.DeleteAsset(path);
                    Debug.Log($"Удалён: {path}");
                }
            }

            AssetDatabase.Refresh();
        }

        private void OnValidate()
        {
            _isValidated = StateAxisValidator.ValidatePreset(this);
        }

        private void OnNameChanged()
        {
            /*
            if (Validate().Count != 0)
                return;
            */
            var number = 0;
            if (AxisName == string.Empty) return;

            while (true)
            {
                var assetPath = AssetDatabase.GetAssetPath(this);
                var result =
                    AssetDatabase.RenameAsset(assetPath, $"{name}{(number > 0 ? $" ({number})" : "")}");
                if (result != "")
                {
                    number++;
                    continue;
                }

                AssetDatabase.SaveAssets();
                break;
            }

            if (number > 0 && AxisName != string.Empty)
                Debug.LogError($"[DbRecord] Name {name} for records already exists!");
        }
#endif
    }
}
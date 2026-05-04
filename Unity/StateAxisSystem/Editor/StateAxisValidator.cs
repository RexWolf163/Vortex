using System;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Vortex.Core.StateAxisSystem.Abstractions;
using Vortex.Unity.StateAxisSystem.Presets;

namespace Vortex.Unity.StateAxisSystem.EditorTools
{
    /// <summary>
    /// Editor-валидатор: при входе в Play Mode проверяет, что для каждого
    /// <see cref="StateAxisPreset"/> в проекте существует сгенерированный класс
    /// и его набор ключей совпадает с пресетом. Рассинхрон → <c>Debug.LogError</c>
    /// с указанием пресета и расхождений.
    ///
    /// Не блокирует вход в Play Mode — это решает разработчик.
    /// </summary>
    [InitializeOnLoad]
    internal static class StateAxisValidator
    {
        static StateAxisValidator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            ValidateAll();
        }

        [MenuItem("Tools/Vortex/StateAxis/Validate all presets")]
        private static void ValidateAll()
        {
            var presetGuids = AssetDatabase.FindAssets($"t:{nameof(StateAxisPreset)}");
            int errorCount = 0;

            foreach (var guid in presetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<StateAxisPreset>(path);
                if (preset == null) continue;

                if (!ValidatePreset(preset))
                    errorCount++;
            }

            if (errorCount == 0)
                Debug.Log($"[StateAxis] Валидация: {presetGuids.Length} пресет(ов) проверено, ошибок не найдено.");
        }

        private static bool ValidatePreset(StateAxisPreset preset)
        {
            if (string.IsNullOrEmpty(preset.AxisName) || string.IsNullOrEmpty(preset.TargetNamespace))
            {
                Debug.LogError($"[StateAxis] Пресет '{preset.name}': не заполнены AxisName или Namespace.", preset);
                return false;
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
                Debug.LogError($"[StateAxis] Пресет '{preset.name}' ссылается на тип '{fullName}', " +
                    $"которого нет в сборках. Откройте пресет и нажмите Save.", preset);
                return false;
            }

            if (!typeof(StateAxis).IsAssignableFrom(type))
            {
                Debug.LogError($"[StateAxis] Тип '{fullName}' не наследует StateAxis. Несогласованный пресет.", preset);
                return false;
            }

            try
            {
                RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StateAxis] Не удалось инициализировать '{fullName}': {e.Message}", preset);
                return false;
            }

            var actualKeys = StateAxis.GetAll(type).Select(s => s.Key).ToHashSet();
            var presetKeys = preset.Keys.Where(k => !string.IsNullOrEmpty(k)).ToHashSet();

            if (!actualKeys.SetEquals(presetKeys))
            {
                var onlyInPreset = string.Join(", ", presetKeys.Except(actualKeys));
                var onlyInClass = string.Join(", ", actualKeys.Except(presetKeys));
                if (string.IsNullOrEmpty(onlyInPreset)) onlyInPreset = "—";
                if (string.IsNullOrEmpty(onlyInClass)) onlyInClass = "—";

                Debug.LogError(
                    $"[StateAxis] Пресет '{preset.name}' рассинхронизирован с '{fullName}'.\n" +
                    $"  Только в пресете: {onlyInPreset}\n" +
                    $"  Только в классе:  {onlyInClass}\n" +
                    $"  Решение: откройте пресет → Save (если правильна модель пресета) " +
                    $"или Load (если правильна модель класса).", preset);
                return false;
            }

            return true;
        }
    }
}

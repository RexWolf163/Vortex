using System.IO;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.StateAxisSystem.Presets;

namespace Vortex.Unity.StateAxisSystem.EditorTools
{
    /// <summary>
    /// При удалении ассета <see cref="StateAxisPreset"/> удаляет парный
    /// сгенерированный <c>.cs</c>-файл, путь к которому хранится в
    /// <see cref="StateAxisPreset.LastGeneratedPath"/>.
    /// </summary>
    public class StateAxisAssetPostprocessor : AssetPostprocessor
    {
        private static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions options)
        {
            var preset = AssetDatabase.LoadAssetAtPath<StateAxisPreset>(assetPath);
            if (preset == null) return AssetDeleteResult.DidNotDelete;

            var generated = preset.LastGeneratedPath;
            if (string.IsNullOrEmpty(generated)) return AssetDeleteResult.DidNotDelete;
            if (!File.Exists(generated)) return AssetDeleteResult.DidNotDelete;

            // DeleteAsset отчищает и .meta. Делаем это до того как Unity завершит удаление пресета.
            AssetDatabase.DeleteAsset(generated);
            Debug.Log($"[StateAxis] Удалён парный файл {generated} (пресет '{preset.name}' удаляется).");

            return AssetDeleteResult.DidNotDelete;
        }
    }
}

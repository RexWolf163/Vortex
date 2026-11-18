using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.StateGraphSystem.Presets;

namespace Vortex.Unity.StateGraphSystem.Editor
{
    /// <summary>
    /// Раскладка узлов графа для визуализатора — редакторские данные, отдельно от пресета БД
    /// (пресет хранит только структуру). Лежит рядом с пресетом под именем «{пресет} Layout»;
    /// с пресетом связана GUID-ом его ассета, поэтому переименование пресета связь не рвёт.
    /// Создаётся при первой записи позиции
    /// </summary>
    public class StateGraphLayout : ScriptableObject
    {
        [Serializable]
        private class NodePosition
        {
            public string id;
            public Vector2 position;
        }

        [SerializeField] private string presetAssetGuid;
        [SerializeField] private List<NodePosition> positions = new();

        public bool TryGetPosition(string nodeId, out Vector2 position)
        {
            foreach (var entry in positions)
                if (entry.id == nodeId)
                {
                    position = entry.position;
                    return true;
                }

            position = default;
            return false;
        }

        public void SetPosition(string nodeId, Vector2 position)
        {
            foreach (var entry in positions)
                if (entry.id == nodeId)
                {
                    entry.position = position;
                    return;
                }

            positions.Add(new NodePosition { id = nodeId, position = position });
        }

        public void RemovePosition(string nodeId) => positions.RemoveAll(entry => entry.id == nodeId);

        /// <summary>Раскладка пресета (null — ещё не создана)</summary>
        public static StateGraphLayout Find(StateGraphPreset preset)
        {
            var presetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preset));
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(StateGraphLayout)}"))
            {
                var layout = AssetDatabase.LoadAssetAtPath<StateGraphLayout>(AssetDatabase.GUIDToAssetPath(guid));
                if (layout != null && layout.presetAssetGuid == presetGuid)
                    return layout;
            }

            return null;
        }

        /// <summary>Создать раскладку рядом с пресетом</summary>
        public static StateGraphLayout Create(StateGraphPreset preset)
        {
            var presetPath = AssetDatabase.GetAssetPath(preset);
            var layout = CreateInstance<StateGraphLayout>();
            layout.presetAssetGuid = AssetDatabase.AssetPathToGUID(presetPath);
            var path = AssetDatabase.GenerateUniqueAssetPath(
                $"{Path.GetDirectoryName(presetPath)}/{Path.GetFileNameWithoutExtension(presetPath)} Layout.asset");
            AssetDatabase.CreateAsset(layout, path);
            return layout;
        }
    }
}

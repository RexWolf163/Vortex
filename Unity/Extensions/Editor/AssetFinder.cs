#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.EditorTools
{
    public static class AssetFinder
    {
        /// <summary>
        /// Находит все ассеты указанного типа в проекте.
        /// </summary>
        /// <typeparam name="T">ScriptableObject, MonoBehaviour, Material, Sprite и т.д.</typeparam>
        /// <param name="searchInFolders">Опциональные папки для сужения поиска (например "Assets/Configs")</param>
        /// <returns>Список найденных ассетов</returns>
        public static List<T> FindAssets<T>(params string[] searchInFolders) where T : Object
        {
            var filter = $"t:{typeof(T).Name}";
            var guids = searchInFolders.Length > 0
                ? AssetDatabase.FindAssets(filter, searchInFolders)
                : AssetDatabase.FindAssets(filter);

            var results = new List<T>(guids.Length);

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    results.Add(asset);
            }

            return results;
        }

        /// <summary>
        /// Находит первый ассет указанного типа или null.
        /// </summary>
        public static T FindAsset<T>(params string[] searchInFolders) where T : Object
        {
            var filter = $"t:{typeof(T).Name}";
            var guids = searchInFolders.Length > 0
                ? AssetDatabase.FindAssets(filter, searchInFolders)
                : AssetDatabase.FindAssets(filter);

            if (guids.Length == 0)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}
#endif
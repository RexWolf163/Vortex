using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.UI.UIBuilder
{
    /// <summary>
    /// Каталог примитивов модуля: префабы папки со всеми подпапками, отсортированные по пути. Подпись — путь
    /// относительно папки без расширения (<c>Special/Button MainMenu</c>), разделитель <c>/</c> даёт группы
    /// в выпадающих списках. Не кешируется: собирается при каждом обращении.
    /// </summary>
    internal static class UIBuilderCatalog
    {
        private const string PrefabExtension = ".prefab";

        /// <returns><c>null</c> — каталог собран; иначе текст ошибки (папка не найдена / пуста).</returns>
        internal static string Load(string folder, out string[] labels, out GameObject[] prefabs)
        {
            labels = new string[0];
            prefabs = new GameObject[0];

            folder = (folder ?? string.Empty).TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
                return $"Папка не найдена: {folder}";

            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0)
                return $"В папке нет префабов: {folder}";

            var prefix = folder.Length + 1;
            labels = paths.Select(p => p.Substring(prefix, p.Length - prefix - PrefabExtension.Length)).ToArray();
            prefabs = paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
            return null;
        }
    }
}

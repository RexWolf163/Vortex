#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using Vortex.Sdk.ContentTagsSystem.Settings;

namespace Vortex.Sdk.ContentTagsSystem.Model
{
    /// <summary>
    /// Источник выпадашек с ключами тегов для инспектора. Ключи собираются из всех ассетов настроек в
    /// проекте: свободный ввод тега — тихая поломка, опечатку видно только по пропавшему контенту.
    /// </summary>
    public static class ContentTagsCatalog
    {
        /// <summary>Ключи всех объявленных тегов. Выражение для <c>[ValueDropdown]</c>.</summary>
        public static IEnumerable<string> EditorKeys()
        {
            var declared = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(ContentSettings)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<ContentSettings>(path);
                if (settings == null)
                    continue;

                foreach (var tag in settings.Tags)
                    if (tag != null && !string.IsNullOrEmpty(tag.Key) && declared.Add(tag.Key))
                        yield return tag.Key;
            }
        }
    }
}
#endif

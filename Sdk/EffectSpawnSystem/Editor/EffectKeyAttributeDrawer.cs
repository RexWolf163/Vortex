#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Sdk.EffectSpawnSystem.Attributes;
using Vortex.Sdk.EffectSpawnSystem.Bus;
using Vortex.Sdk.EffectSpawnSystem.Catalog;

namespace Vortex.Sdk.EffectSpawnSystem.Editor
{
    /// <summary>
    /// Drawer для <see cref="EffectKeyAttribute"/>. Рисует popup со всеми доступными ключами.
    ///
    /// В Editor-mode (не Play) <see cref="EffectSpawn"/> может не иметь зарегистрированного каталога —
    /// drawer в этом случае собирает union всех ключей всех <see cref="EffectsCatalog"/>-ассетов проекта
    /// напрямую через <c>AssetDatabase</c>.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectKeyAttribute))]
    public class EffectKeyAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[EffectKey] only on string fields");
                return;
            }

            var keys = ResolveKeys();
            if (keys.Length == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                EditorGUI.HelpBox(position, "В проекте нет зарегистрированных эффектов.", MessageType.Warning);
                return;
            }

            var current = property.stringValue;
            var index = Array.IndexOf(keys, current);
            var hadValid = index >= 0;
            if (!hadValid) index = 0;

            var newIndex = EditorGUI.Popup(position, label.text, index, keys);
            if (newIndex != index || !hadValid)
                property.stringValue = keys[newIndex];
        }

        private static string[] ResolveKeys()
        {
            // В Play (или после явной RegisterCatalog) — берём из шины.
            var fromBus = EffectSpawn.AllKeys;
            if (fromBus.Count > 0) return fromBus.ToArray();

            // В Editor-mode без регистрации — собираем union из всех каталогов проекта.
            var union = new HashSet<string>();
            var guids = AssetDatabase.FindAssets($"t:{nameof(EffectsCatalog)}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var catalog = AssetDatabase.LoadAssetAtPath<EffectsCatalog>(path);
                if (catalog == null) continue;
                foreach (var k in catalog.Keys) union.Add(k);
            }
            var arr = union.ToArray();
            Array.Sort(arr, StringComparer.Ordinal);
            return arr;
        }
    }
}
#endif

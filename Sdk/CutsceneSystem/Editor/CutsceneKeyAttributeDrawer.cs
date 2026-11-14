#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Sdk.CutsceneSystem.Attributes;
using Vortex.Sdk.CutsceneSystem.Catalog;

namespace Vortex.Sdk.CutsceneSystem.Editor
{
    /// <summary>
    /// Drawer для <see cref="CutsceneKeyAttribute"/>: popup со всеми ключами катсцен. По образцу
    /// <c>EffectKeyAttributeDrawer</c>.
    ///
    /// Ключ опционален: первым пунктом идёт «[NONE]» со значением <c>""</c> — пустое поле не перезаписывается
    /// на первый доступный ключ.
    ///
    /// Источник ключей: в Play — зарегистрированный каталог (<c>CutsceneController.AllKeys</c>); в Editor-mode —
    /// union ключей всех <see cref="CutsceneCatalog"/>-ассетов проекта через <c>AssetDatabase</c>.
    ///
    /// Ключ, которого больше нет в каталоге, молча не перезаписывается: поле рисуется красным с пометкой
    /// «отсутствует», замена — только явным выбором в popup'е
    /// </summary>
    [CustomPropertyDrawer(typeof(CutsceneKeyAttribute))]
    public class CutsceneKeyAttributeDrawer : PropertyDrawer
    {
        private const string EmptyKey = "";
        private const string EmptyLabel = "[NONE]";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[CutsceneKey] only on string fields");
                return;
            }

            var keys = ResolveKeys();
            var current = property.stringValue ?? EmptyKey;
            var index = Array.IndexOf(keys, current);

            if (index < 0)
            {
                var half = position.width * 0.5f;
                var labelRect = new Rect(position.x, position.y, position.width - half, position.height);
                var popupRect = new Rect(position.x + position.width - half, position.y, half, position.height);

                var prevColor = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.55f);
                EditorGUI.LabelField(labelRect, label.text, $"⚠ {current} (отсутствует)");
                GUI.color = prevColor;

                EditorGUI.BeginChangeCheck();
                var pickerIndex = EditorGUI.Popup(popupRect, 0, BuildDisplayLabels(keys));
                if (EditorGUI.EndChangeCheck())
                    property.stringValue = keys[pickerIndex];
                return;
            }

            var newIndex = EditorGUI.Popup(position, label, index, BuildDisplayLabels(keys));
            if (newIndex != index)
                property.stringValue = keys[newIndex];
        }

        private static GUIContent[] BuildDisplayLabels(string[] keys)
        {
            var result = new GUIContent[keys.Length];
            for (var i = 0; i < keys.Length; i++)
                result[i] = new GUIContent(string.IsNullOrEmpty(keys[i]) ? EmptyLabel : keys[i]);
            return result;
        }

        private static string[] ResolveKeys()
        {
            var fromController = CutsceneController.AllKeys;
            IEnumerable<string> source;
            if (fromController.Count > 0)
            {
                source = fromController;
            }
            else
            {
                source = AssetDatabase.FindAssets($"t:{nameof(CutsceneCatalog)}")
                    .Select(g => AssetDatabase.LoadAssetAtPath<CutsceneCatalog>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(c => c != null)
                    .SelectMany(c => c.Keys);
            }

            var list = source
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct()
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            list.Insert(0, EmptyKey);
            return list.ToArray();
        }
    }
}
#endif

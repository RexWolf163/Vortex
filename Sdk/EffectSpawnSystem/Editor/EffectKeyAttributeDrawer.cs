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
    /// Ключ опционален: первой строкой popup'а идёт пункт «&lt;None&gt;» со значением <c>""</c>,
    /// который означает «эффект не задан». Без этого пункта drawer при первой отрисовке поля
    /// с пустым ключом автоматически перезаписывал бы его на первый ключ из массива —
    /// silent overwrite, особенно неприятный в Play-режиме на сериализованных префабах.
    ///
    /// Источник ключей:
    ///  • В Play (или после явного <c>EffectSpawn.RegisterCatalog</c>) — берём union из шины.
    ///  • В Editor-mode без регистрации — собираем union всех ключей всех
    ///    <see cref="EffectsCatalog"/>-ассетов проекта напрямую через <c>AssetDatabase</c>.
    /// Обе ветки симметричны: пустая опция добавляется одинаково.
    ///
    /// Невалидный ключ (значение из удалённого каталога / переименованного эффекта) не
    /// перезаписывается молча: drawer рисует поле красным с пометкой «отсутствует»,
    /// чтобы дизайнер заметил и осознанно выбрал замену из выпадашки.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectKeyAttribute))]
    public class EffectKeyAttributeDrawer : PropertyDrawer
    {
        private const string EmptyKey = "";
        private const string EmptyLabel = "[None]";

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
                // Каталоги вообще пусты — рисуем обычное поле + предупреждение.
                EditorGUI.PropertyField(position, property, label);
                EditorGUI.HelpBox(position, "В проекте нет зарегистрированных эффектов.", MessageType.Warning);
                return;
            }

            var current = property.stringValue ?? EmptyKey;
            var index = Array.IndexOf(keys, current);

            // Ключ задан, но в текущем каталоге его нет (удалили эффект, переименовали ассет,
            // изменили каталог и т. п.). Fail-loud: подсвечиваем красным и выпадашку рисуем
            // отдельно, чтобы дизайнер мог выбрать замену осознанно. Молчаливая перезапись
            // на keys[0] недопустима — теряется информация о том, что было задано раньше.
            if (index < 0 && !string.IsNullOrEmpty(current))
            {
                var half = position.width * 0.5f;
                var labelRect = new Rect(position.x, position.y, position.width - half, position.height);
                var popupRect = new Rect(position.x + position.width - half, position.y, half, position.height);

                var prevColor = GUI.color;
                GUI.color = new Color(1f, 0.55f, 0.55f);
                EditorGUI.LabelField(labelRect, label.text, $"⚠ {current} (отсутствует)");
                GUI.color = prevColor;

                var pickerIndex = EditorGUI.Popup(popupRect, 0, BuildDisplayLabels(keys));
                if (pickerIndex > 0 || (pickerIndex == 0 && keys[0] == EmptyKey))
                    property.stringValue = keys[pickerIndex];
                return;
            }

            // Ключ — либо валиден, либо пустой. Пустая строка соответствует EmptyKey в начале
            // массива, так что index = 0, hadValid = true, перезаписи не происходит.
            if (index < 0) index = 0;

            var displayLabels = BuildDisplayLabels(keys);
            var newIndex = EditorGUI.Popup(position, label, index, displayLabels);
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
            // В Play (или после явной RegisterCatalog) — берём union из шины.
            var fromBus = EffectSpawn.AllKeys;
            IEnumerable<string> source;
            if (fromBus.Count > 0)
            {
                source = fromBus;
            }
            else
            {
                // В Editor-mode без регистрации — union всех каталогов проекта.
                source = AssetDatabase.FindAssets($"t:{nameof(EffectsCatalog)}")
                    .Select(g => AssetDatabase.LoadAssetAtPath<EffectsCatalog>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(c => c != null)
                    .SelectMany(c => c.Keys);
            }

            var list = source
                .Where(k => !string.IsNullOrEmpty(k))
                .Distinct()
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();

            // Опциональность: пустой ключ всегда первый в списке, обе ветки симметричны.
            list.Insert(0, EmptyKey);
            return list.ToArray();
        }
    }
}
#endif
#if UNITY_EDITOR
using System;
using System.Linq;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Core.MaskSystem;

namespace Vortex.Unity.MaskSystem.Editor
{
    /// <summary>
    /// Odin-drawer для <see cref="MaskSelectorAttribute"/>: выпадающий список типов маски вместо стандартного
    /// пикера <c>[SerializeReference]</c> (в Odin нет выбора с фильтром), затем параметры маски, подсказка по правилам,
    /// подсветка ошибки шаблона и тестовая строка. Тестовая строка живёт в drawer'е и не сериализуется.
    /// </summary>
    public sealed class MaskSelectorAttributeDrawer : OdinAttributeDrawer<MaskSelectorAttribute>
    {
        private const string WildcardRules =
            "Шаблон со *. Совпадение — со всей строкой.\n" +
            "*  — ноль или более символов внутри сегмента\n" +
            "**  — ноль или более любых символов, включая разделитель\n" +
            "{имя}  — захват: один или более символов внутри сегмента\n" +
            "{*имя}  — захват хвоста: ноль или более любых символов; только последним\n" +
            "Литералы: {{ → {,  }} → },  \\* → *,  \\\\ → \\\n" +
            "Два захвата подряд без литерала между ними запрещены.\n" +
            "Пример: https://{host}.partner.com/v{ver}/**";

        private const string RegexRules =
            "Регулярное выражение .NET. Неявно привязано к границам строки целиком (включая альтернативы a|b).\n" +
            "(?<имя>…) — ключ по имени; неименованная группа — ключ-номер (\"1\", \"2\"…).\n" +
            "Группа 0 и группы, не участвовавшие в совпадении, в результат не входят.\n" +
            "Пример: api\\.partner\\.com/(?<ver>v\\d+)/.*";

        private Type[] _types;
        private GUIContent[] _options;
        private string _setupError;
        private string _testInput;

        protected override void Initialize()
        {
            var fieldType = Property.ValueEntry?.BaseValueType;
            if (fieldType == null || !typeof(Mask).IsAssignableFrom(fieldType))
            {
                _setupError = $"[MaskSelector] Поле '{Property.Name}' должно иметь тип Mask или его наследника.";
                return;
            }

            var coreAssembly = typeof(Mask).Assembly;
            _types = TypeCache.GetTypesDerivedFrom(fieldType)
                .Append(fieldType)
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition && t.IsSerializable
                            && t.Assembly != coreAssembly && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToArray();

            _options = new[] { new GUIContent("—") }
                .Concat(_types.Select(t => new GUIContent(ObjectNames.NicifyVariableName(t.Name), t.FullName)))
                .ToArray();
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            if (_setupError != null)
            {
                SirenixEditorGUI.ErrorMessageBox(_setupError);
                CallNextDrawer(label);
                return;
            }

            var mask = Property.ValueEntry.WeakSmartValue as Mask;
            var index = mask == null ? 0 : Array.IndexOf(_types, mask.GetType()) + 1;

            if (mask != null && index == 0)
                SirenixEditorGUI.WarningMessageBox(
                    $"Тип {mask.GetType().Name} не предлагается для выбора в инспекторе (реализация ядра без поддержки редактора).");

            var selected = EditorGUILayout.Popup(label ?? GUIContent.none, index, _options);
            if (selected != index)
            {
                Property.ValueEntry.WeakSmartValue = selected == 0 ? null : Activator.CreateInstance(_types[selected - 1]);
                return; // дочерние свойства пересоберутся на следующем проходе
            }

            if (mask == null)
                return;

            EditorGUI.indentLevel++;
            DrawParameters(mask);
            DrawValidationAndTest(mask);
            EditorGUI.indentLevel--;
        }

        private void DrawParameters(Mask mask)
        {
            var rules = mask switch
            {
                WildcardMask => WildcardRules,
                RegexMask => RegexRules,
                _ => null
            };

            foreach (var child in Property.Children)
            {
                if (rules != null && child.Name == nameof(Mask.pattern))
                    child.Draw(new GUIContent(child.NiceName, rules));
                else
                    child.Draw();
            }
        }

        private void DrawValidationAndTest(Mask mask)
        {
            MaskValidation validation;
            try
            {
                validation = mask.Validate();
            }
            catch (InvalidOperationException e)
            {
                // Маска без перевалидации в редакторе изменена после валидации — инвариант неизменности.
                SirenixEditorGUI.ErrorMessageBox(e.Message);
                return;
            }

            if (!validation.IsValid)
                SirenixEditorGUI.ErrorMessageBox(FormatError(mask.pattern, validation));

            _testInput = EditorGUILayout.TextField(new GUIContent("Тестовая строка",
                "Только для редактора, не сохраняется. Показывает результат сопоставления."), _testInput);

            if (string.IsNullOrEmpty(_testInput) || !validation.IsValid)
                return;

            var match = mask.Match(_testInput);
            if (!match.IsMatch)
            {
                SirenixEditorGUI.WarningMessageBox("Не совпало.");
                return;
            }

            var captures = match.Captures.Count == 0
                ? "без захватов"
                : string.Join("\n", match.Captures.Select(c => $"{c.Key} = {c.Value}"));
            SirenixEditorGUI.InfoMessageBox("Совпало: " + (match.Captures.Count == 0 ? captures : "\n" + captures));
        }

        private static string FormatError(string pattern, MaskValidation validation)
        {
            var position = validation.Position;
            if (string.IsNullOrEmpty(pattern) || position < 0 || position > pattern.Length)
                return validation.Message;

            return $"{validation.Message}\nПозиция {position}: {pattern.Substring(0, position)}▸{pattern.Substring(position)}";
        }
    }
}
#endif

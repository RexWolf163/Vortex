using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Naninovel;
using UnityEditor;
using UnityEngine;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor
{
    /// <summary>
    /// Вкладка «Значения»: переменные менеджера Naninovel с текущими значениями, областью и типом; поле нового значения
    /// и Set. Только Play Mode после инициализации движка. Новое значение читается по типу переменной: число — с точкой,
    /// логическое — <c>true</c>/<c>false</c>, строка — как есть; не разобралось — ошибка у переменной, значение не
    /// меняется. Поле, которое правят, не перезаписывается значением из движка до Set.
    /// </summary>
    internal sealed partial class CustomVariablesDashboardWindow
    {
        /// <summary>Правка значения переменной в поле окна</summary>
        private sealed class Edit
        {
            public string Text;

            /// <summary>Поле правили после последнего обновления — значение из движка его не перезаписывает</summary>
            public bool Dirty;

            /// <summary>Ошибка разбора последнего Set; нет — null</summary>
            public string Error;
        }

        private readonly Dictionary<string, Edit> _edits = new();
        private string _valuesFilter = string.Empty;
        private Vector2 _valuesScroll;

        /// <summary>Менеджер, на событие которого подписано окно; не подписано — null</summary>
        private ICustomVariableManager _subscribed;

        private void DrawValues()
        {
            var manager = GetManager();
            if (manager == null)
            {
                UnsubscribeValues();
                EditorGUILayout.HelpBox("Значения доступны только в Play Mode после инициализации Naninovel.",
                    MessageType.Info);
                return;
            }

            SubscribeValues(manager);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                _valuesFilter = EditorGUILayout.TextField(_valuesFilter, EditorStyles.toolbarSearchField);

            var variables = manager.Variables
                .Where(v => _valuesFilter.Length == 0 ||
                            v.Name.IndexOf(_valuesFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (variables.Count == 0)
            {
                EditorGUILayout.HelpBox("Переменных нет.", MessageType.Info);
                return;
            }

            _valuesScroll = EditorGUILayout.BeginScrollView(_valuesScroll);
            foreach (var variable in variables)
                DrawValue(manager, variable);
            EditorGUILayout.EndScrollView();
        }

        private void DrawValue(ICustomVariableManager manager, CustomVariable variable)
        {
            var current = Format(variable.Value);
            if (!_edits.TryGetValue(variable.Name, out var edit))
                _edits[variable.Name] = edit = new Edit { Text = current };
            else if (!edit.Dirty)
                edit.Text = current;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(variable.Name, GUILayout.MinWidth(160),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                GUILayout.Label($"{variable.Scope}, {variable.Value.Type}", EditorStyles.miniLabel, GUILayout.Width(110));
                GUILayout.Label(current, GUILayout.MinWidth(80));

                EditorGUI.BeginChangeCheck();
                edit.Text = EditorGUILayout.TextField(edit.Text, GUILayout.MinWidth(120));
                if (EditorGUI.EndChangeCheck())
                {
                    edit.Dirty = true;
                    edit.Error = null;
                }

                if (GUILayout.Button("Set", GUILayout.Width(40)))
                    Apply(manager, variable, edit);
            }

            if (edit.Error != null)
                EditorGUILayout.HelpBox(edit.Error, MessageType.Error);
        }

        /// <summary>Передать новое значение по типу переменной; не разобралось — ошибка, значение не меняется</summary>
        private static void Apply(ICustomVariableManager manager, CustomVariable variable, Edit edit)
        {
            if (!TryParse(edit.Text, variable.Value.Type, out var value))
            {
                edit.Error = variable.Value.Type == CustomVariableValueType.Numeric
                    ? $"'{edit.Text}' — не число (дробная часть — через точку)."
                    : $"'{edit.Text}' — не логическое значение (true / false).";
                return;
            }

            edit.Dirty = false;
            edit.Error = null;
            GUI.FocusControl(null);
            manager.SetVariableValue(variable.Name, value);
        }

        private static bool TryParse(string text, CustomVariableValueType type, out CustomVariableValue value)
        {
            value = default;
            switch (type)
            {
                case CustomVariableValueType.Numeric:
                    if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                        return false;
                    value = new CustomVariableValue(number);
                    return true;
                case CustomVariableValueType.Boolean:
                    if (!bool.TryParse(text?.Trim(), out var flag))
                        return false;
                    value = new CustomVariableValue(flag);
                    return true;
                default:
                    value = new CustomVariableValue(text ?? string.Empty);
                    return true;
            }
        }

        private void ClearEdits() => _edits.Clear();

        private void SubscribeValues(ICustomVariableManager manager)
        {
            if (ReferenceEquals(_subscribed, manager))
                return;
            UnsubscribeValues();
            _subscribed = manager;
            _subscribed.OnVariableUpdated += OnVariableUpdated;
        }

        private void UnsubscribeValues()
        {
            if (_subscribed == null)
                return;
            _subscribed.OnVariableUpdated -= OnVariableUpdated;
            _subscribed = null;
        }

        private void OnVariableUpdated(CustomVariableUpdatedArgs _) => Repaint();
    }
}

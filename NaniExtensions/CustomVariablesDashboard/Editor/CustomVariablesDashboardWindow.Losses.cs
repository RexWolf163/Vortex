using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor
{
    /// <summary>
    /// Вкладка «Потеряшки»: секции видов потерь (с учётом исключений), двойники по регистру, исключения без переменной и
    /// правка списка исключений. Всё вычисляется при показе — правка исключений применяется без пересборки.
    /// </summary>
    internal sealed partial class CustomVariablesDashboardWindow
    {
        private Vector2 _lossesScroll;

        private readonly HashSet<string> _collapsed = new();

        private SerializedObject _settings;

        private void DrawLosses()
        {
            var index = DrawBuildBar();
            if (index == null)
                return;

            EditorGUILayout.HelpBox($"Данные на момент сборки {index.BuiltAt:HH:mm:ss}. Пересоберите перед удалением " +
                                    "переменных из конфига." + (index.QuestsEnabled
                                        ? string.Empty
                                        : " Квесты Vortex отключены — потери считаются только по сценариям."),
                MessageType.Info);

            _lossesScroll = EditorGUILayout.BeginScrollView(_lossesScroll);

            foreach (var kind in LossAnalysis.Kinds)
            {
                var section = LossAnalysis.GetSection(index, kind);
                if (!Foldout(LossAnalysis.Title(kind), section.Count))
                    continue;
                foreach (var entry in section)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(entry.Name, EditorStyles.linkLabel))
                            Select(entry.Name);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Исключить", EditorStyles.miniButton, GUILayout.Width(80)))
                        {
                            DashboardSettings.instance.Exclude(entry.Name, kind);
                            _settings?.Update();
                        }
                    }
            }

            var twins = LossAnalysis.GetCaseTwins(index);
            if (Foldout("Двойники по регистру", twins.Count))
                foreach (var group in twins)
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        foreach (var entry in group)
                            if (GUILayout.Button(entry.Name, EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
                                Select(entry.Name);
                        GUILayout.FlexibleSpace();
                    }

            var stale = LossAnalysis.GetStaleExclusions(index);
            if (Foldout("Исключения без переменной", stale.Count))
                foreach (var exclusion in stale)
                    GUILayout.Label(string.IsNullOrEmpty(exclusion.comment)
                        ? exclusion.variableName
                        : $"{exclusion.variableName} — {exclusion.comment}");

            DrawExclusions();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>Список исключений из настроек проекта с правкой</summary>
        private void DrawExclusions()
        {
            var settings = DashboardSettings.instance;
            if (!Foldout("Исключения", settings.Exclusions.Count))
                return;

            _settings ??= new SerializedObject(settings);
            _settings.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_settings.FindProperty("exclusions"), GUIContent.none, true);
            if (EditorGUI.EndChangeCheck())
            {
                _settings.ApplyModifiedPropertiesWithoutUndo();
                settings.Save();
            }

            if (settings.Exclusions.Any(x => string.IsNullOrWhiteSpace(x.variableName)))
                EditorGUILayout.HelpBox("Есть исключения с пустым именем — они не действуют.", MessageType.Warning);
        }

        /// <summary>Заголовок сворачиваемой секции со счётчиком; true — секция развёрнута</summary>
        private bool Foldout(string title, int count)
        {
            EditorGUILayout.Space(4f);
            var expanded = !_collapsed.Contains(title);
            var now = EditorGUILayout.Foldout(expanded, $"{title} ({count})", true, EditorStyles.foldoutHeader);
            if (now != expanded)
            {
                if (now)
                    _collapsed.Remove(title);
                else
                    _collapsed.Add(title);
            }

            return now;
        }
    }
}

using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.NaniExtensions.NaniScriptLinks.Editor.Model;

namespace Vortex.NaniExtensions.NaniScriptLinks.Editor
{
    /// <summary>
    /// Вкладка «Исключения»: форма добавления (цель + обязательная причина) и список с правкой причины и снятием.
    /// Работает и без собранной карты; пометка «ненужное» — только при собранной.
    /// </summary>
    internal sealed partial class NaniScriptLinksWindow
    {
        private const string ReasonControl = "NaniScriptLinks.Reason";

        [SerializeField] private string newTarget = string.Empty;

        [SerializeField] private string newComment = string.Empty;

        private bool _focusReason;

        private Vector2 _exclusionsScroll;

        /// <summary>Перенести цель в форму добавления и перейти к причине</summary>
        private void PrepareExclusion(string target)
        {
            newTarget = target;
            newComment = string.Empty;
            tab = Tab.Exclusions;
            _focusReason = true;
            GUI.FocusControl(null);
        }

        private void DrawExclusions(LinkAnalysis analysis)
        {
            DrawAddForm();

            var settings = ScriptLinksSettings.instance;
            if (settings.Exclusions.Count == 0)
            {
                DrawEmpty("Исключений нет.");
                return;
            }

            _exclusionsScroll = EditorGUILayout.BeginScrollView(_exclusionsScroll);
            foreach (var exclusion in settings.Exclusions.ToList())
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(exclusion.target, GUILayout.Width(position.width * 0.35f));
                    var comment = EditorGUILayout.DelayedTextField(exclusion.comment);
                    if (comment != exclusion.comment)
                        settings.SetComment(exclusion, comment);

                    var stale = analysis?.StaleExclusions.FirstOrDefault(s => s.Exclusion == exclusion);
                    if (stale != null)
                        GUILayout.Label(new GUIContent("ненужное", stale.Message), EditorStyles.miniBoldLabel,
                            GUILayout.ExpandWidth(false));

                    if (GUILayout.Button("Снять", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        settings.Remove(exclusion);
                        GUIUtility.ExitGUI();
                    }
                }

            EditorGUILayout.EndScrollView();
        }

        private void DrawAddForm()
        {
            EditorGUILayout.HelpBox("Исключение убирает точки входа из «Сирот» и «Неиспользуемых меток»; битые ссылки не " +
                                    "исключаются. Цель — путь реестра: папка оканчивается на «/» (вместе с метками её " +
                                    "сценариев), сценарий (вместе с его метками) или «сценарий.метка». Причина обязательна.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                newTarget = EditorGUILayout.TextField(newTarget, GUILayout.Width(position.width * 0.35f));
                GUI.SetNextControlName(ReasonControl);
                newComment = EditorGUILayout.TextField(newComment);
                if (_focusReason)
                {
                    _focusReason = false;
                    EditorGUI.FocusTextInControl(ReasonControl);
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newTarget) ||
                                                   string.IsNullOrWhiteSpace(newComment)))
                    if (GUILayout.Button("Добавить", EditorStyles.miniButton, GUILayout.Width(70)) &&
                        ScriptLinksSettings.instance.Add(newTarget, newComment))
                    {
                        newTarget = string.Empty;
                        newComment = string.Empty;
                        GUI.FocusControl(null);
                    }
            }

            EditorGUILayout.Space();
        }
    }
}

#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Bus;

namespace Vortex.Unity.SaveSystem.Editor
{
    /// <summary>
    /// Окно глобального хранилища: <c>Tools/Vortex/Global Save</c>. Модули с текущими значениями, сброс модуля
    /// и сброс всех, переход к настройкам.
    ///
    /// Значения показываются строкой сериализатора — ровно тем, что уйдёт в контейнер. Данные есть только
    /// в Play Mode после загрузки хранилища.
    /// </summary>
    public class GlobalSaveWindow : EditorWindow
    {
        /// <summary>Пункт меню настроек живёт в сборке настроек — вызывается по пути, без ссылки на неё.</summary>
        private const string SettingsMenu = "Tools/Vortex/Configs/Save Settings";

        private readonly HashSet<string> _expanded = new();
        private Vector2 _scroll;

        [MenuItem("Tools/Vortex/Global Save")]
        private static void Open()
        {
            var window = GetWindow<GlobalSaveWindow>("Global Save");
            window.minSize = new Vector2(420f, 200f);
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            DrawToolbar();

            if (!GlobalSaveController.IsInit)
            {
                EditorGUILayout.HelpBox(
                    Application.isPlaying
                        ? "Глобальное хранилище ещё не загружено."
                        : "Модули доступны только в Play Mode — запусти игру.",
                    MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var pair in GlobalSaveController.Modules)
                DrawModule(pair.Key, pair.Value);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Настройки", EditorStyles.toolbarButton))
                    EditorApplication.ExecuteMenuItem(SettingsMenu);

                using (new EditorGUI.DisabledScope(!GlobalSaveController.IsInit))
                {
                    if (GUILayout.Button("Сбросить всё", EditorStyles.toolbarButton)
                        && EditorUtility.DisplayDialog("Global Save",
                            "Сбросить все модули к значениям по умолчанию и записать хранилище?", "Сбросить", "Отмена"))
                        GlobalSaveController.ResetAll();
                }
            }
        }

        private void DrawModule(string key, IGlobalData module)
        {
            var type = module.GetType();
            using (new EditorGUILayout.HorizontalScope())
            {
                var open = _expanded.Contains(key);
                var now = EditorGUILayout.Foldout(open, $"{key}  ({type.Name})", true);
                if (now != open)
                {
                    if (now)
                        _expanded.Add(key);
                    else
                        _expanded.Remove(key);
                }

                if (GUILayout.Button("Сбросить", GUILayout.Width(80f)))
                    GlobalSaveController.Reset(type);
            }

            if (!_expanded.Contains(key))
                return;

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextArea(module.SerializeProperties());
        }
    }
}
#endif

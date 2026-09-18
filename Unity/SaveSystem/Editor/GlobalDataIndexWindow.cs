#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Unity.EditorTools.DataTools;

namespace Vortex.Unity.SaveSystem.Editor
{
    /// <summary>
    /// Окно глобальных данных: <c>Tools/Vortex/SaveData/Global Index</c>.
    ///
    /// Вне Play Mode — индекс всех реализаций <see cref="IGlobalData"/> в проекте: ключ, тип, сборка, значения по
    /// умолчанию и проблемы, из-за которых хранилище модуль пропустит (нет публичного конструктора без параметров,
    /// пустой или повторяющийся ключ).
    ///
    /// В Play Mode — содержимое глобального хранилища с правкой на лету: изменённое свойство сразу фиксируется
    /// (<c>GlobalSaveController.Commit</c>) — запись и уведомление подписчиков как от кода модуля. Сброс модуля и всех.
    ///
    /// Список свойств и поля значений — общие с окном данных игры: <see cref="PocoInspector"/>.
    /// </summary>
    public class GlobalDataIndexWindow : EditorWindow
    {
        /// <summary>Пункт меню настроек живёт в сборке настроек — вызывается по пути, без ссылки на неё.</summary>
        private const string SettingsMenu = "Tools/Vortex/Configs/Save Settings";

        private sealed class ModuleInfo
        {
            public Type Type;
            public string Key;

            /// <summary>Экземпляр со значениями по умолчанию. <c>null</c> — создать не удалось.</summary>
            public object Defaults;

            /// <summary>Почему хранилище пропустит модуль. <c>null</c> — проблем нет.</summary>
            public string Problem;
        }

        private readonly HashSet<Type> _expanded = new();
        private List<ModuleInfo> _index;
        private Vector2 _scroll;

        [MenuItem("Tools/Vortex/SaveData/Global Index")]
        private static void Open()
        {
            var window = GetWindow<GlobalDataIndexWindow>("Global Data");
            window.minSize = new Vector2(460f, 240f);
        }

        // После перекомпиляции окно включается заново — индекс пересобирается
        private void OnEnable() => _index = null;

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (Application.isPlaying)
                DrawRuntime();
            else
                DrawEditMode();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(Application.isPlaying ? "Хранилище (Play Mode)" : "Модули проекта",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (!Application.isPlaying && GUILayout.Button("Обновить", EditorStyles.toolbarButton))
                    _index = null;

                if (GUILayout.Button("Настройки", EditorStyles.toolbarButton))
                    EditorApplication.ExecuteMenuItem(SettingsMenu);

                if (!Application.isPlaying)
                    return;

                using (new EditorGUI.DisabledScope(!GlobalSaveController.IsInit))
                {
                    if (GUILayout.Button("Сбросить всё", EditorStyles.toolbarButton)
                        && EditorUtility.DisplayDialog("Global Data",
                            "Сбросить все модули к значениям по умолчанию и записать хранилище?", "Сбросить", "Отмена"))
                        GlobalSaveController.ResetAll();
                }
            }
        }

        #region Edit mode

        private void DrawEditMode()
        {
            _index ??= BuildIndex();
            if (_index.Count == 0)
            {
                EditorGUILayout.HelpBox("В проекте нет реализаций IGlobalData.", MessageType.Info);
                return;
            }

            foreach (var info in _index)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var open = Foldout(info.Type, $"{info.Key ?? "—"}  ({info.Type.Name})");
                    if (info.Problem != null)
                        EditorGUILayout.HelpBox(info.Problem, MessageType.Error);
                    if (!open)
                        continue;

                    EditorGUILayout.LabelField("Тип", info.Type.FullName);
                    EditorGUILayout.LabelField("Сборка", info.Type.Assembly.GetName().Name);
                    if (info.Defaults == null)
                        continue;

                    EditorGUILayout.LabelField("Значения по умолчанию", EditorStyles.boldLabel);
                    using (new EditorGUI.DisabledScope(true))
                        foreach (var property in PocoInspector.Properties(info.Type))
                            PocoInspector.DrawValue(property, info.Defaults, out _);
                }
            }
        }

        /// <summary>
        /// Индекс реализаций по правилам хранилища: модуль создаётся через публичный конструктор без параметров,
        /// ключ — <see cref="IGlobalData.GetGlobalKey"/>; дубли ключа выбывают все.
        /// </summary>
        private static List<ModuleInfo> BuildIndex()
        {
            var list = new List<ModuleInfo>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IGlobalData>())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                var info = new ModuleInfo { Type = type };
                list.Add(info);

                if (type.ContainsGenericParameters)
                {
                    info.Problem = "Открытый generic-тип — хранилище его не создаст.";
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    info.Problem = "Нет публичного конструктора без параметров — хранилище модуль не найдёт.";
                    continue;
                }

                try
                {
                    var instance = (IGlobalData)Activator.CreateInstance(type);
                    info.Defaults = instance;
                    info.Key = instance.GetGlobalKey();
                    if (string.IsNullOrEmpty(info.Key))
                        info.Problem = "Пустой ключ — модуль не читается и не записывается.";
                }
                catch (Exception e)
                {
                    info.Problem = $"Ошибка создания: {e.Message}";
                }
            }

            foreach (var group in list.Where(i => !string.IsNullOrEmpty(i.Key)).GroupBy(i => i.Key)
                         .Where(g => g.Count() > 1))
            {
                var names = string.Join(", ", group.Select(i => i.Type.Name));
                foreach (var info in group)
                    info.Problem = $"Ключ «{group.Key}» повторяется ({names}) — ни один из модулей не читается и не записывается.";
            }

            return list.OrderBy(i => i.Key ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(i => i.Type.Name, StringComparer.Ordinal)
                .ToList();
        }

        #endregion

        #region Runtime

        private void DrawRuntime()
        {
            if (!GlobalSaveController.IsInit)
            {
                EditorGUILayout.HelpBox(
                    "Глобальное хранилище не загружено: загрузка ещё идёт, или для GlobalSaveController не выбран драйвер в DriverConfig.",
                    MessageType.Info);
                return;
            }

            var modules = GlobalSaveController.Modules;
            if (modules.Count == 0)
            {
                EditorGUILayout.HelpBox("В хранилище нет модулей.", MessageType.Info);
                return;
            }

            foreach (var pair in modules.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var module = pair.Value;
                var type = module.GetType();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool open;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        open = Foldout(type, $"{pair.Key}  ({type.Name})");
                        if (GUILayout.Button("Сбросить", GUILayout.Width(80f)))
                            GlobalSaveController.Reset(type);
                    }

                    if (!open)
                        continue;

                    foreach (var property in PocoInspector.Properties(type))
                    {
                        if (!PocoInspector.DrawValue(property, module, out var value))
                            continue;
                        property.SetValue(module, value);
                        GlobalSaveController.Commit(type);
                    }
                }
            }
        }

        #endregion

        #region Drawing

        private bool Foldout(Type type, string label)
        {
            var open = _expanded.Contains(type);
            var now = EditorGUILayout.Foldout(open, label, true);
            if (now == open)
                return now;

            if (now)
                _expanded.Add(type);
            else
                _expanded.Remove(type);
            return now;
        }

        #endregion
    }
}
#endif

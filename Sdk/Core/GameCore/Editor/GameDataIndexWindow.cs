#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Core.System.Enums;
using Vortex.Unity.EditorTools.DataTools;

namespace Vortex.Sdk.Core.GameCore.Editor
{
    /// <summary>
    /// Окно данных игры: <c>Tools/Vortex/SaveData/Game Index</c>.
    ///
    /// Вне Play Mode — индекс всех реализаций <see cref="GameModel.IGameData"/> в проекте: тип, сборка, значения по
    /// умолчанию и проблемы, из-за которых модель модуль пропустит (нет публичного конструктора без параметров,
    /// открытый generic-тип).
    ///
    /// В Play Mode — содержимое модели с правкой на лету: изменённое свойство сразу записывается и рассылается
    /// событие обновления данных (<c>GameController.CallUpdateEvent</c>), как от кода модуля. Сброс модуля и всех.
    /// В отличие от глобального хранилища, модель игры живёт только в сессии: сохраняется она вместе с сейвом.
    ///
    /// Список свойств и поля значений — общие с окном глобальных данных: <see cref="PocoInspector"/>.
    /// </summary>
    public class GameDataIndexWindow : EditorWindow
    {
        private sealed class ModuleInfo
        {
            public Type Type;

            /// <summary>Экземпляр со значениями по умолчанию. <c>null</c> — создать не удалось.</summary>
            public object Defaults;

            /// <summary>Почему модель пропустит модуль. <c>null</c> — проблем нет.</summary>
            public string Problem;
        }

        private readonly HashSet<Type> _expanded = new();
        private List<ModuleInfo> _index;
        private Vector2 _scroll;

        [MenuItem("Tools/Vortex/SaveData/Game Index")]
        private static void Open()
        {
            var window = GetWindow<GameDataIndexWindow>("Game Data");
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
                GUILayout.Label(Application.isPlaying
                        ? $"Модель игры (Play Mode) — {GameController.GetState()}"
                        : "Модули проекта",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (!Application.isPlaying)
                {
                    if (GUILayout.Button("Обновить", EditorStyles.toolbarButton))
                        _index = null;
                    return;
                }

                if (GUILayout.Button("Сбросить всё", EditorStyles.toolbarButton)
                    && EditorUtility.DisplayDialog("Game Data",
                        "Пересобрать модель игры со значениями по умолчанию? Данные текущей сессии будут потеряны.",
                        "Сбросить", "Отмена"))
                    GameController.EditorResetAll();
            }
        }

        #region Edit mode

        private void DrawEditMode()
        {
            _index ??= BuildIndex();
            if (_index.Count == 0)
            {
                EditorGUILayout.HelpBox("В проекте нет реализаций GameModel.IGameData.", MessageType.Info);
                return;
            }

            foreach (var info in _index)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var open = Foldout(info.Type, info.Type.Name);
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
        /// Индекс реализаций по правилам модели: модуль создаётся через публичный конструктор без параметров,
        /// ключ — сам тип, поэтому дублей быть не может.
        /// </summary>
        private static List<ModuleInfo> BuildIndex()
        {
            var list = new List<ModuleInfo>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<GameModel.IGameData>())
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                var info = new ModuleInfo { Type = type };
                list.Add(info);

                if (type.ContainsGenericParameters)
                {
                    info.Problem = "Открытый generic-тип — модель его не создаст.";
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    info.Problem = "Нет публичного конструктора без параметров — модель модуль не найдёт.";
                    continue;
                }

                try
                {
                    info.Defaults = Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    info.Problem = $"Ошибка создания: {e.Message}";
                }
            }

            return list.OrderBy(i => i.Type.Name, StringComparer.Ordinal).ToList();
        }

        #endregion

        #region Runtime

        private void DrawRuntime()
        {
            if (GameController.GetState() == GameStates.Off)
                EditorGUILayout.HelpBox(
                    "Игра не запущена: показана модель вне сессии. Новая игра пересоберёт её со значениями по умолчанию.",
                    MessageType.Info);

            var modules = GameController.EditorModules();
            if (modules.Count == 0)
            {
                EditorGUILayout.HelpBox("В модели нет модулей.", MessageType.Info);
                return;
            }

            foreach (var pair in modules.OrderBy(p => p.Key.Name, StringComparer.Ordinal))
            {
                var module = pair.Value;
                var type = pair.Key;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    bool open;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        open = Foldout(type, type.Name);
                        if (GUILayout.Button("Сбросить", GUILayout.Width(80f)))
                            GameController.EditorResetModule(type);
                    }

                    if (!open || module == null)
                        continue;

                    foreach (var property in PocoInspector.Properties(type))
                    {
                        if (!PocoInspector.DrawValue(property, module, out var value))
                            continue;
                        property.SetValue(module, value);
                        GameController.EditorCommit();
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

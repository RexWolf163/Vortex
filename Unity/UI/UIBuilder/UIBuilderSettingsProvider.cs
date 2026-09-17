using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Vortex.Unity.UI.UIBuilder
{
    /// <summary>
    /// Страница <c>Project Settings → Vortex/UIBuilder</c>: настройки каждого модуля отдельным блоком.
    /// При открытии список настроек синхронизируется с найденными модулями.
    /// </summary>
    internal sealed class UIBuilderSettingsProvider : SettingsProvider
    {
        private readonly List<(string title, PropertyTree tree)> _blocks = new();

        private UIBuilderSettingsProvider(string path, SettingsScope scope) : base(path, scope)
        {
        }

        [SettingsProvider]
        private static SettingsProvider Create() =>
            new UIBuilderSettingsProvider(UIBuilderController.SettingsPath, SettingsScope.Project)
            {
                keywords = new[] { "vortex", "ui", "builder", "prefab", "primitive" }
            };

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            var settings = UIBuilderSettings.instance;
            settings.Sync();
            foreach (var module in UIBuilderController.CreateModules())
                _blocks.Add((module.Title, PropertyTree.Create(settings.GetFor(module.SettingsType))));
        }

        public override void OnDeactivate()
        {
            foreach (var block in _blocks)
                block.tree.Dispose();
            _blocks.Clear();
        }

        public override void OnGUI(string searchContext)
        {
            var changed = false;
            foreach (var (title, tree) in _blocks)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                tree.Draw(false);
                changed |= EditorGUI.EndChangeCheck() | tree.ApplyChanges();
            }

            var settings = UIBuilderSettings.instance;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Background Layer (Alt+I)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var color = EditorGUILayout.ColorField("Default Color", settings.BackgroundColor);
            if (EditorGUI.EndChangeCheck())
            {
                settings.BackgroundColor = color;
                changed = true;
            }

            if (changed)
                UIBuilderSettings.instance.SaveToDisk();
        }
    }
}

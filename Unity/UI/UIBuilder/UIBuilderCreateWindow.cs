using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Elements;
using Vortex.Unity.UI.UIBuilder.Base;

namespace Vortex.Unity.UI.UIBuilder
{
    /// <summary>
    /// Окно параметров создания. Каталог примитивов собирается при открытии (папка модуля с подпапками) и
    /// показывается выпадающим списком с группировкой по подпапкам. При выборе примитива читается его состав:
    /// показываются только секции модуля, для которых в примитиве есть часть нужного типа.
    /// </summary>
    internal sealed class UIBuilderCreateWindow : EditorWindow
    {
        private UIBuilderModule _module;
        private GameObject _parent;

        private string _error;
        private GameObject[] _prefabs = new GameObject[0];
        private string[] _labels = new string[0];
        private int _selected = -1;
        private string _layerName;
        private Vector2 _size;

        private readonly List<UIBuilderSection> _sections = new();
        private readonly List<PropertyTree> _trees = new();
        private Vector2 _scroll;

        internal static void Open(UIBuilderModule module, UIBuilderModuleSettings settings, GameObject parent)
        {
            var window = CreateInstance<UIBuilderCreateWindow>();
            window.titleContent = new GUIContent($"Create {module.Title}");
            window._module = module;
            window._parent = parent;
            window._layerName = settings.DefaultLayerName;
            window._size = settings.DefaultSize;
            window.LoadCatalog(settings);
            window.minSize = new Vector2(360, 240);
            window.ShowUtility();
        }

        private void LoadCatalog(UIBuilderModuleSettings settings)
        {
            _error = UIBuilderCatalog.Load(settings.Folder, out _labels, out _prefabs);
            if (_error == null)
                Select(settings.DefaultPrefab == null ? -1 : Array.IndexOf(_prefabs, settings.DefaultPrefab));
        }

        private void Select(int index)
        {
            _selected = index;
            DisposeTrees();
            _sections.Clear();
            if (index < 0)
                return;

            var prefab = _prefabs[index];
            foreach (var section in _module.CreateSections())
            {
                if (prefab.GetComponentInChildren(section.PartType, true) == null)
                    continue;
                _sections.Add(section);
                _trees.Add(PropertyTree.Create(section));
            }
        }

        private void OnGUI()
        {
            // После перезагрузки домена модуль не восстанавливается — окно одноразовое
            if (_module == null || _parent == null)
            {
                Close();
                GUIUtility.ExitGUI();
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Родитель", _parent, typeof(GameObject), true);

            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Warning);
                return;
            }

            var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent("Примитив"));
            SearchablePopup.Draw(rect, _labels, _selected, null, index =>
            {
                Select(index);
                Repaint();
            });

            _layerName = EditorGUILayout.TextField("Имя слоя", _layerName);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < _sections.Count; i++)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(_sections[i].Title, EditorStyles.boldLabel);
                _trees[i].Draw(false);
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(_selected < 0 || string.IsNullOrWhiteSpace(_layerName)))
            {
                if (GUILayout.Button("Создать", GUILayout.Height(28)))
                {
                    UIBuilderController.Create(_module, _parent, _prefabs[_selected], _layerName, _size,
                        _sections);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void OnDisable() => DisposeTrees();

        private void DisposeTrees()
        {
            foreach (var tree in _trees)
                tree.Dispose();
            _trees.Clear();
        }
    }
}

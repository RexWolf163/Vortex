#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vortex.Unity.EditorTools.PrefabTools
{
    /// <summary>
    /// Чистка избыточных переопределений префаба: удаляет модификации, значение которых совпадает со значением
    /// в исходном префабе. Висячие переопределения (цели нет) не трогает — это работа встроенного
    /// «Prefab/Remove Unused Overrides».
    ///
    /// Открывается из контекстного меню Hierarchy: <c>Prefab/CleanOverrideTrash</c>. Сканирует все экземпляры
    /// префабов в поддереве выделенного объекта (включая сам объект — в режиме префаба это корень варианта).
    /// Список показывается с галочками: совпадение с базой бывает намеренным (фиксирует значение от будущих
    /// правок базы), поэтому удаляется только отмеченное.
    ///
    /// Не считаются избыточными: default overrides (позиция/имя корня экземпляра), составные и
    /// <c>[SerializeReference]</c>-свойства целиком, свойства, путь которых не находится в источнике или экземпляре.
    /// </summary>
    internal sealed class PrefabOverrideCleanerWindow : EditorWindow
    {
        private const string MenuPath = "GameObject/Prefab/CleanOverrideTrash";
        private const string UndoName = "Clean Override Trash";

        private sealed class Entry
        {
            public (Object target, string path) Key;
            public Object Instance;
            public string ObjectLabel;
            public string ValueLabel;
        }

        [SerializeField] private GameObject root;

        private readonly List<Entry> _entries = new();
        private readonly HashSet<(Object target, string path)> _excluded = new();
        private int _totalModifications;
        private Vector2 _scroll;

        [MenuItem(MenuPath, true)]
        private static bool OpenValidate() => Selection.activeGameObject != null;

        [MenuItem(MenuPath, false, 0)]
        private static void Open(MenuCommand command)
        {
            // Из контекстного меню Hierarchy пункт вызывается по разу на каждый выделенный объект
            if (command.context != null && command.context != Selection.activeGameObject)
                return;

            var window = GetWindow<PrefabOverrideCleanerWindow>("Override Trash");
            window.root = Selection.activeGameObject;
            window._excluded.Clear();
            window.Scan();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Scan;
            Scan();
        }

        private void OnDisable() => Undo.undoRedoPerformed -= Scan;

        private void OnFocus() => Scan();

        private void Scan()
        {
            _entries.Clear();
            _totalModifications = 0;
            if (root == null)
                return;

            _entries.AddRange(CollectRedundant(root, out _totalModifications));
            Repaint();
        }

        private void RemoveSelected()
        {
            var selected = new HashSet<(Object, string)>(
                CollectRedundant(root, out _).Select(e => e.Key).Where(k => !_excluded.Contains(k)));
            if (selected.Count == 0)
                return;

            var removed = 0;
            foreach (var instance in CollectInstances(root))
            {
                var modifications = PrefabUtility.GetPropertyModifications(instance);
                if (modifications == null)
                    continue;

                var kept = modifications.Where(m => !selected.Contains((m.target, m.propertyPath))).ToArray();
                if (kept.Length == modifications.Length)
                    continue;

                Undo.RegisterCompleteObjectUndo(PrefabUtility.GetPrefabInstanceHandle(instance), UndoName);
                PrefabUtility.SetPropertyModifications(instance, kept);
                removed += modifications.Length - kept.Length;
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[{UndoName}] {root.name}: удалено переопределений — {removed}.");
            Scan();
        }

        #region Scan

        /// <summary>
        /// Один GameObject на каждый экземпляр префаба (по instance handle) в поддереве корня.
        /// Вложенное содержимое базы принадлежит внешнему экземпляру; добавленные вложенные префабы — отдельные экземпляры.
        /// </summary>
        private static List<GameObject> CollectInstances(GameObject root)
        {
            var result = new List<GameObject>();
            var handles = new HashSet<Object>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                var go = transform.gameObject;
                if (!PrefabUtility.IsPartOfPrefabInstance(go))
                    continue;

                if (handles.Add(PrefabUtility.GetPrefabInstanceHandle(go)))
                    result.Add(go);
            }

            return result;
        }

        private static List<Entry> CollectRedundant(GameObject root, out int totalModifications)
        {
            totalModifications = 0;
            var result = new List<Entry>();
            var sourceToInstance = BuildSourceMap(root);
            var serialized = new Dictionary<Object, SerializedObject>();

            foreach (var instance in CollectInstances(root))
            {
                var modifications = PrefabUtility.GetPropertyModifications(instance);
                if (modifications == null)
                    continue;

                totalModifications += modifications.Length;
                foreach (var modification in modifications)
                {
                    if (modification.target == null
                        || !sourceToInstance.TryGetValue(modification.target, out var instanceObject)
                        || PrefabUtility.IsDefaultOverride(modification)
                        || !IsRedundant(modification, instanceObject, serialized))
                        continue;

                    result.Add(new Entry
                    {
                        Key = (modification.target, modification.propertyPath),
                        Instance = instanceObject,
                        ObjectLabel = GetObjectLabel(instanceObject, root.transform),
                        ValueLabel = modification.objectReference != null
                            ? modification.objectReference.name
                            : modification.value,
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Объект в исходном префабе → соответствующий объект в поддереве корня. Модификации адресуют источник,
        /// а текущее (переопределённое) значение читается с экземпляра.
        /// </summary>
        private static Dictionary<Object, Object> BuildSourceMap(GameObject root)
        {
            var map = new Dictionary<Object, Object>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                AddToMap(map, transform.gameObject);
                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component != null)
                        AddToMap(map, component);
                }
            }

            return map;
        }

        private static void AddToMap(Dictionary<Object, Object> map, Object instanceObject)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceObject);
            if (source != null)
                map[source] = instanceObject;
        }

        private static bool IsRedundant(PropertyModification modification, Object instanceObject,
            Dictionary<Object, SerializedObject> serialized)
        {
            var sourceProperty = GetSerialized(serialized, modification.target).FindProperty(modification.propertyPath);
            var instanceProperty = GetSerialized(serialized, instanceObject).FindProperty(modification.propertyPath);
            if (sourceProperty == null || instanceProperty == null
                || sourceProperty.propertyType != instanceProperty.propertyType)
                return false;

            switch (sourceProperty.propertyType)
            {
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ManagedReference:
                    return false;

                case SerializedPropertyType.ObjectReference:
                    var sourceValue = sourceProperty.objectReferenceValue;
                    var instanceValue = instanceProperty.objectReferenceValue;
                    // Ссылка внутрь префаба у экземпляра указывает на свою копию объекта, а не на объект источника
                    return instanceValue == sourceValue
                           || (sourceValue != null && instanceValue != null
                               && PrefabUtility.GetCorrespondingObjectFromSource(instanceValue) == sourceValue);

                case SerializedPropertyType.Float:
                    return Mathf.Approximately(instanceProperty.floatValue, sourceProperty.floatValue);

                case SerializedPropertyType.ArraySize:
                    return instanceProperty.intValue == sourceProperty.intValue;

                default:
                    return SerializedProperty.DataEquals(instanceProperty, sourceProperty);
            }
        }

        private static SerializedObject GetSerialized(Dictionary<Object, SerializedObject> cache, Object target)
        {
            if (!cache.TryGetValue(target, out var serializedObject))
                cache[target] = serializedObject = new SerializedObject(target);
            return serializedObject;
        }

        private static string GetObjectLabel(Object instanceObject, Transform root)
        {
            var go = instanceObject as GameObject ?? ((Component)instanceObject).gameObject;
            var path = go.name;
            for (var t = go.transform; t != root && t.parent != null; t = t.parent)
                path = $"{t.parent.name}/{path}";

            var type = instanceObject is GameObject ? "GameObject" : instanceObject.GetType().Name;
            return $"{path} › {type}";
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            if (root == null)
            {
                EditorGUILayout.HelpBox(
                    "Объект не выбран или удалён. ПКМ по объекту в Hierarchy → Prefab/CleanOverrideTrash.",
                    MessageType.Info);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Корень", root, typeof(GameObject), true);

            EditorGUILayout.LabelField(
                $"Переопределений всего: {_totalModifications}, совпадают с базой: {_entries.Count}");
            EditorGUILayout.HelpBox(
                "Совпадение с базой бывает намеренным: такое переопределение фиксирует значение от будущих правок " +
                "базового префаба. Снимите галочки с тех, что нужно сохранить.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Все", EditorStyles.miniButtonLeft))
                    _excluded.Clear();
                if (GUILayout.Button("Ничего", EditorStyles.miniButtonMid))
                    _excluded.UnionWith(_entries.Select(e => e.Key));
                if (GUILayout.Button("Обновить", EditorStyles.miniButtonRight))
                    Scan();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var group in _entries.GroupBy(e => e.ObjectLabel))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(group.Key, EditorStyles.boldLabel);
                    if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                        EditorGUIUtility.PingObject(group.First().Instance);
                }

                foreach (var entry in group)
                {
                    var isSelected = !_excluded.Contains(entry.Key);
                    var toggled = EditorGUILayout.ToggleLeft($"{entry.Key.path} = {entry.ValueLabel}", isSelected);
                    if (toggled == isSelected)
                        continue;

                    if (toggled)
                        _excluded.Remove(entry.Key);
                    else
                        _excluded.Add(entry.Key);
                }
            }

            EditorGUILayout.EndScrollView();

            var selectedCount = _entries.Count(e => !_excluded.Contains(e.Key));
            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                if (GUILayout.Button($"Удалить отмеченные ({selectedCount})", GUILayout.Height(28)))
                    RemoveSelected();
            }
        }

        #endregion
    }
}
#endif

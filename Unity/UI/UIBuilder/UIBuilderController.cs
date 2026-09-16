using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vortex.Unity.UI.UIBuilder.Base;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.UI.UIBuilder
{
    /// <summary>
    /// Ядро UIBuilder: поиск модулей, открытие окна создания из пункта меню модуля и общий шаг сборки слоя.
    /// </summary>
    public static class UIBuilderController
    {
        internal const string SettingsPath = "Project/Vortex/UIBuilder";

        private const string LogPrefix = "[UIBuilder]";
        private const string InitMethodName = "Init";

        /// <summary>Validate-обработчик пунктов меню модулей.</summary>
        public static bool CanOpen() => Selection.activeGameObject != null;

        /// <summary>
        /// Обработчик пункта меню модуля. Папка не задана — открывает страницу настроек; иначе — окно создания
        /// под активным выделенным объектом.
        /// </summary>
        public static void Open<TModule>(MenuCommand command) where TModule : UIBuilderModule, new()
        {
            // Из контекстного меню Hierarchy пункт вызывается по разу на каждый выделенный объект
            if (command.context != null && command.context != Selection.activeGameObject)
                return;

            var parent = Selection.activeGameObject;
            if (parent == null)
                return;

            var module = new TModule();
            var settings = UIBuilderSettings.instance.GetFor(module.SettingsType);
            if (string.IsNullOrEmpty(settings.Folder))
            {
                Debug.LogWarning($"{LogPrefix} {module.Title}: не задана папка примитивов.");
                SettingsService.OpenProjectSettings(SettingsPath);
                return;
            }

            UIBuilderCreateWindow.Open(module, settings, parent);
        }

        internal static IReadOnlyList<UIBuilderModule> CreateModules() =>
            TypeCache.GetTypesDerivedFrom<UIBuilderModule>()
                .Where(t => !t.IsAbstract && !t.ContainsGenericParameters)
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .Select(t => (UIBuilderModule)Activator.CreateInstance(t))
                .ToList();

        /// <summary>
        /// Слой <paramref name="layerName"/> размером <paramref name="size"/> с <see cref="UIComponent"/> под
        /// <paramref name="parent"/>, внутри — экземпляр <paramref name="prefab"/> без изменений; части собираются,
        /// затем шаг модуля. Одна группа Undo; при ошибке созданное откатывается.
        /// </summary>
        internal static void Create(UIBuilderModule module, GameObject parent, GameObject prefab, string layerName,
            Vector2 size, IReadOnlyList<UIBuilderSection> sections)
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Create {module.Title}");

            try
            {
                if (parent.GetComponent<RectTransform>() == null)
                    Debug.LogWarning($"{LogPrefix} Родитель «{parent.name}» не UI-объект (нет RectTransform).", parent);

                var layer = new GameObject(layerName, typeof(RectTransform));
                // Слой неактивен до конца сборки: ExecuteInEditMode-компоненты не должны включиться без ссылок
                layer.SetActive(false);
                Undo.RegisterCreatedObjectUndo(layer, $"Create {module.Title}");
                GameObjectUtility.SetParentAndAlign(layer, parent);
                ((RectTransform)layer.transform).sizeDelta = size;

                var component = layer.AddComponent<UIComponent>();
                PrefabUtility.InstantiatePrefab(prefab, layer.transform);
                InitParts(component);

                module.Apply(component, sections);

                layer.SetActive(true);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(layer.scene);
                Selection.activeGameObject = layer;
            }
            catch (Exception e)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Сбор частей через editor-метод <c>UIComponent.Init</c>. Метод приватный, пакет UIComponents не меняем —
        /// вызов через рефлексию; метод переименован или удалён — исключение с именем.
        /// </summary>
        private static void InitParts(UIComponent component)
        {
            var method = typeof(UIComponent).GetMethod(InitMethodName,
                             BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                             Type.EmptyTypes, null)
                         ?? throw new MissingMethodException(nameof(UIComponent), InitMethodName);
            method.Invoke(component, null);
        }
    }
}

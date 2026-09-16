using System;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.UI.UIBuilder.Base
{
    /// <summary>
    /// Секция параметров создания: общий кусок окна, привязанный к типу части <see cref="UIComponent"/>.
    /// Показывается, только если в выбранном примитиве есть часть <see cref="PartType"/>. Поля секции рисуются
    /// Odin (<c>[SerializeField]</c>). Экземпляр живёт, пока открыто окно.
    /// </summary>
    [Serializable]
    public abstract class UIBuilderSection
    {
        /// <summary>Тип части <c>UIComponentPart</c>, без которой секция бессмысленна.</summary>
        public abstract Type PartType { get; }

        /// <summary>Заголовок секции в окне.</summary>
        public abstract string Title { get; }

        /// <summary>Добавить и настроить компоненты на созданном слое. Части <paramref name="component"/> уже собраны.</summary>
        public abstract void Apply(UIComponent component);

        /// <summary>
        /// Добавляет компонент вида <c>Set*Component</c> и проставляет общие поля: ссылку <c>uiComponent</c> и
        /// <c>position = -1</c> (все части типа). <paramref name="fill"/> дописывает собственные поля компонента.
        /// </summary>
        protected static T AddLinked<T>(UIComponent component, Action<SerializedObject> fill = null)
            where T : Component
        {
            var target = component.gameObject.AddComponent<T>();
            var serialized = new SerializedObject(target);
            Find(serialized, "uiComponent").objectReferenceValue = component;
            Find(serialized, "position").intValue = -1;
            fill?.Invoke(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return target;
        }

        protected static SerializedProperty Find(SerializedObject serialized, string name) =>
            serialized.FindProperty(name)
            ?? throw new InvalidOperationException(
                $"[UIBuilder] {serialized.targetObject.GetType().Name}: не найдено поле «{name}».");
    }
}

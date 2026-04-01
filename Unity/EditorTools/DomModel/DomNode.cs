#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;

namespace Vortex.Unity.EditorTools.DomModel
{
    /// <summary>
    /// Один узел = одно сериализуемое поле.
    /// Хранит предрассчитанные PropertyData
    ///
    /// Возможно враппер лишний, но пусть пока будет. Возможно в него добавится параметров
    /// </summary>
    internal class DomNode
    {
        public DomNode Parent { get; }
        public readonly List<DomNode> Childrens = new();
        public readonly PropertyData Data = new();

        /// <summary>
        /// Маркер первой ноды контейнера
        /// </summary>
        public bool IsFirstNode { get; internal set; }

        public DomPage Page { get; }

        /// <summary>
        /// true = контейнерная нода (компонент), не имеет собственного property
        /// </summary>
        public bool IsContainer { get; internal set; }

        /// <summary>
        /// Индекс порядка для сортировки
        /// </summary>
        public int Order { get; internal set; }

        /// <summary>
        /// Нода для сериализуемого поля.
        /// </summary>
        public DomNode(DomPage page, DomNode parent, SerializedProperty property, GUIContent label)
        {
            Page = page;
            Parent = parent;
            Data.Reset(label, property, InspectorHandler.GetFieldInfo(property));
        }

        /// <summary>
        /// Нода для метода
        /// </summary>
        public DomNode(DomPage page, DomNode parent, Object owner, MethodInfo method)
        {
            Page = page;
            Parent = parent;
            Data.Reset(owner, method);
        }

        /// <summary>
        /// Контейнерная нода (компонент/скрипт). Не имеет property, служит корнем для полей.
        /// </summary>
        public DomNode(DomPage page, float width)
        {
            Page = page;
            IsContainer = true;
            Data.Width = width;
        }
    }
}
#endif
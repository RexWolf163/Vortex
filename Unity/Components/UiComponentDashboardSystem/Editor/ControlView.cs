#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Vortex.Unity.Components.UiComponentDashboardSystem.Editor
{
    /// <summary>
    /// Отрисовка органов управления одного типа управляющего скрипта по сериализуемым полям переданного экземпляра.
    /// </summary>
    internal abstract class ControlView
    {
        /// <summary>Тип управляющего скрипта, органы управления которого рисует вид.</summary>
        public abstract Type ControlType { get; }

        /// <summary>Нарисовать органы управления по сериализуемым полям экземпляра <paramref name="control"/>.</summary>
        /// <param name="control">Сериализованное представление экземпляра управляющего скрипта типа <see cref="ControlType"/>.</param>
        public abstract void Draw(SerializedObject control);
    }
}
#endif

using System;
using UnityEngine;
using Vortex.Core.StateAxisSystem.Abstractions;

namespace Vortex.Unity.StateAxisSystem.Attributes
{
    /// <summary>
    /// Inspector-атрибут для строковых полей, хранящих ключ оси.
    /// Drawer показывает popup с допустимыми ключами оси <see cref="AxisType"/>.
    /// В рантайме поле — обычная <c>string</c>; преобразование в инстанс через
    /// <see cref="StateAxis.GetByKey{T}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// [StateKey(typeof(MoveState))]
    /// [SerializeField] private string defaultMoveState;
    ///
    /// // в рантайме:
    /// var state = StateAxis.GetByKey&lt;MoveState&gt;(defaultMoveState);
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class StateKeyAttribute : PropertyAttribute
    {
        public Type AxisType { get; }

        public StateKeyAttribute(Type axisType)
        {
            if (axisType == null)
                throw new ArgumentNullException(nameof(axisType));
            if (!typeof(StateAxis).IsAssignableFrom(axisType))
                throw new ArgumentException($"[StateKey] {axisType.Name} must inherit from StateAxis", nameof(axisType));
            AxisType = axisType;
        }
    }
}

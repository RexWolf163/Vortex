using System;
using UnityEngine;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Unity.ExtensibleEnumSystem.Attributes
{
    /// <summary>
    /// Inspector-атрибут для строковых полей, хранящих ключ ExtensibleEnum-типа.
    /// Drawer показывает popup с допустимыми ключами типа <see cref="ExtEnumType"/>.
    /// В рантайме поле — обычная <c>string</c>; преобразование в инстанс через
    /// <see cref="ExtensibleEnum.GetByKey{T}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// [ExtEnumKey(typeof(MoveState))]
    /// [SerializeField] private string defaultMoveState;
    ///
    /// // в рантайме:
    /// var state = ExtensibleEnum.GetByKey&lt;MoveState&gt;(defaultMoveState);
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ExtEnumKeyAttribute : PropertyAttribute
    {
        public Type ExtEnumType { get; }

        public ExtEnumKeyAttribute(Type extEnumType)
        {
            if (extEnumType == null)
                throw new ArgumentNullException(nameof(extEnumType));
            if (!typeof(ExtensibleEnum).IsAssignableFrom(extEnumType))
                throw new ArgumentException($"[ExtEnumKey] {extEnumType.Name} must inherit from ExtensibleEnum",
                    nameof(extEnumType));
            ExtEnumType = extEnumType;
        }
    }
}

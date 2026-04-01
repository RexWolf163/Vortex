#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Abstraction
{
    /// <summary>
    /// Уникальный ключ для кеширования данных свойства.
    /// Учитывает и экземпляр объекта, и путь к полю.
    /// </summary>
    internal struct PropertyKey : IEquatable<PropertyKey>
    {
        public readonly int InstanceId;
        public readonly string Path;

        public PropertyKey(SerializedProperty property)
        {
            InstanceId = property.serializedObject.targetObjects[0].GetInstanceID();
            Path = property.propertyPath;
        }

        public bool Equals(PropertyKey other) =>
            InstanceId == other.InstanceId && Path == other.Path;

        public override bool Equals(object obj) => obj is PropertyKey other && Equals(other);

        public override int GetHashCode() =>
            unchecked((InstanceId * 397) ^ (Path?.GetHashCode() ?? 0));

        public static bool operator ==(PropertyKey left, PropertyKey right) => left.Equals(right);
        public static bool operator !=(PropertyKey left, PropertyKey right) => !left.Equals(right);

        public static implicit operator PropertyKey(SerializedProperty property) => new(property);
    }
}
#endif
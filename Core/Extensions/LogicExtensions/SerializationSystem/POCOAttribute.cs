using System;

namespace Vortex.Core.Extensions.LogicExtensions.SerializationSystem
{
    /// <summary>
    /// Маркер сериализуемого типа данных.
    /// Сериализатор обрабатывает только типы помеченные этим атрибутом.
    /// Допускается на классе, структуре или интерфейсе.
    /// При размещении на интерфейсе все его реализации считаются сериализуемыми.
    /// Простые типы (примитивы, string, enum, DateTime, Guid) сериализуются всегда.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, Inherited = false)]
    public class POCOAttribute : Attribute { }

    /// <summary>
    /// Исключает свойство из сериализации в классе помеченном [POCO].
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NotPOCOAttribute : Attribute { }

    /// <summary>
    /// Принудительно включает непубличное свойство (internal/protected/private)
    /// в сериализацию в классе помеченном [POCO].
    /// Для свойств с public getter атрибут избыточен — такие свойства
    /// сериализуются по умолчанию.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IsPOCOAttribute : Attribute { }
}

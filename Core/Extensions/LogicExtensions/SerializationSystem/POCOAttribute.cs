using System;

namespace Vortex.Core.Extensions.LogicExtensions.SerializationSystem
{
    /// <summary>
    /// Маркер сериализуемого типа данных.
    /// Сериализатор обрабатывает только классы/структуры помеченные этим атрибутом.
    /// Простые типы (примитивы, string, enum, DateTime, Guid) сериализуются всегда.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public class POCOAttribute : Attribute { }

    /// <summary>
    /// Исключает свойство из сериализации в классе помеченном [POCO].
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class NotPOCOAttribute : Attribute { }
}

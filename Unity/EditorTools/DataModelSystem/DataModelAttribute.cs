using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.DataModelSystem
{
    /// <summary>
    /// Атрибут для полей, чьи runtime-данные нужно развернуть в инспекторе.
    /// Отображает публичные свойства объекта (включая унаследованные) в foldout-группе.
    /// Свойства с сеттером доступны для редактирования через рефлексию.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DataModelAttribute : PropertyAttribute
    {
    }
}

using System;

namespace Vortex.Unity.EditorTools.DataModelSystem
{
    /// <summary>
    /// Атрибут для методов, которые должны отображаться как кнопки
    /// внутри DataModel-развёртки в инспекторе.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class DataModelMethodAttribute : Attribute
    {
        public string Label { get; }

        public DataModelMethodAttribute() { }

        /// <param name="label">Текст кнопки. Если не задан — используется имя метода</param>
        public DataModelMethodAttribute(string label) => Label = label;
    }
}

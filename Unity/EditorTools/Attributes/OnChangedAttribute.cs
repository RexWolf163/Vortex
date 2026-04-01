using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    /// <summary>
    /// Вызывает метод по имени при изменении значения поля.
    /// Метод должен быть публичным или иметь атрибут [SerializeField],
    /// без параметров или с одним параметром того же типа, что и поле.
    /// </summary>
    public class OnChangedAttribute : PropertyAttribute
    {
        public readonly string MethodName;

        public OnChangedAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}
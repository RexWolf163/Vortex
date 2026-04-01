using System;

namespace Vortex.Unity.EditorTools.Attributes
{
    /// <summary>
    /// Управляет порядком отрисовки поля в Inspector через VortexRenderer.
    /// Priority задаёт желаемую позицию поля (внутри уменьшается на 1).
    /// По умолчанию позиция поля равна его порядковому номеру.
    ///
    /// Пример: [Position(3)] на первом поле → поле встанет после стандартного претендента
    /// на позицию 2, т.е. перед стандартным претендентом на позицию 3.
    ///
    /// Работает только под CollectionEditor (MonoBehaviour).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class PositionAttribute : Attribute
    {
        public int Priority { get; }

        public PositionAttribute(int priority)
        {
            Priority = priority - 1;
        }
    }
}

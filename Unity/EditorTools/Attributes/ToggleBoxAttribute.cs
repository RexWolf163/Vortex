using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    /// <summary>
    /// Условное отображение поля в Inspector.
    /// Поле видно только когда control-поле/свойство/метод возвращает значение равное state.
    /// Поля с этим атрибутом группируются и рисуются под control-полем с отступом и границей.
    ///
    /// Поддерживаемые типы control: bool, int, byte, enum.
    /// Для bool: 0 = false, 1 = true.
    /// Для enum: приведение к int (enumValueIndex).
    ///
    /// Нативный путь: VortexRenderer (CollectionEditor).
    /// Odin путь: ToggleBoxGroupProcessor + ToggleBoxGroupDrawer (OdinGroupDrawer).
    /// </summary>
    public class ToggleBoxAttribute : PropertyAttribute
    {
        public string Control { get; }
        public int State { get; }

        /// <param name="control">Имя поля, свойства или метода на объекте-владельце</param>
        /// <param name="state">Значение, при котором поле отображается (int-приведение)</param>
        public ToggleBoxAttribute(string control, int state)
        {
            Control = control;
            State = state;
        }
    }
}

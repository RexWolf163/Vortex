using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    /// <summary>
    /// Заменяет стандартное поле на горизонтальные кнопки-переключатели.
    /// Поддерживает bool, int, byte, enum.
    ///
    /// Параметры (опциональные):
    ///   labelsMethod — имя метода, возвращающего Dictionary&lt;int, string&gt; (ключ = значение, строка = текст кнопки)
    ///   colorsMethod — имя метода, возвращающего Dictionary&lt;int, Color&gt; (ключ = значение, Color = цвет кнопки)
    ///   isSingleButton - делать кнопку прокликивание которой по кругу меняет значение поля
    ///
    /// Для bool без параметров: кнопки Off/On с цветами из палитры.
    /// Для enum без параметров: кнопки по именам enum-значений.
    /// Для int/byte без labelsMethod: ошибка (невозможно определить набор кнопок).
    /// </summary>
    public class ToggleButtonAttribute : PropertyAttribute
    {
        public string LabelsMethod { get; }
        public string ColorsMethod { get; }

        public bool IsSingleButton { get; }

        public ToggleButtonAttribute(string labelsMethod = null, string colorsMethod = null,
            bool isSingleButton = false)
        {
            LabelsMethod = labelsMethod;
            ColorsMethod = colorsMethod;
            IsSingleButton = isSingleButton;
        }
    }
}
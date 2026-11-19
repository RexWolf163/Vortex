namespace Vortex.Unity.UI.TextInputSystem.Filters
{
    /// <summary>
    /// Маска поля ввода <see cref="UITextInput"/>. Подключается списком через <c>[SerializeReference]</c>: строка
    /// допустима, только если её пропускают все фильтры списка.
    /// Две проверки, потому что во время набора строка ещё не дописана: «-» или пусто — нормальные промежуточные
    /// состояния числа, но не итог
    /// </summary>
    public interface ITextInputFilter
    {
        /// <summary>
        /// Допустима ли строка во время набора. Вызывается на каждый вводимый символ со строкой, которая
        /// получится после его вставки; <c>false</c> — символ не вводится
        /// </summary>
        bool IsValidPartial(string candidate);

        /// <summary>Допустима ли строка как итог: <c>false</c> — сохранение не проходит, ввод продолжается</summary>
        bool IsValidFinal(string text);
    }
}

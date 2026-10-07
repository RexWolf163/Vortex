namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>
    /// Словарь-индекс возможных действий указателя. Имена генерик — конкретную клавишу/ось назначает
    /// верстальщик в Input Actions; комментарий фиксирует предполагаемую конвенцию маппинга на девайс.
    /// ПОРЯДОК НЕ МЕНЯТЬ: индекс = позиция бита в <see cref="PointerActionMask"/> и ключ карты спрайтов.
    /// </summary>
    public enum PointerAction
    {
        /// <summary>Нейтраль — ничего не активно (базовый вид курсора).</summary>
        None,

        /// <summary>Левая кнопка (LMB).</summary>
        BaseClick,

        /// <summary>Правая кнопка (RMB).</summary>
        ContextClick,

        /// <summary>Средняя кнопка (MMB).</summary>
        AltClick
    }
}

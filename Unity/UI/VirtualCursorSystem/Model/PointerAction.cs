namespace Vortex.Unity.UI.VirtualCursorSystem
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
        Action1,

        /// <summary>Правая кнопка (RMB).</summary>
        Action2,

        /// <summary>Средняя кнопка (MMB).</summary>
        Action3,

        /// <summary>Боковая кнопка 1 (Back).</summary>
        Action4,

        /// <summary>Боковая кнопка 2 (Forward).</summary>
        Action5,

        /// <summary>Скролл вверх.</summary>
        Action6,

        /// <summary>Скролл вниз.</summary>
        Action7,

        /// <summary>Запас.</summary>
        Action8,

        /// <summary>Запас.</summary>
        Action9,

        /// <summary>Запас.</summary>
        Action10,
    }
}

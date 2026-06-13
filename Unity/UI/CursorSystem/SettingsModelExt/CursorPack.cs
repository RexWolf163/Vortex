using System;
using UnityEngine;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Набор спрайтов курсора для одного диапазона разрешений.
    ///
    /// Компилируется в сборку настроек (ru.vortex.settings через .asmref папки),
    /// так как тип используется в partial-расширении SettingsModel —
    /// обратная ссылка из сборки настроек на пакет курсора невозможна (цикл)
    /// </summary>
    [Serializable]
    public class CursorPack
    {
        [Tooltip("Спрайт по умолчанию. null в выбранном пакете = ошибка конфигурации (fail-fast)")]
        [SerializeField] private Sprite cursorDefault;

        [Tooltip("Спрайт при удержании левой кнопки мыши. null = не менять при LMB")]
        [SerializeField] private Sprite cursorLeftMouseDown;

        [Tooltip("Спрайт при удержании правой кнопки мыши. null = не менять при RMB")]
        [SerializeField] private Sprite cursorRightMouseDown;

        [Tooltip("Hover-варианты; индекс соответствует MouseHoverListener.index. " +
                 "Порядок и длина должны совпадать во всех пакетах. null-элемент = использовать default")]
        [SerializeField] private Sprite[] cursorOnHover = new Sprite[0];

        public Sprite CursorDefault => cursorDefault;
        public Sprite CursorLeftMouseDown => cursorLeftMouseDown;
        public Sprite CursorRightMouseDown => cursorRightMouseDown;
        public Sprite[] CursorOnHover => cursorOnHover;
    }
}

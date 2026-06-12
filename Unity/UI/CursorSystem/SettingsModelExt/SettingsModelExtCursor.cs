using UnityEngine;

namespace Vortex.Core.SettingsSystem.Model
{
    /// <summary>
    /// Partial-расширение <see cref="SettingsModel"/> со стороны пакета
    /// <c>ru.vortex.unity.cursorsystem</c>. Подключается через <c>.asmref</c> на сборку
    /// настроек — поля доступны напрямую через <c>Settings.Data().CursorDefault</c> и т.д.
    ///
    /// Канонический Vortex-паттерн «package adds its own settings fields» —
    /// см. COMPOSITION.md, раздел «ComplexModel — самосборка доменной модели».
    /// При отключении пакета (удаление папки / снятие зависимости) поля исчезают
    /// вместе с partial-расширением, остальной <see cref="SettingsModel"/> компилируется.
    /// </summary>
    public partial class SettingsModel
    {
        /// <summary>Спрайт курсора по умолчанию. <c>null</c> = аппаратный курсор.</summary>
        public Sprite CursorDefault { get; private set; }

        /// <summary>Спрайт курсора при нажатой LMB. <c>null</c> = не менять при LMB.</summary>
        public Sprite CursorLeftMouseDown { get; private set; }

        /// <summary>Спрайт курсора при нажатой RMB. <c>null</c> = не менять при RMB.</summary>
        public Sprite CursorRightMouseDown { get; private set; }

        /// <summary>Массив hover-вариантов курсора; индекс соответствует <c>MouseHoverListener.index</c>.</summary>
        public Sprite[] CursorOnHover { get; private set; }
    }
}
using Vortex.Unity.UI.CursorSystem;

namespace Vortex.Core.SettingsSystem.Model
{
    /// <summary>
    /// Partial-расширение <see cref="SettingsModel"/> со стороны пакета
    /// <c>ru.vortex.unity.cursorsystem</c>. Подключается через <c>.asmref</c> на сборку
    /// настроек — поля доступны напрямую через <c>Settings.Data().CursorPacks</c>.
    ///
    /// Канонический Vortex-паттерн «package adds its own settings fields» —
    /// см. COMPOSITION.md, раздел «ComplexModel — самосборка доменной модели».
    /// При отключении пакета (удаление папки / снятие зависимости) поля исчезают
    /// вместе с partial-расширением, остальной <see cref="SettingsModel"/> компилируется.
    /// </summary>
    public partial class SettingsModel
    {
        /// <summary>
        /// Наборы курсоров по диапазонам разрешения.
        /// Пустой список или <c>null</c> = аппаратный курсор (контроллер не запускается)
        /// </summary>
        public CursorResolutionPack[] CursorPacks { get; private set; }
    }
}

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
        [Tooltip("Базовый набор курсора: Default (обычный), Action (LMB), AltAction (RMB). " +
                 "Default обязателен — null в выбранном пакете = ошибка конфигурации (fail-fast).")]
        [SerializeField] private CursorHoverEntry cursorDefault = new();

        [Tooltip("Hover-варианты; ключ = CursorHoverEntry.Name (общий для всех пакетов). " +
                 "Недостающий ключ наследуется от более раннего пакета (к первому), не наоборот. " +
                 "У каждого пакета может быть свой набор ключей.")]
        [SerializeField] private CursorHoverEntry[] cursorOnHover = new CursorHoverEntry[0];

        public CursorHoverEntry CursorDefault => cursorDefault;
        public CursorHoverEntry[] CursorOnHover => cursorOnHover;
    }
}

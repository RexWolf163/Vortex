using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// Пресет настроек курсора — наследник <see cref="SettingsPreset"/>, читается
    /// <see cref="CursorController"/> через <c>Settings.OnInit</c> и копируется в
    /// partial-расширение <see cref="Vortex.Core.SettingsSystem.Model.SettingsModel"/>.
    ///
    /// Логика «нет спрайта = нет курсора»: если <see cref="CursorDefault"/> не задан,
    /// контроллер не активируется и курсор остаётся системным (аппаратным). Это позволяет
    /// глобально отключить кастомный курсор, обнулив один SerializeField, без правки кода.
    /// </summary>
    [Serializable]
    public class CursorSettings : SettingsPreset
    {
        /// <summary>Спрайт по умолчанию. <c>null</c> = аппаратный курсор (контроллер не запускается).</summary>
        [BoxGroup("Cursor Settings")] [SerializeField]
        private Sprite cursorDefault;

        /// <summary>Спрайт во время удержания левой кнопки мыши. <c>null</c> = не менять при LMB.</summary>
        [BoxGroup("Cursor Settings")] [SerializeField]
        private Sprite cursorLeftMouseDown;

        /// <summary>Спрайт во время удержания правой кнопки мыши. <c>null</c> = не менять при RMB.</summary>
        [BoxGroup("Cursor Settings")] [SerializeField]
        private Sprite cursorRightMouseDown;

        /// <summary>
        /// Массив hover-вариантов. Индекс в этом массиве соответствует значению поля
        /// <c>index</c> в <see cref="MouseHoverListener"/>. <c>null</c>-элементы массива
        /// для соответствующего индекса трактуются как «использовать default».
        /// </summary>
        [BoxGroup("Cursor Settings")] [SerializeField] [InfoBox("Список вариантов отображения ховера")]
        private Sprite[] cursorOnHover = new Sprite[0];

        /// <inheritdoc cref="cursorDefault"/>
        public Sprite CursorDefault => cursorDefault;

        /// <inheritdoc cref="cursorLeftMouseDown"/>
        public Sprite CursorLeftMouseDown => cursorLeftMouseDown;

        /// <inheritdoc cref="cursorRightMouseDown"/>
        public Sprite CursorRightMouseDown => cursorRightMouseDown;

        /// <inheritdoc cref="cursorOnHover"/>
        public Sprite[] CursorOnHover => cursorOnHover;
    }
}
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
    /// Наборы курсоров задаются по диапазонам разрешения (см. <see cref="CursorResolutionPack"/>):
    /// контроллер выбирает набор с минимальным порогом, покрывающим текущий Screen.height;
    /// если разрешение выше всех порогов — самый крупный набор.
    ///
    /// Логика «нет наборов = нет курсора»: при пустом списке контроллер не активируется
    /// и курсор остаётся системным (аппаратным). Это позволяет глобально отключить
    /// кастомный курсор, очистив список, без правки кода.
    /// </summary>
    [Serializable]
    public class CursorSettings : SettingsPreset
    {
        [BoxGroup("Cursor Settings")]
        [SerializeField]
        [InfoBox("Наборы курсоров по диапазонам разрешения. Пустой список = аппаратный курсор.\n" +
                 "Порядок и длина hover-массивов должны совпадать во всех наборах — " +
                 "индексы MouseHoverListener общие для всех разрешений.")]
        private CursorResolutionPack[] cursorPacks = new CursorResolutionPack[0];

        /// <inheritdoc cref="cursorPacks"/>
        public CursorResolutionPack[] CursorPacks => cursorPacks;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (cursorPacks == null || cursorPacks.Length < 2)
                return;

            var reference = cursorPacks[0]?.Pack?.CursorOnHover?.Length ?? 0;
            for (var i = 1; i < cursorPacks.Length; i++)
            {
                var length = cursorPacks[i]?.Pack?.CursorOnHover?.Length ?? 0;
                if (length == reference)
                    continue;
                Debug.LogWarning(
                    $"[CursorSettings] Длина hover-массива в наборе #{i} ({length}) не совпадает " +
                    $"с набором #0 ({reference}). Индексы MouseHoverListener должны совпадать во всех наборах.",
                    this);
                break;
            }
        }
#endif
    }
}

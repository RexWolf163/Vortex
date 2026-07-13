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
                 "Наборы — по возрастанию MaxScreenHeight (первый = низкое разрешение). Hover-ключи " +
                 "(CursorHoverEntry.Name) общие; отсутствующий в выбранном пакете ключ наследуется " +
                 "от более раннего (к первому), но не наоборот. У каждого пакета может быть свой набор.")]
        private CursorResolutionPack[] cursorPacks = new CursorResolutionPack[0];

        /// <inheritdoc cref="cursorPacks"/>
        public CursorResolutionPack[] CursorPacks => cursorPacks;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (cursorPacks == null || cursorPacks.Length < 2)
                return;

            // Hover-фолбэк идёт от выбранного пакета вниз по массиву к первому, поэтому наборы
            // должны быть отсортированы по возрастанию MaxScreenHeight (первый = низкое разрешение).
            for (var i = 1; i < cursorPacks.Length; i++)
            {
                var prev = cursorPacks[i - 1]?.MaxScreenHeight ?? int.MinValue;
                var cur = cursorPacks[i]?.MaxScreenHeight ?? int.MinValue;
                if (cur > prev)
                    continue;
                Debug.LogWarning(
                    $"[CursorSettings] Наборы должны идти по возрастанию MaxScreenHeight " +
                    $"(#{i} = {cur} не больше #{i - 1} = {prev}). От этого зависит hover-фолбэк.",
                    this);
                break;
            }
        }
#endif
    }
}

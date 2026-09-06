#if USING_VORTEX_CURSOR
using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.CoreAssetsSystem;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Настроечный ассет пакета: список подключаемых драйверов ввода курсора. <see cref="ICoreAsset"/> —
    /// авто-создаётся редактором в <c>Resources/Settings/</c>. Загружается <see cref="CursorInputLoader"/>
    /// на старте (<c>Resources.Load</c>). Порядок драйверов не важен — в контроллере last-source-wins.
    ///
    /// Failfast на загрузке: если модуль включён (<c>USING_VORTEX_CURSOR</c>), а ассета нет или
    /// <see cref="Drivers"/> пуст — <see cref="CursorInputLoader"/> кидает исключение (не тихий отказ).
    /// </summary>
    [CreateAssetMenu(fileName = "InputDriverSet", menuName = "Vortex/Cursor/Input Driver Set")]
    public class InputDriverSet : ScriptableObject, ICoreAsset
    {
        [SerializeReference, HideReferenceObjectPicker]
        [Tooltip("Подключаемые драйверы ввода курсора. Добавляй реализации InputDriver.")]
        private InputDriver[] drivers = Array.Empty<InputDriver>();

        public InputDriver[] Drivers => drivers;
    }
}
#endif

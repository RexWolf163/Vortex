using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Vortex.Unity.UI.VirtualCursorSystem.InputDrivers;

namespace Vortex.Unity.UI.VirtualCursorSystem.UiModule
{
    /// <summary>
    /// UGUI-модуль ввода виртуального курсора — замена <c>InputSystemUIInputModule</c> на EventSystem.
    /// Устройств не читает: указатель UGUI — модель курсора (<see cref="VirtualCursorBaseInput"/>), поэтому hover,
    /// клик, drag и скролл приходят из любого источника, который драйверы сводят в модель (мышь, геймпад, клавиатура),
    /// и работают без физической мыши и без варпа ОС-курсора. Обработка — штатная логика
    /// <see cref="StandaloneInputModule"/> (рейкаст, Enter/Exit, Down/Up/Click, порог drag, scroll).
    ///
    /// <b>Касания.</b> При <see cref="nativeTouch"/> на подходящей платформе касания идут в UGUI напрямую из
    /// Input System — мультитач, каждый палец отдельным указателем; в кадре с касаниями курсорная часть не
    /// обрабатывается. На остальных платформах касания в UGUI не идут — тач-экран работает через драйверы
    /// (например, <see cref="TouchInputDriver"/> в режиме Delta как трекпад).
    ///
    /// <b>Не поддерживается:</b> навигация Selectable (Move/Submit/Cancel в выделенный объект) — её заменяет
    /// фокус-навигация пакета; перо (давление/наклон) и XR-указатели.
    /// </summary>
    [RequireComponent(typeof(VirtualCursorBaseInput))]
    public class VirtualCursorInputModule : StandaloneInputModule
    {
        [SerializeField, Tooltip("Касания идут в UGUI напрямую из Input System (мультитач). Выключено — " +
                                 "тач-экран работает только через драйверы курсора.")]
        private bool nativeTouch = true;

        [SerializeField, ShowIf(nameof(nativeTouch)),
         Tooltip("Платформы прямых касаний. MobileOnly — Android/iOS (на десктопе с тач-экраном палец " +
                 "остаётся трекпадом TouchInputDriver в режиме Delta). All — везде, включая редактор.")]
        private TouchPlatformFilter nativeTouchPlatforms = TouchPlatformFilter.MobileOnly;

        /// <summary>Источник ввода модуля.</summary>
        private VirtualCursorBaseInput _input;

        /// <summary>EnhancedTouch включён этим модулем (снимается симметрично в OnDisable).</summary>
        private bool _touchEnabled;

        /// <summary>Подменить источник ввода модуля моделью курсора.</summary>
        protected override void Awake()
        {
            base.Awake();
            _input = GetComponent<VirtualCursorBaseInput>();
            inputOverride = _input;
        }

        /// <summary>Включить прямые касания, если разрешены на текущей платформе.</summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            _touchEnabled = nativeTouch && nativeTouchPlatforms.Matches(Application.platform);
            if (_touchEnabled)
                EnhancedTouchSupport.Enable();
            _input.NativeTouch = _touchEnabled;
        }

        /// <summary>Выключить прямые касания, если их включал этот модуль.</summary>
        protected override void OnDisable()
        {
            if (_touchEnabled)
            {
                EnhancedTouchSupport.Disable();
                _touchEnabled = false;
            }

            _input.NativeTouch = false;
            base.OnDisable();
        }

        /// <summary>Штатная обработка кадра, затем сброс фронтов кнопок и скролла кадра.</summary>
        public override void Process()
        {
            try
            {
                base.Process();
            }
            finally
            {
                _input.EndFrame();
            }
        }
    }
}

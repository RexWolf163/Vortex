using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Кормит виртуальный UI-указатель (<see cref="VirtualUiPointer"/>) из модели: позиция ← ScreenPosition,
    /// кнопки/скролл ← маска действий. <see cref="LateUpdate"/> — до обработки InputSystemUIInputModule.
    /// Родной UGUI (Button/ScrollRect/side-buttons) читает этот девайс. Создаёт/удаляет устройство.
    /// Маппинг фиксированный: Action1→left, 2→right, 3→middle, 4→back, 5→forward, 6/7→scroll ±.
    /// </summary>
    public class UiPointerFeeder : MonoBehaviour
    {
        [SerializeField, Min(1f), Tooltip("Величина одного тика скролла (Action6/Action7).")]
        private float scrollTick = 120f;

        private VirtualUiPointer _pointer;

        private void OnEnable()
        {
            VirtualUiPointer.EnsureRegistered();
            if (_pointer == null || !_pointer.added)
                _pointer = InputSystem.AddDevice<VirtualUiPointer>();
        }

        private void OnDisable()
        {
            if (_pointer != null && _pointer.added)
                InputSystem.RemoveDevice(_pointer);
            _pointer = null;
        }

        private void LateUpdate()
        {
            var data = VirtualCursorBus.Data;
            if (data == null || _pointer == null)
                return;

            var mask = data.Actions.Value;
            var state = new MouseState { position = data.ScreenPosition.Value };

            if (mask.IsActive(PointerAction.Action1)) state = state.WithButton(MouseButton.Left);
            if (mask.IsActive(PointerAction.Action2)) state = state.WithButton(MouseButton.Right);
            if (mask.IsActive(PointerAction.Action3)) state = state.WithButton(MouseButton.Middle);
            if (mask.IsActive(PointerAction.Action4)) state = state.WithButton(MouseButton.Back);
            if (mask.IsActive(PointerAction.Action5)) state = state.WithButton(MouseButton.Forward);

            var scrollY = (mask.IsActive(PointerAction.Action6) ? 1f : 0f) - (mask.IsActive(PointerAction.Action7) ? 1f : 0f);
            state.scroll = new Vector2(0f, scrollY * scrollTick);

            InputSystem.QueueStateEvent(_pointer, state);
        }
    }
}

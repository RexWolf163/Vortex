using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Model;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Зона перехвата мыши. Пока компонент включён, клапан ловит клавиши мыши (кнопки, колесо, Ctrl+ЛКМ —
    /// по триггеру) только при указателе над ним. Вне зоны мышь фильтруется: нажатие не ловится и проходит в UI
    /// как обычно — кнопка «Отмена» работает. Без компонента в сцене мышь ловится везде.
    ///
    /// Объект должен принимать лучи UI (Graphic с включённым raycastTarget) и не содержать кнопок: клик над
    /// зоной клапан проглатывает.
    /// </summary>
    public class CaptureMouseHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private bool _hovered;

        private void OnEnable() => RebindBus.Controller.AddCaptureFilter(Accepts);

        private void OnDisable()
        {
            RebindBus.Controller.RemoveCaptureFilter(Accepts);
            _hovered = false;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        private bool Accepts(BindingValue candidate) => _hovered || !IsMouse(candidate.Trigger);

        private static bool IsMouse(string path)
        {
            try
            {
                var layout = InputControlPath.TryGetDeviceLayout(path);
                return !string.IsNullOrEmpty(layout) && InputSystem.IsFirstLayoutBasedOnSecond(layout, "Mouse");
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

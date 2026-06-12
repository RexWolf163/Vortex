using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vortex.Unity.UI.CursorSystem
{
    public class MouseHoverListener : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, ValueDropdown("GetList")]
        private int index = -1;

        public void OnPointerEnter(PointerEventData eventData)
        {
            CursorController.OnHover(index);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CursorController.OnUnHover(index);
        }

        private void OnDisable()
        {
            OnPointerExit(null);
        }

#if UNITY_EDITOR
        private ValueDropdownList<int> GetList()
        {
            var result = new ValueDropdownList<int> { new ValueDropdownItem<int>("[NONE]", -1) };
            var settings = Resources.LoadAll<CursorSettings>("");
            if (settings == null || settings.Length == 0)
                return result;

            var list = settings[0].CursorOnHover;
            if (list == null || list.Length == 0)
            {
                index = -1;
                return result;
            }
            for (var i = 0; i < list.Length; i++)
            {
                var sprite = list[i];
                result.Add(new ValueDropdownItem<int>(sprite.name, i));
            }

            return result;
        }
#endif
    }
}
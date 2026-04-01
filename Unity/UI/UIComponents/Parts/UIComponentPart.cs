#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.UIComponents.Parts
{
    public abstract class UIComponentPart : MonoBehaviour
    {
        [InfoBubble(
            "Editor-параметр. Если включить, инспектор перестанет менять настройки rectTransform под заполнение контейнера")]
        [SerializeField]
        private bool onlyNativeSize = false;
#if UNITY_EDITOR
        [OnInspectorInit]
        protected void OnInspector()
        {
            if (onlyNativeSize)
                return;
            var rect = transform.GetComponent<RectTransform>();
            if (rect == null)
                return;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchorMin = Vector2.zero;
        }
#endif
    }
}
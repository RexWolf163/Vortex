using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;
using Vortex.Unity.UI.UIComponents.Attributes;
using Vortex.Unity.UI.UIComponents.Parts;

namespace Vortex.Unity.Components.Misc.LocalizationSystem
{
    /// <summary>
    /// Компонент для линковки методов внешних классов
    /// </summary>
    [RequireComponent(typeof(UIComponent))]
    public class SetActionComponent : MonoBehaviour
    {
        /// <summary>
        /// Линки на onClick
        /// </summary>
        [SerializeField] private UnityEvent events;

        [SerializeField, AutoLink] private UIComponent uiComponent;

        [SerializeField, UIComponentLink(typeof(UIComponentButton), "uiComponent"), TitleGroup("Link")]
        private int position = -1;

        private void OnEnable()
        {
            if (uiComponent == null)
            {
                Debug.LogError($"Target for SetText:{name} component is missing.");
                return;
            }

            if (position < 0)
                uiComponent.SetAction(events.Invoke);
            else
                uiComponent.SetAction(events.Invoke, position);
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                if (uiComponent == null)
                    return;
#endif
            if (position < 0)
                uiComponent.SetAction(null);
            else
                uiComponent.SetAction(null, position);
        }
    }
}
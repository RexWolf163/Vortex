using UnityEngine;
using Vortex.Unity.UI.UIComponents;
using Vortex.Unity.UIProviderSystem.View;
using UIProvider = Vortex.Core.UIProviderSystem.Bus.UIProvider;

namespace Vortex.Unity.UIProviderSystem.Handlers
{
    public class CallUIClose : MonoBehaviour
    {
        [SerializeField] protected UserInterface ui;

        [SerializeField] private UIComponent uiComponent;

        private void Awake() => uiComponent?.SetAction(CloseUI);

        private void CloseUI()
        {
            UIProvider.Close(ui.GetId());
        }
    }
}
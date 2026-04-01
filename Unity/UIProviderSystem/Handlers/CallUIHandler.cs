using System;
using System.Linq;
using UnityEngine;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.UI.UIComponents;
using UIProvider = Vortex.Core.UIProviderSystem.Bus.UIProvider;

namespace Vortex.Unity.UIProviderSystem.Handlers
{
    public class CallUIHandler : MonoBehaviour
    {
        [SerializeField, DbRecord(typeof(UserInterfaceData))]
        protected string uiId;

        /// <summary>
        /// Закрывать вместо открытия
        /// </summary>
        [SerializeField] protected bool closeUI;

        [SerializeField] private UIComponent uiComponent;

        private Type type;

        private void Awake()
        {
            type = Type.GetType(uiId);
            uiComponent?.SetAction(CallUI);
        }

        public virtual void CallUI()
        {
            if (closeUI)
                CloseUI();
            else
                OpenUI();
        }

        public virtual void ToggleUI()
        {
            if (UIProvider.GetOpenedUIs().Any(u => u.GuidPreset == uiId))
                CloseUI();
            else
                OpenUI();
        }

        private void CloseUI()
        {
            UIProvider.Close(uiId);
        }

        private void OpenUI()
        {
            UIProvider.Open(uiId);
        }
    }
}
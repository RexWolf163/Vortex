using System.Globalization;
using System.Linq;
using UnityEngine;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Unity.LocalizationSystem;
using Vortex.Unity.UI.TweenerSystem;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.UIs.SaveLoad.Views
{
    /// <summary>
    /// Всплывашка с информацией о новом сейве
    /// </summary>
    public class SaveCompleteView : MonoBehaviour
    {
        [SerializeField] private TweenerHub[] tweeners;

        [SerializeField] private UIComponent componentName;
        [SerializeField] private UIComponent componentTimestamp;

        [SerializeField] private string timestampPattern = "dd/MM/yyyy ' | ' HH:mm";
        [SerializeField, LocalizationKey] private string autoSavePattern;

        [SerializeField, LocalizationKey] private string manualSavePattern;

        private void OnEnable()
        {
            SaveController.OnSaveComplete += Refresh;
            foreach (var tweener in tweeners)
                tweener.Back(true);
        }

        private void OnDisable()
        {
            SaveController.OnSaveComplete -= Refresh;
        }

        private void Refresh()
        {
            foreach (var tweener in tweeners)
            {
                tweener.Back(true);
                tweener.Forward();
            }

            var dict = SaveController.GetIndex();
            if (dict == null || dict.Count == 0)
                return;
            var lastSave = dict.OrderByDescending(p => p.Value.UnixTimestamp).First().Value;

            componentName?.SetText(lastSave.GetDisplayName(autoSavePattern, manualSavePattern));
            componentTimestamp?.SetText(lastSave.Date.ToLocalTime().ToString(timestampPattern, CultureInfo.InvariantCulture));
        }
    }
}
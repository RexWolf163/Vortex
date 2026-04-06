using System.Globalization;
using System.Linq;
using UnityEngine;
using Vortex.Core.LocalizationSystem;
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

            var saveAr = lastSave.Name.Split('_');
            if (saveAr.Length < 2
                || !(saveAr[0].StartsWith(SavingSystemConstants.AutoName)
                     || saveAr[0].StartsWith(SavingSystemConstants.ManualName)))
            {
                componentName?.SetText(lastSave.Name);
                return;
            }

            var pattern = manualSavePattern;
            var index = saveAr[1];
            if (SavingSystemConstants.AutoName.Equals(saveAr[0]))
                pattern = autoSavePattern;
            var labelText = string.Format(pattern.Translate(), index);
            if (saveAr.Length > 2)
                labelText = $"{labelText}: {string.Join('_', saveAr[2..])}";
            componentName?.SetText(labelText);


            componentName?.SetText(lastSave.Name);
            componentTimestamp?.SetText(lastSave.Date.ToString(timestampPattern, CultureInfo.InvariantCulture));
        }
    }
}
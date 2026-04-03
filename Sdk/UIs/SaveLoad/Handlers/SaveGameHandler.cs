using UnityEngine;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.UIs.SaveLoad.Handlers
{
    /// <summary>
    /// Хэндлер запуска процесса сохранения
    /// </summary>
    public class SaveGameHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private UIComponent uiComponent;

        private void OnEnable()
        {
            uiComponent.SetAction(SaveGame);
        }

        private void OnDisable()
        {
            uiComponent.SetAction(null);
        }

        private void SaveGame() => SaveController.Save(GetSaveName());

        private string GetSaveName() => $"{SavingSystemConstants.ManualName}_{SaveController.GetNumberLastSave() + 1}";
    }
}
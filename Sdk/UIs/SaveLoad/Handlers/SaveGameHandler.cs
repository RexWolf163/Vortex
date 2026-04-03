using Cysharp.Threading.Tasks;
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
            uiComponent.SetAction(() => SaveGame().Forget(Debug.LogException));
        }

        private void OnDisable()
        {
            uiComponent.SetAction(null);
        }

        private async UniTask SaveGame()
        {
            var guid = await SaveController.Save(GetSaveName());
            if (guid == null)
            {
                Debug.LogError("[SaveGameHandler] Couldn't save game");
                return;
            }

            SavePreviewController.SavePreview(CameraCaptureHandler.Capture(), guid);
        }

        private string GetSaveName() => $"{SavingSystemConstants.ManualName}_{SaveController.GetNumberLastSave() + 1}";
    }
}
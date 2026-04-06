using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
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
        [SerializeField] private UIComponent uiComponent;

        private void OnEnable()
        {
            uiComponent?.SetAction(() => SaveGame().Forget(Debug.LogException));
        }

        private void OnDisable()
        {
            uiComponent?.SetAction(null);
        }

        /// <summary>
        /// Запуск сохранения игры
        /// </summary>
        public void Save() => SaveGame().Forget(Debug.LogException);

        [Button]
        private async UniTask SaveGame()
        {
            await UniTask.WaitForEndOfFrame(this);
            var texture = CameraCaptureHandler.Capture();
            var guid = await SaveController.Save(GetSaveName());
            if (guid == null)
            {
                Debug.LogError("[SaveGameHandler] Couldn't save game");
                return;
            }

            SavePreviewController.SavePreview(texture, guid);
            Destroy(texture);
        }

        private string GetSaveName() => $"{SavingSystemConstants.ManualName}_{SaveController.GetNumberLastSave() + 1}";
    }
}
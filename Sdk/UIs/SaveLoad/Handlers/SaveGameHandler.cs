using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.SaveSystem.Bus;
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
            // Повторный клик, пока свой сейв в очереди: новый сейв ни с чем не сливается, а имя manual_N
            // вычисляется до записи — дал бы второй сейв с тем же номером
            if (_isSaving)
                return;

            _isSaving = true;
            try
            {
                // Конец кадра — для снимка превью: SavePreviewController снимает экран на запросе сохранения
                await UniTask.WaitForEndOfFrame(this);
                var guid = await SaveKindController.Save(SavingSystemConstants.ManualName, GetSaveName());
                if (guid == null)
                    Debug.LogError("[SaveGameHandler] Couldn't save game", this);
            }
            finally
            {
                _isSaving = false;
            }
        }

        /// <summary>
        /// Свой сейв запрошен и ещё не завершён
        /// </summary>
        private bool _isSaving;

        private static string GetSaveName() =>
            $"{SavingSystemConstants.ManualName}_{SaveController.GetNumberLastSave() + 1}";
    }
}
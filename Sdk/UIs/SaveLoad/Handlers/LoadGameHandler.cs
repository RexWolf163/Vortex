using UnityEngine;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.UIs.SaveLoad.Models;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.UIs.SaveLoad.Handlers
{
    /// <summary>
    /// Хэндлер запуска процесса сохранения
    /// </summary>
    public class LoadGameHandler : MonoBehaviour
    {
        /// <summary>
        /// Ссылка на класс-хранилище нужной модели данных
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        [SerializeField] private UIComponent button;

        private void OnEnable() => button?.SetAction(LoadGame);

        private void OnDisable() => button?.SetAction(null);

        public void LoadGame()
        {
            var saveSlot = Storage.GetData<SaveSlotData>();
            if (saveSlot == null)
            {
                Debug.LogError("[LoadGameHandler] No save slot found");
                return;
            }

            SaveController.Load(saveSlot.Guid);
        }
    }
}
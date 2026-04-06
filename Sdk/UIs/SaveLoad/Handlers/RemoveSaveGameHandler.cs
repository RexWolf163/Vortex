using UnityEngine;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.UIs.SaveLoad.Models;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.UIs.SaveLoad.Handlers
{
    /// <summary>
    /// Хэндлер запуска процесса удаления
    /// </summary>
    public class RemoveSaveGameHandler : MonoBehaviour
    {
        /// <summary>
        /// Ссылка на класс-хранилище нужной модели данных
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        [SerializeField] private UIComponent button;

        /// <summary>
        /// Модель данных из хранилища
        /// </summary>
        private SaveSlotData _data;

        private void OnEnable()
        {
            Storage.OnUpdateLink += UpdateLink;
            Init();
        }

        private void OnDisable()
        {
            DeInit();
            Storage.OnUpdateLink -= UpdateLink;
        }

        private void Init()
        {
            _data = Storage.GetData<SaveSlotData>();
            if (_data == null)
                return;
            button?.SetAction(RemoveSave);
        }

        private void DeInit()
        {
            _data = null;
            button?.SetAction(null);
        }

        private void UpdateLink()
        {
            DeInit();
            Init();
        }

        public void RemoveSave() => SaveController.Remove(_data.Guid);
    }
}
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.AudioLocalizationSystem.Handlers
{
    /// <summary>
    /// Хэндлер для воспроизведения звукового аналога для текста из IDataStorage
    /// </summary>
    public class AudioLocaleHandler : MonoBehaviour
    {
        /// <summary>
        /// Ссылка на класс-хранилище нужной модели данных
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        /// <summary>
        /// Модель данных из хранилища
        /// </summary>
        private StringData _data;

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
            _data = Storage.GetData<StringData>();
            if (_data == null)
                return;
            _data.OnUpdate += OnDataUpdated;
            OnDataUpdated(_data.Value);
        }

        private void DeInit()
        {
            if (_data != null)
                _data.OnUpdate -= OnDataUpdated;
            _data = null;
        }

        private void UpdateLink()
        {
            DeInit();
            Init();
        }

        /// <summary>
        /// Модель данных изменилась
        /// </summary>
        private void OnDataUpdated(string s)
        {
            if (s.IsNullOrWhitespace())
                return;
            AudioLocalizationController.PlayForText(s);
        }
    }
}
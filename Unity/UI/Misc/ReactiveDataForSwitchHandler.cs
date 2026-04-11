using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UI.Misc
{
    [RequireComponent(typeof(UIStateSwitcher))]
    public class ReactiveDataForSwitchHandler : MonoBehaviour
    {
        /// <summary>
        /// Ссылка на класс-хранилище нужной модели данных
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        [SerializeField, AutoLink] private UIStateSwitcher switcher;

        /// <summary>
        /// Модель данных из хранилища
        /// Приоритет имеет IntParametr
        /// </summary>
        private BoolData _boolData;

        /// <summary>
        /// Модель данных из хранилища
        /// Приоритет имеет IntParametr
        /// </summary>
        private IntData _intData;

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
            _intData = Storage.GetData<IntData>();
            if (_intData != null)
            {
                _intData.OnUpdate += OnIntDataUpdated;
                OnIntDataUpdated(_intData);
                return;
            }

            _boolData = Storage.GetData<BoolData>();
            if (_boolData != null)
            {
                _boolData.OnUpdate += OnBoolDataUpdated;
                OnBoolDataUpdated(_boolData);
            }
        }

        private void DeInit()
        {
            if (_intData != null)
                _intData.OnUpdate -= OnIntDataUpdated;
            _intData = null;
            if (_boolData != null)
                _boolData.OnUpdate -= OnBoolDataUpdated;
            _boolData = null;
        }

        private void UpdateLink()
        {
            DeInit();
            Init();
        }

        /// <summary>
        /// Модель данных изменилась
        /// </summary>
        private void OnIntDataUpdated(int value)
        {
            value = Mathf.Clamp(value, 0, switcher.States.Length -1);
            switcher.Set(value);
        }

        /// <summary>
        /// Модель данных изменилась
        /// </summary>
        private void OnBoolDataUpdated(bool value)
        {
            switcher.Set(value ? 1 : 0);
        }
    }
}
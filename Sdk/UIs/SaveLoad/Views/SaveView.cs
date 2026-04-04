using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.UIs.SaveLoad.Models;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.LocalizationSystem;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.UIs.SaveLoad.Views
{
    /// <summary>
    /// Представление сохранения
    /// </summary>
    public class SaveView : MonoBehaviour
    {
        /// <summary>
        /// Ссылка на класс-хранилище нужной модели данных
        /// </summary>
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        /// <summary>
        /// Компонент с кнопкой
        /// </summary>
        [SerializeField, AutoLink] private UIComponent slotButton;

        /// <summary>
        /// Компонент с названием сейва.
        /// Если структура под индекс (auto_4), то обрабатываться будет как индекс
        /// </summary>
        [SerializeField] private UIComponent slotName;

        /// <summary>
        /// Компонент с меткой времени
        /// </summary>
        [SerializeField] private UIComponent timestamp;

        [SerializeField] private RawImage slotImage;

        [SerializeField] private string timestampPattern = "dd/MM/yyyy ' | ' HH:mm";

        [SerializeField, LocalizationKey] private string autoSavePattern;

        [SerializeField, LocalizationKey] private string manualSavePattern;

        /// <summary>
        /// Модель данных из хранилища
        /// </summary>
        private SaveSlotData _data;

        /// <summary>
        /// GUID выбранного сейва (в фокусе)
        /// </summary>
        private StringData _focused;

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

            if (slotImage != null)
            {
                slotImage.texture = _data.Preview;
                slotImage.gameObject.SetActive(true);
            }

            if (slotButton != null)
            {
                _focused = Storage.GetData<StringData>();
                _focused.OnUpdateData += RefreshFocus;
                var callback = Storage.GetData<Action<string>>();
                slotButton.SetAction(() => callback?.Invoke(_data.Guid));
                RefreshFocus();
            }

            timestamp?.SetText(_data.Summary.Date.ToString(timestampPattern, CultureInfo.InvariantCulture));


            var saveAr = _data.Summary.Name.Split('_');
            if (saveAr.Length < 2
                || !(saveAr[0].StartsWith(SavingSystemConstants.AutoName)
                     || saveAr[0].StartsWith(SavingSystemConstants.ManualName)))
            {
                slotName?.SetText(_data.Summary.Name);
                return;
            }

            var pattern = manualSavePattern;
            var index = saveAr[1];
            if (SavingSystemConstants.AutoName.Equals(saveAr[0]))
                pattern = autoSavePattern;
            var labelText = string.Format(pattern.Translate(), index);
            if (saveAr.Length > 2)
                labelText = $"{labelText}: {string.Join('_', saveAr[2..])}";
            slotName?.SetText(labelText);
        }

        private void RefreshFocus()
        {
            //Переключение состояния "в фокусе"
            var b = _focused != null && _focused.Value.Equals(_data.Guid);
            slotButton.SetSwitcher(b ? SwitcherState.On : SwitcherState.Off);
        }

        private void DeInit()
        {
            if (_focused != null)
                _focused.OnUpdateData -= RefreshFocus;
            _data = null;
            slotName?.SetText("");
            if (slotImage != null)
            {
                slotImage.texture = null;
                slotImage.gameObject.SetActive(false);
            }

            slotButton?.SetAction(null);
        }

        private void UpdateLink()
        {
            DeInit();
            Init();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (slotImage == null)
                slotImage = GetComponentInChildren<RawImage>();
        }

#endif
    }
}
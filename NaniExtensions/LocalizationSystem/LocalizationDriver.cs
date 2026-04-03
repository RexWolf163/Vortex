using System;
using System.Collections.Generic;
using Naninovel;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Core.System.Abstractions;
using UniTask = Cysharp.Threading.Tasks.UniTask;

namespace Vortex.NaniExtensions.LocalizationSystem
{
    public partial class LocalizationDriver : Singleton<LocalizationDriver>, IDriver, IChanneledDriver
    {
        private const string Path = "Localization";
        private const string SaveSlot = "AppLanguage";
        private const string SaveSlotChannel = "AppLanguage{0}";

        private static Dictionary<string, string> _localeData;

        /// <summary>
        /// Событие смены языка локали
        /// </summary>
        public event Action OnLocalizationChanged;

        /// <summary>
        /// Событие вызывается после завершения асинхронной загрузки данных
        /// </summary>
        public event Action OnInit;

        private static void CallOnInit() => Instance.OnInit?.Invoke();

        public void Init()
        {
            //OnInit вызывается после завершения асинхронной загрузки данных
        }

        public void Destroy()
        {
        }

        /// <summary>
        /// Связь индекса с данными драйвера
        /// </summary>
        /// <param name="index"></param>
        public void SetIndex(Dictionary<string, string> index) => _localeData = index;

        /// <summary>
        /// Получить дефолтный язык для приложения (при инициации)
        /// </summary>
        /// <returns></returns>
        public string GetDefaultLanguage()
        {
            var lang = Application.systemLanguage.ToString();
            if (PlayerPrefs.HasKey(SaveSlot))
            {
                var result = PlayerPrefs.GetString(SaveSlot);
                if (!result.IsNullOrWhitespace())
                {
                    lang = result;
                    var index = _resource.langsKeys.IndexOf(lang);
                    if (index >= 0)
                        return _resource.langsKeys[index];
                }
            }

            var i = _resource.langs.IndexOf(lang);
            if (i >= 0)
                return _resource.langsKeys[i];
            var defaultLang = _resource.GetDefaultLanguage();
            if (defaultLang.IsNullOrWhitespace() && _resource.langsKeys.Length > 0)
                defaultLang = _resource.langsKeys[0];
            return defaultLang;
        }

        /// <summary>
        /// Получить дефолтный язык настроек локализации
        /// </summary>
        /// <returns></returns>
        internal string GetPresetDefaultLanguage()
        {
            var defaultLang = _resource.GetDefaultLanguage();
            if (defaultLang.IsNullOrWhitespace() && _resource.langsKeys.Length > 0)
                defaultLang = _resource.langsKeys[0];
            return defaultLang;
        }

        /// <summary>
        /// Установить язык для приложения
        /// </summary>
        /// <param name="language"></param>
        public async UniTask SetLanguage(string language)
        {
            if (PlayerPrefs.HasKey(SaveSlot) && PlayerPrefs.GetString(SaveSlot) == language)
                return;

            PlayerPrefs.SetString(SaveSlot, language);
            await Loader.RunAlone(this);
            CallOnLocalizationChanged();
        }

        private static void CallOnLocalizationChanged() => Instance.OnLocalizationChanged?.Invoke();

        /// <summary>
        /// Получить перечень зафиксированных языков
        /// </summary>
        /// <returns></returns>
        public string[] GetLanguages() =>
            _resource.langsKeys;

        public async UniTask SetChannelLanguage(byte channel, string language)
        {
            var chKey = string.Format(SaveSlotChannel, channel);
            if (PlayerPrefs.HasKey(chKey) && PlayerPrefs.GetString(chKey) == language)
                return;
            PlayerPrefs.SetString(chKey, language);
            if (channel == 0)
                await Loader.RunAlone(this);
            CallOnLocalizationChanged();
        }

        public string GetChannelLanguage(byte channel)
        {
            var lang = Application.systemLanguage.ToString();
            var chKey = string.Format(SaveSlotChannel, channel);
            //Ищем в сейве канала
            if (PlayerPrefs.HasKey(chKey))
            {
                var result = PlayerPrefs.GetString(chKey);
                if (!result.IsNullOrWhitespace())
                {
                    lang = result;
                    var index = _resource.langsKeys.IndexOf(lang);
                    if (index >= 0)
                        return _resource.langsKeys[index];
                }
            }

            //Ищем как дефолтный сейв
            if (PlayerPrefs.HasKey(SaveSlot))
            {
                var result = PlayerPrefs.GetString(SaveSlot);
                if (!result.IsNullOrWhitespace())
                {
                    lang = result;
                    var index = _resource.langsKeys.IndexOf(lang);
                    if (index >= 0)
                        return _resource.langsKeys[index];
                }
            }

            //Пытаемся получить как системный язык
            var i = _resource.langs.IndexOf(lang);
            if (i >= 0)
                return _resource.langsKeys[i];

            //Пытаемся получить дефолтный язык
            var defaultLang = _resource.GetDefaultLanguage();
            if (defaultLang.IsNullOrWhitespace() && _resource.langsKeys.Length > 0)
                defaultLang = _resource.langsKeys[0];
            return defaultLang;
        }
    }
}
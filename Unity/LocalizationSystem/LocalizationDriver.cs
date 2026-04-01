using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.System.Abstractions;

namespace Vortex.Unity.LocalizationSystem
{
    public partial class LocalizationDriver : Singleton<LocalizationDriver>, IDriver
    {
        private const string Path = "Localization";
        private const string SaveSlot = "AppLanguage";

        private static Dictionary<string, string> _localeData;

        private string[] _cashedLangs;

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
                    lang = result;
            }

            return GetLanguages().Contains(lang) ? lang : GetLanguages()[0];
        }

        /// <summary>
        /// Установить язык для приложения
        /// </summary>
        /// <param name="language"></param>
        public async UniTask SetLanguage(string language)
        {
            PlayerPrefs.SetString(SaveSlot, language);
            await Loader.RunAlone(this);
            CallOnLocalizationChanged();
        }

        private static void CallOnLocalizationChanged() => Instance.OnLocalizationChanged?.Invoke();

        /// <summary>
        /// Получить перечень зафиксированных языков
        /// </summary>
        /// <returns></returns>
        public string[] GetLanguages()
        {
            if (_cashedLangs != null && _cashedLangs.Length != 0)
                return _cashedLangs;
            var langs = _resource.langs;
            var result = new List<string>();

            //Проверяем наличие ассоциаций для языка в системных
            foreach (var lang in langs)
            {
                if (!Enum.TryParse(typeof(SystemLanguage), lang, true, out var temp))
                    Debug.LogError($"Language {lang} is not supported");
                if (temp == null)
                    continue;
                result.Add(((SystemLanguage)temp).ToString());
            }

            _cashedLangs = result.ToArray();
            return _cashedLangs;
        }
    }
}
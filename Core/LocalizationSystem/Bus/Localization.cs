using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.LocalizationSystem.Bus
{
    public partial class Localization : SystemController<Localization, IDriver>
    {
        /// <summary>
        /// Событие смены языка локали
        /// </summary>
        public static event Action OnLocalizationChanged;

        /// <summary>
        /// Значение текущей локали
        /// </summary>
        private static string _currentLanguage;

        /// <summary>
        /// Значение текущей локали
        /// Lazy-инициализация
        /// </summary>
        private static string CurrentLanguage => _currentLanguage ??= Driver.GetDefaultLanguage();

        /// <summary>
        /// Индекс локализованных фрагментов
        /// </summary>
        private static readonly Dictionary<string, string> Index = new(StringComparer.OrdinalIgnoreCase);

        protected override void OnDriverConnect()
        {
            Driver.SetIndex(Index);
            Driver.OnLocalizationChanged += CallOnLocalization;
        }

        protected override void OnDriverDisconnect()
        {
            Driver.OnLocalizationChanged -= CallOnLocalization;
        }

        /// <summary>
        /// Узнать текущую локаль приложения 
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentLanguage()
        {
            if (CurrentLanguage.IsNullOrWhitespace())
                SetCurrentLanguage(Driver.GetDefaultLanguage()); //дефолтный язык
            return CurrentLanguage;
        }

        /// <summary>
        /// Установить язык для приложения
        /// </summary>
        /// <param name="language"></param>
        public static void SetCurrentLanguage(string language)
        {
            _currentLanguage = language;
            Driver.SetLanguage(language);
        }

        /// <summary>
        /// Возвращает ассоциацию с ключом в текущей локали приложения
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetTranslate(string key) => Index.ContainsKey(key) ? Index[key] : $"##!{key}!##";

        /// <summary>
        /// Проверка есть ли такой ключ в реестре
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool HasTranslate(string key) => Index.ContainsKey(key);

        private static void CallOnLocalization() => OnLocalizationChanged?.Invoke();
    }
}
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.LocalizationSystem
{
    public interface IDriver : ISystemDriver
    {
        /// <summary>
        /// Получить дефолтный язык для приложения (при инициации)
        /// </summary>
        /// <returns></returns>
        public string GetDefaultLanguage();

        /// <summary>
        /// Установить язык для приложения
        /// </summary>
        /// <param name="language"></param>
        public UniTask SetLanguage(string language);

        /// <summary>
        /// Связь индекса с данными драйвера
        /// </summary>
        /// <param name="index"></param>
        public void SetIndex(Dictionary<string, string> index);

        /// <summary>
        /// Событие смены языка локали
        /// </summary>
        public event Action OnLocalizationChanged;


        public string[] GetLanguages();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Core.System.ProcessInfo;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.NaniExtensions.LocalizationSystem.Presets;
using LanguageData = Vortex.Unity.LocalizationSystem.Presets.LanguageData;

namespace Vortex.NaniExtensions.LocalizationSystem
{
    public partial class LocalizationDriver : IProcess
    {
        private ProcessData _processData;

        private static LocalizationPreset _resource;

        public ProcessData GetProcessInfo() => _processData;

        [RuntimeInitializeOnLoadMethod]
        private static void Register()
        {
            if (!Localization.SetDriver(Instance))
            {
                Dispose();
                return;
            }

            var resources = Resources.LoadAll<LocalizationPreset>(Path);
            if (resources == null || resources.Length == 0)
            {
                Debug.LogError("[Localization] Localization Preset not found]");
                return;
            }

            _resource = resources[0];
            Loader.Register(Instance);
        }

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            _localeData.Clear();
            var size = _resource.localeData.Length;
            _processData = new ProcessData()
            {
                Name = "Localization Data",
                Progress = 0,
                Size = size
            };

            var currentLanguage = Localization.GetCurrentLanguage();

            for (var i = 0; i < _resource.localeData.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await UniTask.CompletedTask;
                    return;
                }

                var data = _resource.localeData[i];
                var translateData = data.Texts.FirstOrDefault(x => x.Language == currentLanguage);
                if (translateData.Language.IsNullOrWhitespace())
                    translateData = data.Texts[0];
                _localeData.AddNew(data.Key, translateData.Text);

                if (i % 20 == 0)
                    await UniTask.Yield();
            }

            TimeController.Call(CallOnInit, this);
            await UniTask.CompletedTask;
        }

        public Type[] WaitingFor() => null;

        /// <summary>
        /// Собрать индекс-копию под указанный язык из уже загруженного пресета (синхронно, без IO).
        /// Тот же источник и формат, что у основного индекса в <see cref="RunAsync"/>, но с падением
        /// на дефолтный язык по ключу (частичная локаль → дефолт, не «первый попавшийся» язык).
        /// </summary>
        public Dictionary<string, string> GetLanguagePack(string language)
        {
            var pack = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_resource?.localeData == null)
                return pack;

            var fallback = GetDefaultLanguage();
            foreach (var data in _resource.localeData)
            {
                var translateData = Pick(data.Texts, language);
                if (translateData.Language.IsNullOrWhitespace())
                    translateData = Pick(data.Texts, fallback);
                if (translateData.Language.IsNullOrWhitespace() && data.Texts.Count > 0)
                    translateData = data.Texts[0];

                pack[data.Key] = translateData.Text;
            }

            return pack;
        }

        private static LanguageData Pick(IReadOnlyList<LanguageData> texts, string language) =>
            texts.FirstOrDefault(x => x.Language == language);
    }
}
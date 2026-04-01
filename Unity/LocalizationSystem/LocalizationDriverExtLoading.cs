using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Core.System.ProcessInfo;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.LocalizationSystem.Presets;

namespace Vortex.Unity.LocalizationSystem
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

            //TODO - перенести в addressable ассеты
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
    }
}
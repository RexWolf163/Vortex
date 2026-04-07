using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.ProcessInfo;
using Vortex.Sdk.AudioLocalizationSystem.Model;

namespace Vortex.Sdk.AudioLocalizationSystem
{
    public class AudioLocalizationController : Singleton<AudioLocalizationController>, IProcess
    {
        private static readonly Dictionary<string, AudioLocaleData> Index = new();

        private const string SaveKey = "AudioLocalization";

        private static readonly ProcessData ProcessData = new(name: SaveKey);

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            Loader.Register(Instance);
        }

        /// <summary>
        /// Запустить воспроизведение звуковой дорожки для указанного текста
        /// </summary>
        /// <param name="text"></param>
        public static void PlayForText(string text)
        {
            if (!Index.TryGetValue(text, out var data))
                return;
            var clip = data.GetSoundClip();
            if (clip == null)
                return;
            var channel = clip.Channel;
            AudioController.StopAllSounds(channel.Name);
            AudioController.PlaySound(clip);
        }

        public ProcessData GetProcessInfo() => ProcessData;

        public async UniTask RunAsync(CancellationToken cancellationToken)
        {
            Index.Clear();
            var list = Database.GetRecords<AudioLocaleData>();
            ProcessData.Size = list.Length;
            var batch = 0;
            foreach (var localeData in list)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;
                ProcessData.Progress++;
                Index.AddNew(localeData.TextGuid, localeData);
                if (++batch > 50)
                {
                    batch = 0;
                    await UniTask.Yield(cancellationToken);
                }
            }
        }

        public Type[] WaitingFor() => new[] { typeof(Database) };
    }
}
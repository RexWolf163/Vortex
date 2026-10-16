using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Core.AudioSystem.Model;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.ProcessInfo;
using Vortex.Sdk.AudioLocalizationSystemV2.Model;
using Vortex.Unity.AudioSystem.Model;

namespace Vortex.Sdk.AudioLocalizationSystemV2
{
    public class AudioLocalizationController : Singleton<AudioLocalizationController>, IProcess
    {
        private static readonly Dictionary<string, AudioLocaleData> Index = new();

        private const string SaveKey = "AudioLocalizationV2";

        private static readonly ProcessData ProcessData = new(name: SaveKey);

        /// <summary>
        /// Хэндл реплики, клип которой сейчас грузится. Повторный запрос той же реплики во время
        /// загрузки игнорируется: она и так вот-вот заиграет, а второй LoadAsync на том же хэндле
        /// (он же владелец в кэше) сорвал бы владение первого.
        /// </summary>
        private static AssetHandle<AudioClip> _pending;

        /// <summary>
        /// Отмена загрузки реплики, вытесненной более новым запросом.
        /// </summary>
        private static CancellationTokenSource _pendingCts;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            Loader.Register(Instance);
        }

        /// <summary>
        /// Запустить воспроизведение звуковой дорожки для указанного текста.
        /// Клип грузится на время проигрывания и отпускается по его завершению (естественный конец,
        /// вытеснение следующей репликой, остановка канала).
        /// </summary>
        /// <param name="text"></param>
        public static void PlayForText(string text)
        {
            if (!Index.TryGetValue(text, out var data))
                return;
            var handle = data.GetLocale();
            if (handle == null)
                return;
            if (handle == _pending)
                return;
            var channel = AudioController.GetChannel(data.Channel);
            if (channel == null)
            {
                Debug.LogError($"[AudioLocalizationController] Audio channel for {data.Name} not set");
                return;
            }

            // Вытесняем и звучащую реплику (её OnFinished отпустит клип), и ещё грузящуюся.
            AudioController.StopAllSounds(channel.Name);
            _pendingCts?.Cancel();
            _pendingCts?.Dispose();
            _pendingCts = new CancellationTokenSource();
            PlayAsync(data, handle, channel.Name, _pendingCts.Token).Forget();
        }

        private static async UniTaskVoid PlayAsync(AudioLocaleData data, AssetHandle<AudioClip> handle,
            string channel, CancellationToken ct)
        {
            _pending = handle;
            AudioClip clip;
            try
            {
                clip = await handle.LoadAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // Вытеснена до старта — владение снято самим кэшем, отпускать нечего.
                return;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AudioLocalizationController] Voice load failed for {data.Name}: {e.Message}");
                return;
            }
            finally
            {
                if (_pending == handle)
                    _pending = null;
            }

            if (ct.IsCancellationRequested)
            {
                handle.Release();
                return;
            }

            if (clip == null)
            {
                Debug.LogError($"[AudioLocalizationController] Voice clip is empty for {data.Name}");
                handle.Release();
                return;
            }

            var wrapper = AudioController.PlaySoundWithControl(new SoundClipFixed(clip, false, channel));
            if (wrapper == null)
            {
                Debug.LogError($"[AudioLocalizationController] Voice playback failed to start for {data.Name}");
                handle.Release();
                return;
            }

            wrapper.OnFinished += handle.Release;
            // Звук мог завершиться ещё до подписки (OnFinished уже не повторится) — отпускаем сами.
            if (wrapper.State == PlaybackState.Finished)
                handle.Release();
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
                // Пустой хэндл на воспроизведении неотличим от «нет озвучки для языка» — сообщаем при индексации.
                foreach (var voice in localeData.Voices)
                    if (voice.Value == null)
                        Debug.LogError($"[AudioLocalizationController] Voice handle is not set for " +
                                       $"{localeData.Name}, language «{voice.Key}»");
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

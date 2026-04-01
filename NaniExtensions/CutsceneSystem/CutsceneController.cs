using System;
using System.Threading;
using Audio;
using Cysharp.Threading.Tasks;
using Naninovel;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.NaniExtensions.AudioSystem;
using Vortex.NaniExtensions.Core;
using Vortex.NaniExtensions.CutsceneSystem.Models;
using Vortex.Sdk.Core.GameCore;
using AudioController = Vortex.Core.AudioSystem.Bus.AudioController;
using Event = Spine.Event;
using UniTask = Cysharp.Threading.Tasks.UniTask;
using Random = UnityEngine.Random;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    public static class CutsceneController
    {
        private static bool _sexSceneRunning;
        private static SpineBackground _spineBackground;
        private static SkeletonAnimation _skeletonAnimation;

        private static CutsceneData _data;
        private static int _phaseIndex;

        private static CancellationTokenSource StopSexSceneTokenSource { get; set; } = new();

        private static bool _wasInit;

        /// <summary>
        /// эмбиент фазы
        /// </summary>
        private static string _ambientId;

        /// <summary>
        /// музыка момента
        /// </summary>
        private static string _musicId;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            Engine.OnInitializationFinished += Init;
        }

        private static void Init()
        {
            App.OnExit += Dispose;
            NaniWrapper.ScriptPlayer.OnStop += Close;
            _wasInit = true;
        }

        private static void Close(Script obj)
        {
            if (!_sexSceneRunning)
                return;
            NaniVortexAudioConnector.SetCutsceneMode(false);
            GameController.OnGameStateChanged -= CheckGameState;
            if (!_musicId.IsNullOrWhitespace())
                AudioController.StopCoverMusic();
            Close().Forget(Debug.LogException);
        }

        private static void Dispose()
        {
            _wasInit = false;
            App.OnExit -= Dispose;
            GameController.OnGameStateChanged -= CheckGameState;

            if (_skeletonAnimation != null)
                _skeletonAnimation.AnimationState.Event -= HandleAnimationEvents;

            //Восстанавливаем звуки нани
            AudioNaniController.PlayNaniMusic();
            AudioController.StopCoverMusic();
        }

        private static void Reset()
        {
            if (!_wasInit)
            {
                Debug.LogError("[SexSceneController] System not initialized");
                return;
            }

            //Сбрасываем индексы
            _phaseIndex = 0;

            //Паузим музыку нани
            AudioNaniController.StopNaniMusic();
            //Выключаем звуки нани
            AudioNaniController.StopNaniSfx();
            //Выключаем голос нани
            AudioNaniController.StopNaniVoice();
            //Выключаем все возможные звуки
            AudioController.StopAllSounds();
        }

        private static void CheckGameState()
        {
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Off:
                    Close().Forget();
                    break;
                case GameStates.Play:
                    _skeletonAnimation.timeScale = 1;
                    if (!_ambientId.IsNullOrWhitespace())
                        AudioController.PlaySound(_ambientId, true);
                    if (!_musicId.IsNullOrWhitespace())
                        DelayedStartMusic().Forget(Debug.LogException);
                    break;
                case GameStates.Win:
                case GameStates.Fail:
                case GameStates.Loading:
                    break;
                case GameStates.Paused:
                    _skeletonAnimation.timeScale = 0;
                    AudioController.StopAllSounds();
                    AudioController.StopCoverMusic();
                    AudioNaniController.StopNaniVoice();
                    break;
            }
        }

        private static async UniTask DelayedStartMusic()
        {
            await UniTask.DelayFrame(2);
            AudioController.PlayCoverMusic(_musicId);
        }

        public static async UniTask Open(string targetSexSceneKey, bool canBeClosedByButton = false)
        {
            NaniVortexAudioConnector.SetCutsceneMode(true);
            _sexSceneRunning = true;
            //Прячем все лишнее в няне
            NaniWrapper.ResetNani();

            //Отменяем все старое
            StopSexSceneTokenSource.Cancel();
            await UniTask.WaitForEndOfFrame();
            StopSexSceneTokenSource.Dispose();

            StopSexSceneTokenSource = new CancellationTokenSource();

            GameController.OnGameStateChanged += CheckGameState;

            //Обновляем настройки звука няни, потому что они где-то там затираются
            NaniWrapper.AudioManager.BgmVolume = NaniVortexAudioConnector.GetNaniBgmVolume();
            NaniWrapper.AudioManager.SfxVolume = NaniVortexAudioConnector.GetNaniSfxVolume();
            NaniWrapper.AudioManager.VoiceVolume = NaniVortexAudioConnector.GetNaniVoiceVolume();

            //Загружаем секс сцену
            _data = await Addressables.LoadAssetAsync<CutsceneData>(string.Format(CutsceneData.AddressablePathKey,
                targetSexSceneKey));
            if (_data == null)
            {
                Debug.LogError($"Can't open SexSceneWindow, coz can't find sex scene at path '{targetSexSceneKey}'");
                return;
            }


            //Разблокируем контент раз уж показываем его
            NaniWrapper.UnlockableManager.UnlockItem(targetSexSceneKey);
            NaniWrapper.StateManager.SaveGlobal().Forget();

            try
            {
                //Загружаем секс сцену в виде бекграунда
                _spineBackground = await NaniWrapper.BackgroundManager.AddActor(targetSexSceneKey) as SpineBackground;
                _spineBackground.ChangeVisibility(true, new Tween(duration: 1)).Forget();
                _skeletonAnimation = _spineBackground.Transform.GetComponentInChildren<SkeletonAnimation>();
                _skeletonAnimation.AnimationState.Event += HandleAnimationEvents;

                //Сброс состояний
                Reset();

                //Запускаем музыку (Только одну, если их несколько)
                var c = _data?.SexSceneAmbients.Count;
                if (c is > 0)
                {
                    _musicId = _data.SexSceneAmbients[Random.Range(0, c.Value)];
                    AudioController.PlayCoverMusic(_musicId);
                }

                //Загружаем фазу
                await LoadPhase();
            }
            catch (OperationCanceledException)
            {
                //Вовзращаем UI на место
                NaniWrapper.UIManager.SetUIVisibleWithToggle(true);
                Reset();
            }
        }

        public static async UniTask Close()
        {
            _sexSceneRunning = false;
            try
            {
                if (_skeletonAnimation != null)
                    _skeletonAnimation.AnimationState.Event -= HandleAnimationEvents;
            }
            catch (MissingReferenceException)
            {
                //Ignore
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            AudioController.StopCoverMusic();
            //Паузим музыку нани
            AudioNaniController.StopNaniMusic();
            //Выключаем звуки нани
            AudioNaniController.StopNaniSfx();
            //Выключаем голос нани
            AudioNaniController.StopNaniVoice();
            //Выключаем все возможные звуки
            AudioController.StopAllSounds();
            //Выключаем секс сцену
            StopSexSceneTokenSource.Cancel();
            StopSexSceneTokenSource.Dispose();
            StopSexSceneTokenSource = new();
            _musicId = null;
            _ambientId = null;

            //Если есть бекграунд, то плавно убираем его
            if (_spineBackground != null)
            {
                try
                {
                    await _spineBackground.ChangeVisibility(false, new Tween(1f));
                }
                catch (Exception e)
                {
                    //Ничего не делаем
                }

                //В конце удаляем бекграунд
                NaniWrapper.BackgroundManager.RemoveActor(_spineBackground.Id);
            }
        }

        public static async UniTask NextPhase()
        {
            //Переходим на следующую фазу
            _phaseIndex++;

            //Предохранитель индекса
            if (_data.Phases.Count <= _phaseIndex || _phaseIndex < 0)
                return;

            //Загружаем фазу
            await LoadPhase();
        }

        private static async UniTask LoadPhase()
        {
            //Запускаем эмбиент голоса
            _ambientId = _data.Phases[_phaseIndex].AmbientAudioPack;
            if (!_ambientId.IsNullOrWhitespace())
            {
                AudioController.StopAllSounds();
                //Запускаем новый луп голоса
                AudioController.PlaySound(_ambientId, true);
            }

            //Запускаем анимацию фазы
            await PlayAnimation();
            //Если анимация оказалась без лупа, то значит мы должны перейти к следующей сразу автоматически
            if (_data.Phases[_phaseIndex].AnimationLooped == false)
                await NextPhase();
        }


        private static UniTask PlayAnimation()
        {
            //Кеш фазы
            var phase = _data.Phases[_phaseIndex];

            //Запускаем анимацию
            var entry = _skeletonAnimation.AnimationState.SetAnimation(0, phase.AnimationKey, phase.AnimationLooped);
            //Если анимация луп - то мы ее никогда не дождемся, сразу возвращаем
            if (phase.AnimationLooped)
                return UniTask.CompletedTask;

            //Иначе ждем пока анимация не закончится
            bool isAnimationCompleted = false;
            entry.Complete += (_) => { isAnimationCompleted = true; };
            return UniTask.WaitUntil(() => isAnimationCompleted, cancellationToken: StopSexSceneTokenSource.Token);
        }

        private static void HandleAnimationEvents(TrackEntry trackEntry, Event e)
        {
            var eventSoundId = _data.EventToAudioDatas.Find(audioData => audioData.EventName == e.Data.Name)?.AudioPack;
            if (!eventSoundId.IsNullOrWhitespace())
                AudioController.PlaySound(eventSoundId);
        }
    }
}
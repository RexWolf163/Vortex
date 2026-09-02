using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Naninovel;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.AppSystem.System.TimeSystem;
using UniTask = Cysharp.Threading.Tasks.UniTask;
#if UNITY_EDITOR
using UnityEditor;
using Vortex.Unity.Extensions;
#endif

namespace Vortex.NaniExtensions.Core
{
    public static class NaniWrapper
    {
        private static IAudioManager _audioManager;
        private static IStateManager _stateManager;
        private static ILocalizationManager _l10N;
        private static ICommunityLocalization _communityL10N;
        private static IScriptPlayer _scriptPlayer;
        private static IBackgroundManager _backgroundManager;
        private static ICharacterManager _characterManager;
        private static ITextPrinterManager _textPrinterManager;
        private static IChoiceHandlerManager _choiceHandlerManager;
        private static IUnlockableManager _unlockableManager;
        private static IUIManager _uiManager;
        private static ICustomVariableManager _variablesManager;

        public static IAudioManager AudioManager => _audioManager ??= Engine.GetService<IAudioManager>();
        public static IStateManager StateManager => _stateManager ??= Engine.GetService<IStateManager>();

        public static ILocalizationManager L10N => _l10N ??= Engine.GetService<ILocalizationManager>();

        public static ICommunityLocalization CommunityL10N =>
            _communityL10N ??= Engine.GetService<ICommunityLocalization>();

        public static IScriptPlayer ScriptPlayer => _scriptPlayer ??= Engine.GetService<IScriptPlayer>();

        public static IBackgroundManager BackgroundManager =>
            _backgroundManager ??= Engine.GetService<IBackgroundManager>();

        public static ICharacterManager CharacterManager =>
            _characterManager ??= Engine.GetService<ICharacterManager>();

        public static ITextPrinterManager TextPrinterManager =>
            _textPrinterManager ??= Engine.GetService<ITextPrinterManager>();

        public static IChoiceHandlerManager ChoiceHandlerManager =>
            _choiceHandlerManager ??= Engine.GetService<IChoiceHandlerManager>();

        public static IUnlockableManager UnlockableManager =>
            _unlockableManager ??= Engine.GetService<IUnlockableManager>();

        public static IUIManager UIManager => _uiManager ??= Engine.GetService<IUIManager>();

        public static ICustomVariableManager VariablesManager =>
            _variablesManager ??= Engine.GetService<ICustomVariableManager>();

        /// <summary>
        /// Nani script player Запущен
        /// </summary>
        public static event Action OnNaniStart;

        /// <summary>
        /// Nani script player Остановился и не ожидается Choice или иных действий 
        /// </summary>
        public static event Action OnNaniStop;

        private static bool _isPlaying;

        /// <summary>
        /// Назначена отложенная проверка останова. Гасит ложный OnNaniStop на @if/goto/смене скрипта:
        /// ScriptPlayer.Resume() синхронно делает Stop()→OnPlay в одном кадре, поэтому решение об останове
        /// откладываем на кадр и перепроверяем <see cref="NaniIsPlaying"/> (см. ConfirmStopDeferred).
        /// </summary>
        private static bool _stopPending;

        /// <summary>Owner-ключ для дебаунса SaveGlobal по родному останову скрипта (см. <see cref="OnNativeStopSave"/>).</summary>
        private static readonly object SaveGlobalOwner = new();

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            GameController.OnNewGame -= OnNewGame;
            GameController.OnLoadGame -= OnLoadGame;
            GameController.OnGameStateChanged -= OnStateChanged;
            GameController.OnNewGame += OnNewGame;
            GameController.OnLoadGame += OnLoadGame;
            GameController.OnGameStateChanged += OnStateChanged;
        }

        private static void OnStateChanged()
        {
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Off:
                case GameStates.Win:
                case GameStates.Fail:
                    ScriptPlayer.Stop();
                    ResetNani();
                    break;
            }
        }

        private static void OnNewGame()
        {
            ScriptPlayer.Stop();
            ResetNani();
            VariablesManager.ResetAllVariables();
        }

        private static void OnLoadGame()
        {
            ScriptPlayer.Stop();
            // Чистый baseline плеера ТОЛЬКО на загрузке (не в общем ResetNani — там валилось на teardown-путях).
            // Движок здесь гарантированно готов (OnLoadGame после WaitForSessionServices/Engine.Initialized),
            // поэтому Dynamic-выгруженный скрипт корректно перезагрузится следующим LoadAndPlay — фикс «script not loaded»
            // на сценарии «проиграл → Нани выгрузила → загрузка сейва без рестарта движка». Паттерн Stop()+ResetService()
            // — как в GalleryController.StopCutscene.
            ScriptPlayer.ResetService();
            ResetNani();
        }

        public static void ResetNani()
        {
            ScriptPlayer.OnPlay -= OnScriptEvent;
            ScriptPlayer.OnStop -= OnScriptEvent;
            ScriptPlayer.OnStop -= OnNativeStopSave;

            AudioManager.StopAllBgm();
            AudioManager.StopAllSfx();
            AudioManager.StopVoice();

            var bgs = BackgroundManager.Actors;
            var tween = new Tween(0);
            foreach (var actor in bgs)
                actor.ChangeVisibility(false, tween);
            var actors = CharacterManager.Actors;
            foreach (var actor in actors)
                actor.ChangeVisibility(false, tween);
            var textPrinterActors = TextPrinterManager.Actors;
            foreach (var actor in textPrinterActors)
                actor.ChangeVisibility(false, tween);
            var choiceHandlerActors = ChoiceHandlerManager.Actors;
            foreach (var actor in choiceHandlerActors)
            {
                var choices = actor.Choices.ToArray();
                foreach (var choice in choices)
                    actor.RemoveChoice(choice.Id);

                actor.ChangeVisibility(false, tween);
            }

            ChoiceHandlerManager.ResetService();

            /*
            try
            {
                ScriptPlayer.Stop();
                ScriptPlayer.ResetService();
            }
            catch (Exception ex)
            {
                if (Settings.Data().DebugMode)
                    Debug.LogException(ex);
            }
            */

            ScriptPlayer.OnPlay += OnScriptEvent;
            ScriptPlayer.OnStop += OnScriptEvent;
            ScriptPlayer.OnStop += OnNativeStopSave;
        }

        /// <summary>
        /// Родной останов скрипта → сохранить глобал (в т.ч. реестр прочитанных строк для промотки).
        /// Через <see cref="TimeController.Accumulate{T}"/> — коалесит частые/транзиентные стопы (@if/goto,
        /// смена скрипта дают Stop→Resume в кадре) в одну запись на конец кадра. Не идеал, но с дебаунсом.
        /// </summary>
        private static void OnNativeStopSave(Script _) =>
            TimeController.Accumulate(SaveGlobalDeferred, SaveGlobalOwner);

        private static void SaveGlobalDeferred()
        {
            // Флаш аккумулятора мог прийти после уничтожения движка (teardown/выход из плей-мода) — не трогаем.
            if (Engine.Initialized)
                StateManager.SaveGlobal().Forget();
        }

        private static void OnScriptEvent(Script obj)
        {
            var playing = NaniIsPlaying();
            if (playing)
            {
                // Старт/возобновление — мгновенно. Заодно снимаем отложенный останов: если это был
                // транзиент (Resume: Stop→OnPlay в одном кадре), OnPlay гасит назначенную проверку.
                _stopPending = false;
                if (_isPlaying)
                    return;
                _isPlaying = true;
                if (Settings.Data().DebugMode)
                    Debug.Log("Nani Is Run");
                OnNaniStart?.Invoke();
                return;
            }

            // Останов может быть ложным (@if/goto/смена скрипта уводят в Resume, а тот синхронно зовёт
            // Stop() перед пересозданием playRoutine). Не стреляем сразу — подтверждаем через кадр.
            if (!_isPlaying || _stopPending)
                return;
            _stopPending = true;
            ConfirmStopDeferred().Forget();
        }

        /// <summary>
        /// Подтверждение останова на следующем кадре. К этому моменту Resume уже пересоздал playRoutine
        /// (Playing снова true) — значит останов был транзиентным и глушится. Реальный останов
        /// (Stop() без последующего Resume) остаётся false и порождает OnNaniStop.
        /// </summary>
        private static async UniTask ConfirmStopDeferred()
        {
            await UniTask.NextFrame();

            // Движок мог быть уничтожен (выход из плей-мода) — кешированные сервисы протухли. Считаем
            // останов состоявшимся: сбрасываем оба флага, чтобы следующий реальный старт поднял OnNaniStart.
            if (!Engine.Initialized)
            {
                _stopPending = false;
                _isPlaying = false;
                return;
            }

            if (!_stopPending) // сняли встречным OnPlay в том же кадре — это был не останов
                return;
            _stopPending = false;
            if (NaniIsPlaying()) // playRoutine пересоздан — возобновились, останова нет
                return;

            _isPlaying = false;
            if (Settings.Data().DebugMode)
                Debug.Log("Nani Is Stopped");
            OnNaniStop?.Invoke();
        }

        public static bool NaniIsPlaying()
        {
            var id = ChoiceHandlerManager?.Actors?.FirstOrDefault()?.Id ?? "";
            if (!id.IsNullOrWhitespace()
                && ChoiceHandlerManager != null
                && ChoiceHandlerManager.GetActorState(id).Visible)
                return true;

            return ScriptPlayer is { Playing: true };
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: собирает список всех id персонажей из всех <see cref="CharactersConfiguration"/>-ассетов проекта.
        /// Используется как источник для <c>[ValueDropdown]</c> в хэндлерах, привязанных к ключу персонажа Naninovel.
        /// </summary>
        public static List<string> GetNaniCharacters()
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets("t:CharactersConfiguration");

            if (guids.Length == 0)
                return result;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<CharactersConfiguration>(path);
                if (config != null && config.Metadata != null)
                    result.AddRange(config.Metadata.GetAllIds());
            }

            return result;
        }

        /// <summary>
        /// Editor-only: возвращает список имён предопределённых переменных Naninovel
        /// из <see cref="CustomVariablesConfiguration"/>. Источник для <c>[ValueDropdown]</c> в хэндлерах,
        /// которым нужен выбор Nani-переменной.
        /// </summary>
        public static List<string> GetNaniVariables()
        {
            var result = new List<string>();
            var config = AssetDatabaseExt.GetSingletonAsset<CustomVariablesConfiguration>();
            if (config != null && config.PredefinedVariables != null)
                result.AddRange(config.PredefinedVariables.Select(customVar => customVar.Name));

            return result;
        }
#endif
    }
}
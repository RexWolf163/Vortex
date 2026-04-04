using System;
using System.Linq;
using Naninovel;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.Core.GameCore;

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
            ResetNani();
        }

        public static void ResetNani()
        {
            ScriptPlayer.OnPlay -= OnScriptEvent;
            ScriptPlayer.OnStop -= OnScriptEvent;

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

            ScriptPlayer.OnPlay += OnScriptEvent;
            ScriptPlayer.OnStop += OnScriptEvent;
        }

        private static void OnScriptEvent(Script obj)
        {
            if (_isPlaying == NaniIsPlaying())
                return;
            _isPlaying = NaniIsPlaying();
            if (_isPlaying)
                OnNaniStart?.Invoke();
            else
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
    }
}
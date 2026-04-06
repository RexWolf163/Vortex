using System;
using Naninovel;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.NaniExtensions.Core;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.NaniExtensions.SaveSystem
{
    public static class NaniDataSaveController
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            GameController.OnGameStateChanged += OnGameStateChanged;
            GameController.OnLoadGame += OnLoadGame;
        }

        private static bool _isPlaying;

        private static NaniStateData _data;

        private static void OnGameStateChanged()
        {
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Off:
                    Unsubscribe();
                    break;
                case GameStates.Play:
                    Subscribe();
                    break;
                case GameStates.Win:
                    break;
                case GameStates.Fail:
                    break;
                case GameStates.Paused:
                    break;
                case GameStates.Loading:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void Subscribe()
        {
            if (_isPlaying) return;
            _isPlaying = true;
            _data = GameController.Get<NaniStateData>();
            NaniWrapper.OnNaniStart += SaveState;
            NaniWrapper.OnNaniStop += SaveState;
        }

        private static void Unsubscribe()
        {
            if (!_isPlaying) return;
            _isPlaying = false;
            NaniWrapper.OnNaniStart -= SaveState;
            NaniWrapper.OnNaniStop -= SaveState;
        }

        private static void SaveState()
        {
            var list = NaniWrapper.VariablesManager.Variables;
            foreach (var customVariable in list)
            {
                var value = customVariable.Value;
                switch (value.Type)
                {
                    case CustomVariableValueType.String:
                        _data.Variables[customVariable.Name] = new StringData(value.String);
                        break;
                    case CustomVariableValueType.Numeric:
                        _data.Variables[customVariable.Name] = new FloatData(value.Number);
                        break;
                    case CustomVariableValueType.Boolean:
                        _data.Variables[customVariable.Name] = new BoolData(value.Boolean);
                        break;
                }
            }
        }

        private static void OnLoadGame()
        {
            _data = GameController.Get<NaniStateData>();
            NaniWrapper.VariablesManager.ResetAllVariables();
            foreach (var dataVariable in _data.Variables)
            {
                switch (dataVariable.Value)
                {
                    case StringData stringValue:
                        NaniWrapper.VariablesManager.SetVariableValue(dataVariable.Key,
                            new CustomVariableValue(stringValue));
                        break;
                    case FloatData floatValue:
                        NaniWrapper.VariablesManager.SetVariableValue(dataVariable.Key,
                            new CustomVariableValue(floatValue));
                        break;
                    case BoolData boolValue:
                        NaniWrapper.VariablesManager.SetVariableValue(dataVariable.Key,
                            new CustomVariableValue(boolValue));
                        break;
                }
            }
        }
    }
}
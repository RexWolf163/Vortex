using System;
using System.Collections.Generic;
using Vortex.Sdk.CharacterViewSystem.Models;
using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.MapLevels.Bus;

namespace Vortex.Sdk.CharacterViewSystem.View
{
    public class CharactersView : MonoBehaviour
    {
        private IReadOnlyDictionary<string, PlayableCharacter> _index;

        private void OnEnable()
        {
            GameController.OnGameStateChanged += OnGameStateChanged;
            OnGameStateChanged();
        }

        private void OnDisable()
        {
            GameController.OnGameStateChanged -= OnGameStateChanged;
            Dispose();
        }

        private void OnGameStateChanged()
        {
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Loading:
                case GameStates.Off:
                    Dispose();
                    break;
                case GameStates.Play:
                    Init();
                    break;
                case GameStates.Win:
                case GameStates.Fail:
                    break;
                case GameStates.Paused:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Dispose()
        {
            if (MapLevelsBus.Controller != null)
                MapLevelsBus.Controller.OnActiveLevelChanged -= OnMapChanged;
        }

        private void Init()
        {
            MapLevelsBus.Controller.OnActiveLevelChanged += OnMapChanged;
            _index = Vortex.Sdk.CharacterViewSystem.CharactersBus.GetPlayableCharacters();
        }

        private void OnMapChanged(string mapId, string gateId)
        {
            foreach (var pc in _index.Values)
            {
                //TODO заспаунить персонажа
            }
        }
    }
}
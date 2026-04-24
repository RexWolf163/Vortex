using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Sdk.Core.UIHandlers
{
    [RequireComponent(typeof(UIStateSwitcher))]
    public class GameStateHandler : MonoBehaviour
    {
        [SerializeField, StateSwitcher(typeof(GameStates)), AutoLink]
        private UIStateSwitcher switcher;

        private void Awake()
        {
            switcher.ResetStates();
        }

        private void OnEnable()
        {
            if (App.GetState() < AppStates.Running && App.GetState() != AppStates.Unfocused)
            {
                gameObject.SetActive(false);
                return;
            }

            GameController.OnGameStateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameController.OnGameStateChanged -= Refresh;
        }

        private void Refresh()
        {
            switcher.Set(GameController.GetState());
        }
    }
}
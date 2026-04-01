using UnityEngine;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.Sdk.Core.UIHandlers
{
    public class GameMenuHandler : MonoBehaviour
    {
        public void NewGame()
        {
            GameController.NewGame();
        }

        public void SetPause()
        {
            GameController.SetPause(true);
        }

        public void SetUnPause()
        {
            GameController.SetPause(false);
        }

        public void ExitGame()
        {
            GameController.ExitGame();
        }

        public void ExitApplication()
        {
            GameController.Exit();
        }
    }
}
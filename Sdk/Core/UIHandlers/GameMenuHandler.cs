using System.Linq;
using UnityEngine;
using Vortex.Core.SaveSystem.Bus;
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

        public void LoadLastGame()
        {
            var list = SaveController.GetIndex();
            if (list.Count == 0)
                return;
            var lastGuid = list.Keys.First();
            var last = list[lastGuid];

            foreach (var (guid, summary) in list)
            {
                if (summary.UnixTimestamp <= last.UnixTimestamp) continue;
                last = summary;
                lastGuid = guid;
            }

            SaveController.Load(lastGuid);
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
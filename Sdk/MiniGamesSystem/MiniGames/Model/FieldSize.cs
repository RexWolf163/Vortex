using System;
using Sirenix.OdinInspector;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Model
{
    [Serializable]
    public struct FieldSize
    {
        [HorizontalGroup] public int columns;
        [HorizontalGroup] public int rows;

        public FieldSize(int c, int r)
        {
            columns = c;
            rows = r;
        }
    }
}
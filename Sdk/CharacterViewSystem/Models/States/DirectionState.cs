using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Sdk.CharacterViewSystem.Models.States
{
    public partial class DirectionState : ExtensibleEnum
    {
        public static readonly DirectionState North = new(nameof(North), 0);
        public static readonly DirectionState East = new(nameof(East), 1);
        public static readonly DirectionState South = new(nameof(South), 2);
        public static readonly DirectionState West = new(nameof(West), 3);
        public static readonly DirectionState Up = new(nameof(Up), 4);
        public static readonly DirectionState Down = new(nameof(Down), 5);

        public DirectionState(string key, int order) : base(key, order)
        {
        }
    }
}
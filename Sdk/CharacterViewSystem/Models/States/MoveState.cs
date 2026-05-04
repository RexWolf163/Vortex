using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Sdk.CharacterViewSystem.Models.States
{
    public partial class MoveState : ExtensibleEnum
    {
        public static readonly MoveState Stay = new(nameof(Stay), 0);
        public static readonly MoveState Move = new(nameof(Move), 1);

        public MoveState(string key, int order) : base(key, order)
        {
        }
    }
}
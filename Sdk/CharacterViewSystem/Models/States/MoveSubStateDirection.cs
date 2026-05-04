using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Sdk.CharacterViewSystem.Models.States
{
    public partial class MoveSubStateDirection : ExtensibleEnum
    {
        public static readonly MoveSubStateDirection Forward = new(nameof(Forward), 0);
        public static readonly MoveSubStateDirection Back = new(nameof(Back), 1);
        public static readonly MoveSubStateDirection Side = new(nameof(Side), 2);

        public MoveSubStateDirection(string key, int order) : base(key, order)
        {
        }
    }
}
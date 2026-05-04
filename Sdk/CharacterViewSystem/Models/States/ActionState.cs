using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Sdk.CharacterViewSystem.Models.States
{
    public partial class ActionState : ExtensibleEnum
    {
        public static readonly ActionState Idle = new(nameof(Idle), 0);
        public static readonly ActionState Speak = new(nameof(Speak), 1);
        public static readonly ActionState Use = new(nameof(Use), 2);

        public ActionState(string key, int order) : base(key, order)
        {
        }
    }
}
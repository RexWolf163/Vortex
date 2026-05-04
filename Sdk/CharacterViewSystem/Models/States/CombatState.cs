using System.Collections.Generic;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;

namespace Vortex.Sdk.CharacterViewSystem.Models.States
{
    public partial class CombatState : ExtensibleEnum
    {
        public static readonly CombatState Idle = new(nameof(Idle), 0);
        public static readonly CombatState Attack = new(nameof(Attack), 1);
        public static readonly CombatState Defence = new(nameof(Defence), 2);

        public CombatState(string key, int order) : base(key, order)
        {
        }

        public static IReadOnlyList<CombatState> All => GetAll<CombatState>();
    }
}
using System;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Битовая маска одновременно активных <see cref="PointerAction"/> (бит N = (int)action).
    /// Value-тип с value-equality → пригоден для реактивного дедупа. Даёт одновременность
    /// (несколько кнопок сразу) для девайса и доминанту (по приоритету порядка enum) для спрайта.
    /// </summary>
    public readonly struct PointerActionMask : IEquatable<PointerActionMask>
    {
        public static readonly PointerActionMask Empty = new(0);

        private readonly int _bits;

        public PointerActionMask(int bits) => _bits = bits;

        public bool IsEmpty => _bits == 0;

        public bool IsActive(PointerAction action) =>
            action != PointerAction.None && (_bits & (1 << (int)action)) != 0;

        public PointerActionMask With(PointerAction action) =>
            action == PointerAction.None ? this : new PointerActionMask(_bits | (1 << (int)action));

        public PointerActionMask Without(PointerAction action) =>
            action == PointerAction.None ? this : new PointerActionMask(_bits & ~(1 << (int)action));

        public PointerActionMask Set(PointerAction action, bool active) => active ? With(action) : Without(action);

        /// <summary>Доминанта для выбора спрайта: младший активный бит = высший приоритет (порядок enum).</summary>
        public PointerAction Dominant()
        {
            for (var i = (int)PointerAction.Action1; i <= (int)PointerAction.Action10; i++)
                if ((_bits & (1 << i)) != 0)
                    return (PointerAction)i;
            return PointerAction.None;
        }

        public bool Equals(PointerActionMask other) => _bits == other._bits;
        public override bool Equals(object obj) => obj is PointerActionMask m && Equals(m);
        public override int GetHashCode() => _bits;
    }
}

using System;

namespace Vortex.Unity.UI.VirtualCursorSystem.Model
{
    /// <summary>
    /// Битовая маска одновременно активных <see cref="PointerAction"/> (бит N = (int)action).
    /// Value-тип с value-equality → пригоден для реактивного дедупа. Даёт одновременность
    /// (несколько кнопок сразу) для девайса и доминанту (по приоритету порядка enum) для спрайта.
    /// <see cref="PointerAction.None"/> битом не представлен: он никогда не активен и не меняет маску.
    /// </summary>
    public readonly struct PointerActionMask : IEquatable<PointerActionMask>
    {
        /// <summary>Маска без активных действий.</summary>
        public static readonly PointerActionMask Empty = new(0);

        /// <summary>Биты активных действий: бит N = (int)action.</summary>
        private readonly int _bits;

        /// <summary>Маска из готовых битов.</summary>
        public PointerActionMask(int bits) => _bits = bits;

        /// <summary>Ни одно действие не активно.</summary>
        public bool IsEmpty => _bits == 0;

        /// <summary>Активно ли действие. Для <see cref="PointerAction.None"/> — всегда false.</summary>
        public bool IsActive(PointerAction action) =>
            action != PointerAction.None && (_bits & (1 << (int)action)) != 0;

        /// <summary>Копия маски с включённым действием (<see cref="PointerAction.None"/> — без изменений).</summary>
        public PointerActionMask On(PointerAction action) =>
            action == PointerAction.None ? this : new PointerActionMask(_bits | (1 << (int)action));

        /// <summary>Копия маски с выключенным действием (<see cref="PointerAction.None"/> — без изменений).</summary>
        public PointerActionMask Off(PointerAction action) =>
            action == PointerAction.None ? this : new PointerActionMask(_bits & ~(1 << (int)action));

        /// <summary>Копия маски с действием, выставленным в <paramref name="active"/>.</summary>
        public PointerActionMask Set(PointerAction action, bool active) => active ? On(action) : Off(action);

        /// <summary>
        /// Доминирующее действие — активное с наименьшим индексом enum (младший активный бит
        /// от <see cref="PointerAction.BaseClick"/> до <see cref="PointerAction.AltClick"/>).
        /// </summary>
        /// <returns>Доминирующее действие или <see cref="PointerAction.None"/>, если активных нет.</returns>
        public PointerAction Dominant()
        {
            for (var i = (int)PointerAction.BaseClick; i <= (int)PointerAction.AltClick; i++)
                if ((_bits & (1 << i)) != 0)
                    return (PointerAction)i;
            return PointerAction.None;
        }

        /// <summary>Равенство по битам.</summary>
        public bool Equals(PointerActionMask other) => _bits == other._bits;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is PointerActionMask m && Equals(m);

        /// <inheritdoc/>
        public override int GetHashCode() => _bits;
    }
}
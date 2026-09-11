using System;
using System.Collections.Generic;
using System.Linq;

namespace Vortex.Sdk.RebindSystem.Model
{
    /// <summary>
    /// Значение слота: клавиша-триггер и 0–2 модификатора (пути контролов Input System). Неизменяемо.
    /// Модификаторы хранятся как записаны — общий <c>&lt;Keyboard&gt;/ctrl</c> или со стороной
    /// <c>&lt;Keyboard&gt;/leftCtrl</c>; нормализация для сравнения — в сигнатуре.
    /// </summary>
    public sealed class BindingValue : IEquatable<BindingValue>
    {
        /// <summary>Пустое значение: слот без клавиши.</summary>
        public static readonly BindingValue Empty = new(null);

        public BindingValue(string trigger, params string[] modifiers)
        {
            Trigger = string.IsNullOrWhiteSpace(trigger) ? null : trigger.Trim();
            Modifiers = modifiers == null
                ? Array.Empty<string>()
                : modifiers.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()).ToArray();
        }

        /// <summary>Путь клавиши-триггера. <c>null</c> — значение пустое.</summary>
        public string Trigger { get; }

        /// <summary>Пути модификаторов, 0–2.</summary>
        public IReadOnlyList<string> Modifiers { get; }

        public bool IsEmpty => string.IsNullOrEmpty(Trigger);

        /// <summary>Равенство без учёта регистра и порядка модификаторов.</summary>
        public bool Equals(BindingValue other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (!string.Equals(Trigger, other.Trigger, StringComparison.OrdinalIgnoreCase))
                return false;
            if (Modifiers.Count != other.Modifiers.Count)
                return false;
            return Modifiers.All(m => other.Modifiers.Any(o => string.Equals(m, o, StringComparison.OrdinalIgnoreCase)));
        }

        public override bool Equals(object obj) => obj is BindingValue value && Equals(value);

        public override int GetHashCode()
        {
            var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Trigger ?? string.Empty);
            foreach (var modifier in Modifiers)
                hash ^= StringComparer.OrdinalIgnoreCase.GetHashCode(modifier);
            return hash;
        }

        public override string ToString() => IsEmpty ? "<пусто>" : string.Join("+", Modifiers.Append(Trigger));
    }
}

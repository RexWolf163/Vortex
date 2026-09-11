using System;
using System.Linq;
using Vortex.Sdk.RebindSystem.Model;
using Vortex.Sdk.RebindSystem.Presets;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Сигнатура — нормализованный ключ сравнения значения: модификаторы по алфавиту + триггер, без учёта
    /// регистра. При выключенном различении сторон левый/правый модификатор приводятся к общему.
    /// Ctrl и Ctrl+A — разные сигнатуры.
    /// </summary>
    internal static class Signature
    {
        /// <summary>Сигнатура значения. <c>null</c> — значение пустое.</summary>
        internal static string Of(BindingValue value, bool distinguishSides)
        {
            if (value == null || value.IsEmpty)
                return null;

            var trigger = Normalize(value.Trigger);
            if (value.Modifiers.Count == 0)
                return trigger;

            var modifiers = value.Modifiers
                .Select(m => NormalizeModifier(m, distinguishSides))
                .Distinct()
                .OrderBy(m => m, StringComparer.Ordinal);
            return string.Join("+", modifiers) + "+" + trigger;
        }

        internal static string Normalize(string path) => path.Trim().ToLowerInvariant();

        /// <summary>Нормализация модификатора: без различения сторон leftCtrl/rightCtrl → ctrl и т.п.</summary>
        internal static string NormalizeModifier(string path, bool distinguishSides)
        {
            var normalized = Normalize(path);
            if (distinguishSides)
                return normalized;

            var slash = normalized.LastIndexOf('/');
            var head = slash >= 0 ? normalized.Substring(0, slash + 1) : string.Empty;
            var key = slash >= 0 ? normalized.Substring(slash + 1) : normalized;
            key = key switch
            {
                "leftshift" or "rightshift" => "shift",
                "leftctrl" or "rightctrl" => "ctrl",
                "leftalt" or "rightalt" => "alt",
                _ => key
            };
            return head + key;
        }

        /// <summary>Набор модификаторов значения без различения сторон — для сверки с запрещёнными.</summary>
        internal static ModifierSet SetOf(BindingValue value)
        {
            var set = ModifierSet.None;
            foreach (var modifier in value.Modifiers)
            {
                var normalized = NormalizeModifier(modifier, false);
                var key = normalized.Substring(normalized.LastIndexOf('/') + 1);
                set |= key switch
                {
                    "shift" => ModifierSet.Shift,
                    "ctrl" => ModifierSet.Ctrl,
                    "alt" => ModifierSet.Alt,
                    _ => ModifierSet.None
                };
            }

            return set;
        }
    }
}

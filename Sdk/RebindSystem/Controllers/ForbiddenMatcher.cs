using System.Collections.Generic;
using Vortex.Sdk.RebindSystem.Model;
using Vortex.Sdk.RebindSystem.Presets;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Сверка значения со списком запрещённых. Модификаторы сравниваются без различения сторон: запись
    /// «Alt+Enter» запрещает и левый, и правый Alt при любом режиме различения.
    /// </summary>
    internal static class ForbiddenMatcher
    {
        internal static bool IsForbidden(BindingValue value, IReadOnlyList<ForbiddenEntry> entries)
        {
            if (value == null || value.IsEmpty || entries == null)
                return false;

            var trigger = Signature.Normalize(value.Trigger);
            var set = Signature.SetOf(value);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Control)
                                  || Signature.Normalize(entry.Control) != trigger)
                    continue;

                switch (entry.Mask)
                {
                    case ModifierMask.None when set == ModifierSet.None:
                    case ModifierMask.Specific when set == entry.Modifiers:
                    case ModifierMask.AnyNonEmpty when set != ModifierSet.None:
                        return true;
                }
            }

            return false;
        }
    }
}

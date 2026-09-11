using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Vortex.Sdk.RebindSystem.Model;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Правило обслуживания: что система считает обслуживаемым биндингом — то, что назначается через
    /// «нажмите клавишу» без существенных усложнений.
    ///
    /// Контрол обслуживаемый, если его layout — семейство Button (по иерархии layout'ов Input System) или
    /// синтетическое направление оси (контрол семейства Axis с именем up/down/left/right: колесо мыши и
    /// подобные). Составной обслуживается только «1–2 модификатора Shift/Ctrl/Alt + триггер» с триггером
    /// клавиатуры или мыши. Любые interactions/processors, прочие составные, оси, значения, usage- и
    /// wildcard-пути — надсистемные.
    ///
    /// Разбор идёт по layout'ам, без подключённых устройств: классификация не зависит от того, воткнут ли
    /// геймпад.
    /// </summary>
    internal static class ServiceRule
    {
        private static readonly HashSet<string> OneModifierNames =
            new(StringComparer.OrdinalIgnoreCase) { "OneModifier", "ButtonWithOneModifier" };

        private static readonly HashSet<string> TwoModifiersNames =
            new(StringComparer.OrdinalIgnoreCase) { "TwoModifiers", "ButtonWithTwoModifiers" };

        private static readonly HashSet<string> ModifierParts =
            new(StringComparer.OrdinalIgnoreCase) { "modifier", "modifier1", "modifier2" };

        // У современных составных триггер — «binding», у устаревших ButtonWith* — «button».
        private static readonly HashSet<string> TriggerParts =
            new(StringComparer.OrdinalIgnoreCase) { "binding", "button" };

        private static readonly HashSet<string> HalfAxes =
            new(StringComparer.OrdinalIgnoreCase) { "up", "down", "left", "right" };

        private static readonly HashSet<string> ModifierKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "shift", "leftShift", "rightShift", "ctrl", "leftCtrl", "rightCtrl", "alt", "leftAlt", "rightAlt"
        };

        /// <summary>Layout устройства из пути. <c>null</c> — нет layout'а или wildcard.</summary>
        internal static string DeviceLayout(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                var device = InputControlPath.TryGetDeviceLayout(path);
                return device == null || device == "*" ? null : device;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Layout основан на базовом (или совпадает с ним).</summary>
        internal static bool IsBasedOn(string layout, string baseLayout)
        {
            if (string.IsNullOrEmpty(layout) || string.IsNullOrEmpty(baseLayout))
                return false;
            try
            {
                return InputSystem.IsFirstLayoutBasedOnSecond(layout, baseLayout);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Контрол дискретный: семейство Button или синтетическое направление оси.</summary>
        internal static bool IsDiscreteControl(string path)
        {
            // usage/wildcard отсекаем до разбора: TryGetControlLayout на wildcard бросает NotImplementedException.
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf('*') >= 0 || path.IndexOf('{') >= 0)
                return false;
            if (DeviceLayout(path) == null)
                return false;

            string layout;
            try
            {
                layout = InputControlPath.TryGetControlLayout(path);
            }
            catch (Exception)
            {
                return false;
            }

            if (string.IsNullOrEmpty(layout))
                return false;
            if (IsBasedOn(layout, "Button"))
                return true;
            return IsBasedOn(layout, "Axis") && HalfAxes.Contains(LastSegment(path));
        }

        /// <summary>Имя синтетического направления оси: up/down/left/right.</summary>
        internal static bool IsHalfAxis(string name) => name != null && HalfAxes.Contains(name);

        /// <summary>Модификатор Shift/Ctrl/Alt клавиатуры (любой стороны).</summary>
        internal static bool IsKeyboardModifier(string path) =>
            IsBasedOn(DeviceLayout(path), "Keyboard") && ModifierKeys.Contains(LastSegment(path));

        internal static bool IsKeyboardOrMouse(string path)
        {
            var device = DeviceLayout(path);
            return IsBasedOn(device, "Keyboard") || IsBasedOn(device, "Mouse");
        }

        internal static string LastSegment(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }

        /// <summary>Проверка кандидата на обслуживаемость (п. 4 и 7 порядка проверки).</summary>
        internal static RejectReason ValidateCandidate(BindingValue value)
        {
            if (value == null || value.IsEmpty)
                return RejectReason.InvalidTrigger;
            if (value.Modifiers.Count > 2)
                return RejectReason.TooManyModifiers;
            if (!IsDiscreteControl(value.Trigger))
                return RejectReason.InvalidTrigger;
            foreach (var modifier in value.Modifiers)
                if (!IsKeyboardModifier(modifier))
                    return RejectReason.InvalidTrigger;
            // Два модификатора одного семейства (LeftCtrl+RightCtrl): в ассете нужно держать оба, а сигнатура без
            // различения сторон склеит их в один Ctrl.
            if (value.Modifiers.Count == 2
                && Signature.NormalizeModifier(value.Modifiers[0], false) == Signature.NormalizeModifier(value.Modifiers[1], false))
                return RejectReason.AmbiguousModifiers;
            if (value.Modifiers.Count > 0 && !IsKeyboardOrMouse(value.Trigger))
                return RejectReason.InvalidTrigger;
            return RejectReason.None;
        }

        /// <summary>
        /// Прочитать обслуживаемое значение заводского биндинга по индексу. <paramref name="partCount"/> —
        /// сколько частей составного пропустить вызывающему (заполняется и при отказе).
        /// </summary>
        internal static bool TryReadServiced(InputAction action, int index, out BindingValue value, out int partCount)
        {
            value = null;
            partCount = 0;
            var bindings = action.bindings;
            var binding = bindings[index];
            if (binding.isPartOfComposite)
                return false;

            if (binding.isComposite)
            {
                var next = index + 1;
                while (next < bindings.Count && bindings[next].isPartOfComposite)
                    next++;
                partCount = next - index - 1;
            }

            if (!string.IsNullOrEmpty(binding.interactions) || !string.IsNullOrEmpty(binding.processors))
                return false;

            if (!binding.isComposite)
            {
                if (!IsDiscreteControl(binding.path))
                    return false;
                value = new BindingValue(binding.path);
                return true;
            }

            var name = CompositeName(binding.path);
            var expected = OneModifierNames.Contains(name) ? 1 : TwoModifiersNames.Contains(name) ? 2 : 0;
            if (expected == 0)
                return false;

            string trigger = null;
            var modifiers = new List<string>();
            for (var i = index + 1; i <= index + partCount; i++)
            {
                var part = bindings[i];
                if (!string.IsNullOrEmpty(part.interactions) || !string.IsNullOrEmpty(part.processors))
                    return false;

                var partName = part.name ?? string.Empty;
                if (ModifierParts.Contains(partName))
                    modifiers.Add(part.path);
                else if (TriggerParts.Contains(partName) && trigger == null)
                    trigger = part.path;
                else
                    return false;
            }

            if (trigger == null || modifiers.Count != expected)
                return false;

            var candidate = new BindingValue(trigger, modifiers.ToArray());
            if (ValidateCandidate(candidate) != RejectReason.None)
                return false;

            value = candidate;
            return true;
        }

        /// <summary>Имя составного без параметров: «OneModifier(overrideModifiersNeedToBePressedFirst)» → «OneModifier».</summary>
        internal static string CompositeName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            var bracket = path.IndexOf('(');
            return (bracket >= 0 ? path.Substring(0, bracket) : path).Trim();
        }
    }
}

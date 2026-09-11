using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.RebindSystem.Presets
{
    /// <summary>Модификаторы комбинации (без различения сторон).</summary>
    [Flags]
    public enum ModifierSet
    {
        None = 0,
        Shift = 1,
        Ctrl = 2,
        Alt = 4
    }

    /// <summary>Как запрещённая запись относится к модификаторам.</summary>
    public enum ModifierMask
    {
        /// <summary>Запрещена сама клавиша без модификаторов.</summary>
        None,

        /// <summary>Запрещена клавиша ровно с указанным набором модификаторов.</summary>
        Specific,

        /// <summary>Запрещена клавиша с любым непустым набором модификаторов (сама клавиша — разрешена).</summary>
        AnyNonEmpty
    }

    /// <summary>
    /// Запрещённая к назначению клавиша или комбинация. Модификаторы — общие (Ctrl покрывает левый и правый)
    /// при любом режиме различения сторон.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class ForbiddenEntry
    {
        [SerializeField, HorizontalGroup, HideLabel, Tooltip("Путь контрола Input System, напр. <Keyboard>/f4.")]
        private string control;

        [SerializeField, HorizontalGroup(120), HideLabel]
        private ModifierMask mask;

        [SerializeField, HorizontalGroup(160), HideLabel, ShowIf(nameof(mask), ModifierMask.Specific)]
        private ModifierSet modifiers;

        public ForbiddenEntry()
        {
        }

        public ForbiddenEntry(string control, ModifierMask mask = ModifierMask.None,
            ModifierSet modifiers = ModifierSet.None)
        {
            this.control = control;
            this.mask = mask;
            this.modifiers = modifiers;
        }

        public string Control => control;

        public ModifierMask Mask => mask;

        public ModifierSet Modifiers => modifiers;
    }
}

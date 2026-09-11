using System;
using System.Linq;
using UnityEngine.InputSystem;

namespace Vortex.Sdk.RebindSystem.Model
{
    /// <summary>
    /// Слот — позиция в списке кнопок команды внутри группы устройств. Адрес — <see cref="BindKey"/>
    /// (<c>Карта/Экшен#Группа#N</c>). Снаружи только для чтения; меняет слот исключительно контроллер системы.
    /// </summary>
    public sealed class BindSlot
    {
        internal BindSlot(string commandId, string map, string group, int index)
        {
            CommandId = commandId;
            Map = map;
            Group = group;
            Index = index;
            BindKey = MakeKey(commandId, group, index);
            Value = BindingValue.Empty;
        }

        public string BindKey { get; }

        public string CommandId { get; }

        public string Map { get; }

        public string Group { get; }

        public int Index { get; }

        /// <summary>
        /// Значение слота. Для слота неактивной группы — значение раскладки, хотя в игре оно не срабатывает.
        /// </summary>
        public BindingValue Value { get; internal set; }

        /// <summary>Заводское значение. <c>null</c> — в заводской раскладке слота не было.</summary>
        public BindingValue Default { get; internal set; }

        /// <summary>
        /// Слот назначен игроком явно. Сохраняется в снимке даже при совпадении с заводским — изменение
        /// заводской клавиши в обновлении его не затрагивает. Снимается сбросом.
        /// </summary>
        public bool IsExplicit { get; internal set; }

        public SlotConflict Conflict { get; internal set; }

        public SlotOrigin Origin
        {
            get
            {
                if (Default == null)
                    return Value.IsEmpty ? SlotOrigin.Empty : SlotOrigin.Added;
                if (Value.IsEmpty)
                    return SlotOrigin.Cleared;
                return Value.Equals(Default) ? SlotOrigin.Default : SlotOrigin.Overridden;
            }
        }

        /// <summary>Layout устройства триггера (Ctrl+ЛКМ — мышь). <c>null</c> — слот пуст.</summary>
        public string DeviceLayout
        {
            get
            {
                if (Value.IsEmpty)
                    return null;
                try
                {
                    return InputControlPath.TryGetDeviceLayout(Value.Trigger);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        /// <summary>Сырая строка отображения Input System («Ctrl+A»). Локализация и глифы — забота вьюшки.</summary>
        public string GetDisplayString()
        {
            if (Value.IsEmpty)
                return string.Empty;
            return string.Join("+", Value.Modifiers.Append(Value.Trigger).Select(path =>
                InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice)));
        }

        /// <summary>Id заводского биндинга в ассете. <see cref="Guid.Empty"/> — заводского нет.</summary>
        internal Guid FactoryBindingId { get; set; }

        /// <summary>
        /// Имя вспомогательного биндинга, добавленного системой в экшен (добавленный слот или смена формы
        /// значения — простая клавиша против комбинации). <c>null</c> — не добавлялся.
        /// </summary>
        internal string AuxName { get; set; }

        /// <summary>Текущая сигнатура — ключ слота в индексе занятости. <c>null</c> — слот пуст.</summary>
        internal string Signature { get; set; }

        internal static string MakeKey(string commandId, string group, int index) => $"{commandId}#{group}#{index}";

        /// <summary>Разбор адреса слота. Разбирается с конца: имя экшена в принципе может содержать «#».</summary>
        internal static bool TryParseKey(string bindKey, out string commandId, out string group, out int index)
        {
            commandId = null;
            group = null;
            index = -1;
            if (string.IsNullOrEmpty(bindKey))
                return false;

            var last = bindKey.LastIndexOf('#');
            if (last <= 0 || !int.TryParse(bindKey.Substring(last + 1), out index))
                return false;

            var middle = bindKey.LastIndexOf('#', last - 1);
            if (middle <= 0)
                return false;

            commandId = bindKey.Substring(0, middle);
            group = bindKey.Substring(middle + 1, last - middle - 1);
            return group.Length > 0;
        }
    }
}

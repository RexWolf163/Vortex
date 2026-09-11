using System;
using UnityEngine.InputSystem;
using Vortex.Sdk.RebindSystem.Model;

namespace Vortex.Sdk.RebindSystem.Controllers
{
    /// <summary>
    /// Единственное место, которое пишет в ассет Input System.
    ///
    /// Заводские значения не трогаются: изменения игрока ложатся слоем переопределений
    /// (<c>overridePath</c> — несериализуемое поле, в файл ассета не попадает). Сброс — снятие
    /// переопределения. Неактивная раскладка и снятая клавиша — переопределение в пустой путь.
    ///
    /// Реальный биндинг добавляется только если переопределить нечего: слот сверх заводских или форма
    /// значения не совпадает с заводской (простая клавиша против комбинации — переопределением не
    /// превратить). Тогда заводской гасится, а в экшен добавляется вспомогательный биндинг с именем-маркером.
    /// Добавление помечает ассет грязным, и Input System сам переимпортирует его с диска при выходе из
    /// Play Mode — в редакторе в заводской ассет ничего не утекает.
    ///
    /// Контракт: биндинги проектного ассета меняет только система. Изменения в обход не отслеживаются —
    /// следующая запись системы их перекроет.
    /// </summary>
    internal static class AssetWriter
    {
        /// <summary>Префикс имён вспомогательных биндингов системы.</summary>
        internal const string Marker = "VortexRebind";

        /// <summary>
        /// Чистый лист: снять все переопределения и стереть вспомогательные биндинги системы. Нужен при
        /// повторном запуске без перезагрузки домена — иначе состояние прошлой сессии осталось бы на ассете.
        /// </summary>
        internal static void CleanSlate(InputActionAsset asset)
        {
            if (asset == null)
                return;

            asset.RemoveAllBindingOverrides();
            foreach (var map in asset.actionMaps)
            foreach (var action in map.actions)
            {
                // С конца: стирание составного уносит его части, стоящие после него.
                for (var i = action.bindings.Count - 1; i >= 0; i--)
                {
                    var binding = action.bindings[i];
                    if (!binding.isPartOfComposite && binding.name != null
                                                   && binding.name.StartsWith(Marker, StringComparison.Ordinal))
                        action.ChangeBinding(i).Erase();
                }
            }
        }

        /// <summary>Привести ассет к состоянию слота. Неактивная группа — клавиша не срабатывает.</summary>
        internal static void Apply(RebindCommand command, BindSlot slot, bool active)
        {
            var action = command?.Action;
            if (action == null)
                return;

            var value = active ? slot.Value : BindingValue.Empty;
            RemoveAux(action, slot);

            var factoryIndex = FindById(action, slot.FactoryBindingId);
            if (factoryIndex < 0)
            {
                if (!value.IsEmpty)
                    AddAux(action, slot, value);
                return;
            }

            if (value.IsEmpty)
            {
                SetFactory(action, factoryIndex, null, disable: true);
                return;
            }

            if (slot.Default != null && value.Equals(slot.Default))
            {
                ResetFactory(action, factoryIndex);
                return;
            }

            if (slot.Default != null && slot.Default.Modifiers.Count == value.Modifiers.Count)
            {
                SetFactory(action, factoryIndex, value, disable: false);
                return;
            }

            SetFactory(action, factoryIndex, null, disable: true);
            AddAux(action, slot, value);
        }

        /// <summary>
        /// Записать значение поверх заводского биндинга той же формы либо погасить его (пустой путь).
        /// У составного пишутся части: модификаторы по порядку, триггер — в часть binding/button.
        /// </summary>
        private static void SetFactory(InputAction action, int index, BindingValue value, bool disable)
        {
            var bindings = action.bindings;
            if (!bindings[index].isComposite)
            {
                action.ApplyBindingOverride(index, disable ? string.Empty : value.Trigger);
                return;
            }

            var modifier = 0;
            for (var i = index + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
            {
                if (disable)
                {
                    action.ApplyBindingOverride(i, string.Empty);
                    continue;
                }

                var partName = bindings[i].name ?? string.Empty;
                var isModifier = partName.StartsWith("modifier", StringComparison.OrdinalIgnoreCase);
                var path = isModifier
                    ? modifier < value.Modifiers.Count ? value.Modifiers[modifier++] : string.Empty
                    : value.Trigger;
                action.ApplyBindingOverride(i, path);
            }
        }

        /// <summary>Снять переопределения заводского биндинга (у составного — со всех частей).</summary>
        private static void ResetFactory(InputAction action, int index)
        {
            var bindings = action.bindings;
            action.RemoveBindingOverride(index);
            if (!bindings[index].isComposite)
                return;
            for (var i = index + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
                action.RemoveBindingOverride(i);
        }

        private static void AddAux(InputAction action, BindSlot slot, BindingValue value)
        {
            var name = $"{Marker}:{Guid.NewGuid():N}";
            if (value.Modifiers.Count == 0)
            {
                action.AddBinding(value.Trigger).WithName(name);
            }
            else
            {
                if (value.Modifiers.Count == 1)
                    action.AddCompositeBinding("OneModifier")
                        .With("modifier", value.Modifiers[0])
                        .With("binding", value.Trigger);
                else
                    action.AddCompositeBinding("TwoModifiers")
                        .With("modifier1", value.Modifiers[0])
                        .With("modifier2", value.Modifiers[1])
                        .With("binding", value.Trigger);

                // Составной дописывается в конец — это последний составной экшена.
                var index = LastCompositeIndex(action);
                if (index >= 0)
                    action.ChangeBinding(index).WithName(name);
            }

            slot.AuxName = name;
        }

        private static void RemoveAux(InputAction action, BindSlot slot)
        {
            if (string.IsNullOrEmpty(slot.AuxName))
                return;
            var index = FindByName(action, slot.AuxName);
            if (index >= 0)
                action.ChangeBinding(index).Erase();
            slot.AuxName = null;
        }

        // Индексы биндингов сдвигаются после каждого добавления/удаления — ищем всякий раз заново.
        private static int FindById(InputAction action, Guid id)
        {
            if (id == Guid.Empty)
                return -1;
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
                if (bindings[i].id == id)
                    return i;
            return -1;
        }

        private static int FindByName(InputAction action, string name)
        {
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
                if (!bindings[i].isPartOfComposite && bindings[i].name == name)
                    return i;
            return -1;
        }

        private static int LastCompositeIndex(InputAction action)
        {
            var bindings = action.bindings;
            for (var i = bindings.Count - 1; i >= 0; i--)
                if (bindings[i].isComposite)
                    return i;
            return -1;
        }
    }
}

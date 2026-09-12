using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
using UnityEngine.InputSystem;
#endif

namespace Vortex.Sdk.RebindSystem.Presets
{
    /// <summary>
    /// Группа устройств: делит слоты команды по устройствам и служит сменной раскладкой.
    ///
    /// Типы устройств — имена layout'ов Input System; сопоставление с учётом наследования (<c>Gamepad</c>
    /// покрывает XInput, DualShock, DualSense, Switch Pro). Пересечение типов между группами допустимо — так
    /// задаются сменные раскладки одного устройства. Группы с общим ключом набора взаимоисключающие: активна
    /// не больше одной.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class DeviceGroupSettings
    {
        [SerializeField, Tooltip("Ключ группы: входит в адрес слота (Карта/Экшен#Группа#N) и в снимок. Без # и /.")]
        private string key;

        // Имя источника строкой, не nameof: метод живёт под UNITY_EDITOR, в билде имени нет.
        [SerializeField, ValueDropdown("DeviceLayoutsList"), Tooltip("Layout'ы устройств Input System.")]
        private string[] deviceLayouts = new string[0];

        [SerializeField, Min(1), Tooltip("Число слотов команды в этой группе.")]
        private int slots = 2;

        [SerializeField, Tooltip("Ключ набора переключения. Группы с общим ключом — взаимоисключающие раскладки. " +
                                 "Пусто — группа самостоятельна.")]
        private string switchSet;

        public DeviceGroupSettings()
        {
        }

        public DeviceGroupSettings(string key, int slots, params string[] deviceLayouts)
        {
            this.key = key;
            this.slots = slots;
            this.deviceLayouts = deviceLayouts;
        }

        public string Key => key;

        public IReadOnlyList<string> DeviceLayouts => deviceLayouts ?? Array.Empty<string>();

        public int Slots => slots;

        public string SwitchSet => switchSet;

#if UNITY_EDITOR
        private IEnumerable<string> DeviceLayoutsList() => RebindEditorLists.DeviceLayouts();
#endif
    }

#if UNITY_EDITOR
    /// <summary>Списки для выпадашек инспектора конфига.</summary>
    internal static class RebindEditorLists
    {
        /// <summary>Id всех команд проектного ассета ввода: «Карта/Экшен».</summary>
        public static IEnumerable<string> CommandIds()
        {
            var asset = InputSystem.actions;
            if (asset == null)
                return Array.Empty<string>();
            return asset.actionMaps.SelectMany(m => m.actions.Select(a => $"{m.name}/{a.name}"));
        }

        /// <summary>Имена layout'ов устройств, зарегистрированных в Input System.</summary>
        public static IEnumerable<string> DeviceLayouts()
        {
            foreach (var name in InputSystem.ListLayouts())
            {
                Type type = null;
                try
                {
                    type = InputSystem.LoadLayout(name)?.type;
                }
                catch (Exception)
                {
                    // layout, который не собирается без устройства, в выпадашку не попадает
                }

                if (type != null && typeof(InputDevice).IsAssignableFrom(type))
                    yield return name;
            }
        }
    }
#endif
}

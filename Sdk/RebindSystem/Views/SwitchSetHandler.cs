using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Presets;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Переключение раскладки внутри набора (<see cref="DeviceGroupSettings.SwitchSet"/>): <see cref="Next"/> и
    /// <see cref="Previous"/> вешаются на кнопки и активируют соседнюю группу набора по порядку конфига —
    /// остальные группы набора система выключает сама. Название активной группы — в необязательный UIComponent.
    /// </summary>
    public class SwitchSetHandler : MonoBehaviour
    {
        // Имя источника строкой, не nameof: метод живёт под UNITY_EDITOR, в билде имени нет.
        [SerializeField, ValueDropdown("SwitchSets"), Tooltip("Ключ набора переключения из RebindSettings.")]
        private string switchSet;

        /// <summary>Название активной группы. Необязательно.</summary>
        [SerializeField] private UIComponent title;

        [SerializeField, Tooltip("Шаблон названия: {0} — ключ активной группы. Проходит локализацию UIComponent. " +
                                 "В наборе нет активной — пустая строка.")]
        private string titlePattern = "{0}";

        private void OnEnable()
        {
            RebindBus.OnGroupsChanged += Refresh;
            RebindBus.OnRebuilt += Refresh;
            // До загрузки обновит OnRebuilt, который система поднимает по её завершении.
            if (RebindBus.IsReady)
                Refresh();
        }

        private void OnDisable()
        {
            RebindBus.OnGroupsChanged -= Refresh;
            RebindBus.OnRebuilt -= Refresh;
        }

        public void Next() => Shift(1);

        public void Previous() => Shift(-1);

        /// <summary>Активировать соседнюю группу набора. В наборе нет активной — первую.</summary>
        private void Shift(int step)
        {
            var groups = SetGroups();
            if (groups.Count == 0)
                return;

            var current = ActiveIndex(groups);
            var next = current < 0 ? 0 : (current + step + groups.Count) % groups.Count;
            RebindBus.Controller.SetGroupActive(groups[next].Key, true);
        }

        private void Refresh()
        {
            if (title == null)
                return;
            var groups = SetGroups();
            var current = ActiveIndex(groups);
            title.SetText(current < 0 ? string.Empty : string.Format(titlePattern, groups[current].Key));
        }

        private List<DeviceGroupSettings> SetGroups()
        {
            // Группы без набора самостоятельны: включение одной не выключает другие — переключения не получится.
            if (string.IsNullOrEmpty(switchSet))
            {
                Debug.LogError("[SwitchSetHandler] Не задан ключ набора переключения.", this);
                return new List<DeviceGroupSettings>();
            }

            var groups = RebindBus.Data.Groups.Where(g => g.SwitchSet == switchSet).ToList();
            if (groups.Count == 0)
                Debug.LogError($"[SwitchSetHandler] В наборе «{switchSet}» нет групп.", this);
            return groups;
        }

        private static int ActiveIndex(List<DeviceGroupSettings> groups) =>
            groups.FindIndex(g => RebindBus.Data.IsGroupActive(g.Key));

#if UNITY_EDITOR
        private IEnumerable<string> SwitchSets() =>
            Resources.LoadAll<RebindSettings>("").FirstOrDefault()?.Groups
                .Select(g => g.SwitchSet).Where(s => !string.IsNullOrEmpty(s)).Distinct()
            ?? Enumerable.Empty<string>();
#endif
    }
}

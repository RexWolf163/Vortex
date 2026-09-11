using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Unity.UI.PoolSystem;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
using Vortex.Sdk.RebindSystem.Presets;
#endif

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Хэндлер группы устройств: выводит в пул обслуживаемые команды своих карт. Данные элемента пула —
    /// команда (<see cref="Model.RebindCommand"/>) и группа (<see cref="Presets.DeviceGroupSettings"/>);
    /// их читает <see cref="RebindCommandView"/>.
    ///
    /// Пул перезаполняется целиком по «пересобрано всё»: при загрузке, сбросе карты или всех изменений и импорте
    /// слоты модели заменяются.
    /// </summary>
    public class RebindGroupHandler : MonoBehaviour
    {
        // Имена источников строкой, не nameof: методы живут под UNITY_EDITOR, в билде имён нет.
        [SerializeField, ValueDropdown("GroupKeys"), Tooltip("Ключ группы устройств из RebindSettings.")]
        private string groupKey;

        [SerializeField, ValueDropdown("MapNames"), Tooltip("Карты ввода, команды которых выводятся — по порядку.")]
        private string[] maps;

        [SerializeField] private Pool pool;

        private void OnEnable()
        {
            RebindBus.OnRebuilt += Fill;
            // До загрузки заполнит OnRebuilt, который система поднимает по её завершении.
            if (RebindBus.IsReady)
                Fill();
        }

        private void OnDisable()
        {
            RebindBus.OnRebuilt -= Fill;
            pool.Clear();
        }

        private void Fill()
        {
            pool.Clear();

            var data = RebindBus.Data;
            var group = data.Groups.FirstOrDefault(g => g.Key == groupKey);
            if (group == null)
            {
                Debug.LogError($"[RebindGroupHandler] Группы «{groupKey}» нет в RebindSettings.", this);
                return;
            }

            foreach (var map in maps)
            foreach (var command in data.GetCommands(map))
                if (command.Serviced)
                    pool.AddItem(command, group);
        }

#if UNITY_EDITOR
        private IEnumerable<string> GroupKeys() =>
            Resources.LoadAll<RebindSettings>("").FirstOrDefault()?.Groups.Select(g => g.Key)
            ?? Enumerable.Empty<string>();

        private IEnumerable<string> MapNames() =>
            InputSystem.actions != null
                ? InputSystem.actions.actionMaps.Select(m => m.name)
                : Enumerable.Empty<string>();
#endif
    }
}

using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.RebindSystem.Model;
using Vortex.Sdk.RebindSystem.Presets;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.PoolSystem;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Представление команды в группе устройств: название и слоты группы в пуле. Данные — из элемента пула
    /// <see cref="RebindGroupHandler"/>: команда и группа.
    ///
    /// Выводятся слоты с индексом меньше X группы: слоты сверх X возможны только из загрузки, операции к ним не
    /// обращаются, первая же операция над командой их опустошит.
    /// </summary>
    public class RebindCommandView : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        /// <summary>Название команды. Необязательно.</summary>
        [SerializeField] private UIComponent title;

        [SerializeField, Tooltip("Шаблон названия: {0} — id «Карта/Экшен», {1} — карта, {2} — экшен. " +
                                 "Проходит локализацию UIComponent.")]
        private string titlePattern = "{0}";

        [SerializeField] private Pool slots;

        private void OnEnable()
        {
            Storage.OnUpdateLink += UpdateLink;
            Init();
        }

        private void OnDisable()
        {
            DeInit();
            Storage.OnUpdateLink -= UpdateLink;
        }

        private void Init()
        {
            var command = Storage.GetData<RebindCommand>();
            var group = Storage.GetData<DeviceGroupSettings>();
            if (command == null || group == null)
                return;

            var action = command.Id.Substring(command.Map.Length + 1);
            title?.SetText(string.Format(titlePattern, command.Id, command.Map, action));

            foreach (var slot in command.GetSlots(group.Key))
                if (slot.Index < group.Slots)
                    slots.AddItem(slot);
        }

        private void DeInit() => slots.Clear();

        private void UpdateLink()
        {
            DeInit();
            Init();
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.RebindSystem.Bus;
using Vortex.Sdk.RebindSystem.Model;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Представление слота: строка клавиши, клик открывает перехват нажатия для слота. Данные — слот из элемента
    /// пула <see cref="RebindCommandView"/>.
    ///
    /// Строка клавиши — сырая строка Input System («Ctrl+A»);
    /// выключить. Свитчеры необязательны: конфликт (<see cref="SlotConflict"/>), происхождение
    /// (<see cref="SlotOrigin"/>), ожидание нажатия для этого слота (<see cref="SwitcherState"/>).
    /// </summary>
    public class RebindSlotView : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        /// <summary>Текст клавиши.</summary>
        [SerializeField, AutoLink] private UIComponent component;

        [SerializeField, StateSwitcher(typeof(SlotConflict))]
        private UIStateSwitcher conflictSwitcher;

        [SerializeField, StateSwitcher(typeof(SlotOrigin))]
        private UIStateSwitcher originSwitcher;

        [SerializeField, StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher captureSwitcher;

        private BindSlot _slot;

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
            _slot = Storage.GetData<BindSlot>();
            if (_slot == null)
                return;

            RebindBus.OnSlotsChanged += OnSlotsChanged;
            RebindBus.OnCaptureChanged += OnCaptureChanged;
            Refresh();
            OnCaptureChanged(RebindBus.Capture);
        }

        private void DeInit()
        {
            RebindBus.OnSlotsChanged -= OnSlotsChanged;
            RebindBus.OnCaptureChanged -= OnCaptureChanged;
            _slot = null;
        }

        private void UpdateLink()
        {
            DeInit();
            Init();
        }

        public void SaveNewKey()
        {
            var reason = RebindBus.Controller.SaveSignalForBind(_slot.BindKey);
            if (reason != RejectReason.None)
                Debug.LogError($"[RebindSlotView] Перехват для «{_slot.BindKey}» не открыт: {reason}.", this);
        }

        private void OnSlotsChanged(IReadOnlyList<string> bindKeys)
        {
            foreach (var key in bindKeys)
            {
                if (key != _slot.BindKey)
                    continue;
                Refresh();
                return;
            }
        }

        private void Refresh()
        {
            component.SetText(_slot.GetDisplayString());
            conflictSwitcher?.Set(_slot.Conflict);
            originSwitcher?.Set(_slot.Origin);
        }

        private void OnCaptureChanged(CaptureValve valve)
        {
            var waiting = valve is { IsOpen: true } && valve.BindKey == _slot.BindKey;
            captureSwitcher?.Set(waiting ? SwitcherState.On : SwitcherState.Off);
        }
    }
}

using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Vortex.Unity.UI.StateSwitcher.Items
{
    /// <summary>
    /// Запуск функции при включении стейта
    /// </summary>
    [Serializable]
    public class EventFire : StateItem
    {
        [SerializeField, OnValueChanged("CorrectEventsSettings", true)]
        private UnityEvent events;

        public override void Set() => events.Invoke();

        public override void DefaultState()
        {
        }

#if UNITY_EDITOR

        private void CorrectEventsSettings()
        {
            var c = events.GetPersistentEventCount();
            for (var i = 0; i < c; i++)
                events.SetPersistentListenerState(i, UnityEventCallState.EditorAndRuntime);
        }

        public override StateItem Clone()
        {
            var clone = new EventFire
            {
                events = events,
            };
            return clone;
        }

        public override string DropDownGroupName => "Events";

        public override string DropDownItemName => "FireEvent";
#endif
    }
}
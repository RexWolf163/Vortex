using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Core.System.Enums;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using InfoMessageType = Vortex.Unity.EditorTools.Attributes.InfoMessageType;
using Object = System.Object;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Хранилище данных.
    /// Может хранить НЕСКОЛЬКО объектов одновременно.
    /// При запросе данных вернется ПЕРВЫЙ подходящий по типу по принципу FIFO
    /// </summary>
    public class DataStorage : MonoBehaviour, IDataStorage
    {
        [SerializeField]
        [InfoBubble("$GetContent", InfoMessageType.None, "HideIf")]
        [InfoBubble("Переключает свитчер по факту наличия-отсутствия данных в контейнере\n<b>Опционально</b>")]
        [StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher dataSwitcher;

        private List<Object> _data = new();
        public event Action OnUpdateLink;

        private void OnEnable() => dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);

        public void SetData(Object data)
        {
            _data.Clear();
            _data.Add(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
            OnUpdateLink?.Invoke();
        }

        public void SetData(Object[] data)
        {
            _data.Clear();
            if (data is { Length: > 0 })
                _data.AddRange(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
            OnUpdateLink?.Invoke();
        }

        public void AddData(Object data)
        {
            foreach (var o in _data.ToArray())
            {
                if (o.GetType() != data.GetType())
                    continue;
                _data.Remove(o);
            }

            _data.Add(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
        }

        public T GetData<T>() where T : class => _data.FirstOrDefault(o => o is T) as T;

        private bool IsEmpty()
        {
            if (_data == null || _data.Count == 0)
                return true;
            foreach (var o in _data)
                if (o == null)
                    return true;

            return false;
        }

#if UNITY_EDITOR

        private string GetContent() => string.Join('\n', _data?.Select(o => o != null ? GetTypeLabel(o) : "[NULL]"));

        private string GetTypeLabel(object o)
        {
            var type = o.GetType();
            var name = type.Name;
            switch (o)
            {
                case IntData intData:
                    return $"{name}: {intData.Value}";
                case FloatData floatData:
                    return $"{name}: {floatData.Value}";
                case BoolData boolData:
                    return $"{name}: {(boolData.Value ? "TRUE" : "FALSE")}";
                case StringData stringData:
                    return $"{name}: «{stringData.Value[..12]}»";
                case string str:
                    return $"{name}: «{str[..12]}»";
            }

            return name;
        }

        private bool HideIf() => App.GetState() == AppStates.None;
#endif
    }
}
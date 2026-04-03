using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
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
        [ShowInInspector, HideInEditorMode, DisplayAsString]
        private string InStorage => string.Join('\n', _data?.Select(o => o?.GetType().Name ?? "[NULL]"));

        [SerializeField]
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
    }
}
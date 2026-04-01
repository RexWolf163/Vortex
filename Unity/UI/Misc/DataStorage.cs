using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
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

        private List<Object> _data = new();
        public event Action OnUpdateLink;

        public void SetData(Object data)
        {
            _data.Clear();
            _data.Add(data);
            OnUpdateLink?.Invoke();
        }

        public void SetData(Object[] data)
        {
            _data.Clear();
            if (data is { Length: > 0 })
                _data.AddRange(data);
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
        }

        public T GetData<T>() where T : class => _data.FirstOrDefault(o => o is T) as T;
    }
}
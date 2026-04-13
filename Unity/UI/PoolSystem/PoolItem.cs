using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.UI.PoolSystem
{
    /// <summary>
    /// Элемент-Контейнер для пула
    /// Реализует интерфейс IDataStorage. Хранит инициализирующие данные
    /// </summary>
    public class PoolItem : MonoBehaviour, IDataStorage
    {
        [ShowInInspector, HideInEditorMode, Multiline(5), ReadOnly]
        private string InStorage => string.Join('\n', _data?.Select(o => o?.GetType().Name ?? "[NULL]"));

        private List<object> _data = new();

        private Pool _owner;

        private object _key;

        public event Action OnUpdateLink;
        public T GetData<T>() where T : class => _data.FirstOrDefault(o => o is T) as T;

        internal void MakeLink(object data, Pool pool)
        {
            _data.Clear();
            _key = data;
            _owner = pool;
            switch (data)
            {
                case null:
                    break;
                case Array ar:
                    foreach (var o in ar)
                        _data.Add(o);
                    break;
                default:
                    _data.Add(data);
                    break;
            }

            gameObject.SetActive(_key != null);
            OnUpdateLink?.Invoke();
        }

        internal void Remove() => MakeLink(null, _owner);

        private void OnEnable() => CheckState();

        private void OnDisable()
        {
            TimeController.RemoveCall(this);
            if (_key != null && _owner != null)
                SelfDestroy();
        }

        private void CheckState() => gameObject.SetActive(_key != null);

        private void SelfDestroy()
        {
            if (_key != null && _owner != null)
                _owner.RemoveItem(_key);
        }

        private void OnDestroy() => TimeController.RemoveCall(this);
    }
}
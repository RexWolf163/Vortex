using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Unity.UI.PoolSystem
{
    public class Pool : MonoBehaviour
    {
        /// <summary>
        /// Образец контейнера
        /// </summary>
        [SerializeField] private PoolItem preset;

        /// <summary>
        /// Реестр активных контейнеров
        /// </summary>
        private readonly Dictionary<object, PoolItem> _index = new();

        /// <summary>
        /// Перечень-очередь обнуленных контейнеров
        /// </summary>
        private readonly Queue<PoolItem> _freeItems = new();

        private void Awake()
        {
            _index.Clear();
            var list = GetComponentsInChildren<PoolItem>();
            foreach (var item in list)
                _freeItems.Enqueue(item);
        }

        private void OnDestroy() => Clear();

        /// <summary>
        /// Добавить элемент пула для данных
        /// </summary>
        /// <param name="data"></param>
        public void AddItem(params object[] data) => AddItem(data, false);

        /// <summary>
        /// Добавить элемент пула для данных
        /// (в начало списка)
        /// </summary>
        /// <param name="data"></param>
        public void AddItemBefore(params object[] data) => AddItem(data, true);

        /// <summary>
        /// Добавить элемент пула для данных
        /// </summary>
        /// <param name="data"></param>
        public void AddItem(object data, bool before = false)
        {
            if (_index.ContainsKey(data))
                return;
            var item = CreateItem();
            if (before)
                item.transform.SetAsFirstSibling();
            else
                item.transform.SetAsLastSibling();

            item.transform.localPosition = new Vector3(1000000f, 1000000f, 0);

            _index.AddNew(data, item);
            item.MakeLink(data, this);
        }

        /// <summary>
        /// Добавить элемент пула для данных
        /// Вернуть компонент указанного типа из контейнера
        /// </summary>
        /// <param name="data"></param>
        public T AddItem<T>(params object[] data) where T : MonoBehaviour => AddItem<T>(data, false);

        /// <summary>
        /// Добавить элемент пула для данных
        /// (в начало списка)
        /// Вернуть компонент указанного типа из контейнера
        /// </summary>
        /// <param name="data"></param>
        public T AddItemBefore<T>(params object[] data) where T : MonoBehaviour => AddItem<T>(data, true);


        /// <summary>
        /// Добавить элемент пула для данных.
        /// Вернуть компонент указанного типа из контейнера
        /// </summary>
        /// <param name="data"></param>
        /// <param name="before">вставить в начало иерархии</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T AddItem<T>(object data, bool before = false) where T : MonoBehaviour
        {
            if (_index.ContainsKey(data))
                return GetItem<T>(data);
            AddItem(data, before);
            return GetItem<T>(data);
        }

        /// <summary>
        /// Вернуть компонент указанного типа из контейнера связанного с данными
        /// </summary>
        /// <param name="data"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetItem<T>(object data) where T : MonoBehaviour
        {
            if (!_index.TryGetValue(data, out var item))
                return null;
            var res = item.GetComponent<T>();
            if (res == null)
                res = item.GetComponentInChildren<T>();
            return res;
        }

        /// <summary>
        /// Создать новый контейнер в пуле
        /// </summary>
        /// <returns></returns>
        private PoolItem CreateItem()
        {
            if (_freeItems.Count > 0)
                return _freeItems.Dequeue();

            var item = Instantiate(preset, new Vector3(1000000f, 1000000f, 0), Quaternion.identity, transform);
            item.gameObject.SetActive(false);
            return item;
        }

        /// <summary>
        /// Удалить контейнер
        /// </summary>
        /// <param name="data"></param>
        public void RemoveItem(object data)
        {
            if (!_index.TryGetValue(data, out var value))
                return;
            value.Remove();
            _freeItems.Enqueue(value);
            _index.Remove(data);
        }

        /// <summary>
        /// Удалить все элементы возвращающие из колбэка true
        /// </summary>
        /// <param name="callback"></param>
        public void RemoveByCallback(Func<object, bool> callback)
        {
            var list = _index.Keys.ToList();
            foreach (var key in list)
                if (callback.Invoke(key))
                    RemoveItem(key);
        }

        /// <summary>
        /// очистка контейнера
        /// </summary>
        public void Clear()
        {
            var list = _index.Keys.ToArray();
            foreach (var data in list)
                RemoveItem(data);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.Misc.DataOrchestratorSystem
{
    /// <summary>
    /// Базовый класс для распределения модели данных по контейнерам DataStorage.
    /// Наследник реализует Map/Unmap для привязки свойств модели к контейнерам.
    /// </summary>
    /// <typeparam name="T">Тип модели данных</typeparam>
    public abstract class DataOrchestrator<T> : MonoBehaviour where T : class
    {
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        protected T Data { get; private set; }

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
            Data = Storage?.GetData<T>();
            if (Data == null)
                return;
            Subscribe(Data);
            Map(Data);
        }

        private void DeInit()
        {
            if (Data != null)
            {
                Unmap();
                Unsubscribe(Data);
            }

            ClearStorages();
            Data = null;
        }

        private void UpdateLink()
        {
            DeInit();
            Init();
        }

        /// <summary>
        /// Привязать свойства модели к контейнерам DataStorage
        /// </summary>
        protected abstract void Map(T data);

        /// <summary>
        /// Отвязать подписки от модели данных
        /// </summary>
        protected abstract void Unmap();

        /// <summary>
        /// Подписка на события модели данных.
        /// Переопределите для добавления собственных подписок
        /// </summary>
        protected virtual void Subscribe(T data)
        {
        }

        /// <summary>
        /// Отписка от событий модели данных.
        /// Переопределите для снятия собственных подписок
        /// </summary>
        protected virtual void Unsubscribe(T data)
        {
        }

        /// <summary>
        /// Модель данных изменилась.
        /// Переопределите для обработки обновлений
        /// </summary>
        protected virtual void OnDataUpdated()
        {
        }

        /// <summary>
        /// Записать данные в контейнер
        /// </summary>
        protected void Push(DataStorage storage, object data)
        {
            storage?.SetData(data);
        }

        private void ClearStorages()
        {
            var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                if (field.FieldType != typeof(DataStorage))
                    continue;
                var storage = field.GetValue(this) as DataStorage;
                storage?.SetData(null);
            }
        }

#if UNITY_EDITOR
        [Button("Generate Hierarchy"), PropertyOrder(-1)]
        private void GenerateHierarchy()
        {
            var fields = GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var created = new List<string>();

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(DataStorage))
                    continue;

                var existing = field.GetValue(this) as DataStorage;
                if (existing != null)
                    continue;

                var childName = field.Name.TrimStart('_');
                var child = transform.Find(childName)?.gameObject;
                if (child == null)
                {
                    child = new GameObject(childName);
                    child.transform.SetParent(transform, false);
                    RemoveRectTransform(child);
                }

                var storage = child.GetComponent<DataStorage>();
                if (storage == null)
                    storage = child.AddComponent<DataStorage>();

                field.SetValue(this, storage);
                created.Add(childName);
            }

            if (created.Count > 0)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(gameObject);
                Debug.Log($"[DataOrchestrator] Created {created.Count} storages: {string.Join(", ", created)}");
            }
            else
            {
                Debug.Log("[DataOrchestrator] All storages already linked.");
            }
        }

        private static void RemoveRectTransform(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            if (rect != null)
                DestroyImmediate(rect);
        }
#endif
    }
}

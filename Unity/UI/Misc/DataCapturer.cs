using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Класс для захвата в контейнер значений в свойстве класса-источника
    /// </summary>
    public class DataCapturer : MonoBehaviour, IDataStorage
    {
        public event Action OnUpdateLink;

        [SerializeField] private MonoBehaviour source;

        [SerializeField, ValueSelector("GetListProperties")]
        private string property;

        private PropertyInfo _property;

        [ShowInInspector, HideInEditorMode, HideLabel, FoldoutGroup("Data")]
        private object _propertyValue;

        private void Awake()
        {
            var type = source.GetType();
            _property = type.GetProperty(property);
            if (_property == null)
            {
                Debug.LogError($"[DataCapturer] {name}: {property} property not found in type {source.GetType().Name}");
            }

            if (source is IReactiveData reactSource)
                reactSource.OnUpdateData += RefreshLink;
        }

        private void Start() => RefreshLink();

        private void OnDestroy()
        {
            if (source is IReactiveData reactSource)
                reactSource.OnUpdateData -= RefreshLink;
            _propertyValue = null;
            _property = null;
        }

        private void RefreshLink()
        {
            var newProp = _property?.GetValue(source, null);
            var b = !ReferenceEquals(newProp, _propertyValue);
            _propertyValue = newProp;
            if (b && _propertyValue != null)
                OnUpdateLink?.Invoke();
        }

        public T GetData<T>() where T : class => _propertyValue as T;

#if UNITY_EDITOR
        /// <summary>
        /// Возвращает список свойств класса
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> GetListProperties()
        {
            var type = source.GetType();
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
            return properties.Where(p => (typeof(IReactiveData)).IsAssignableFrom(p.PropertyType))
                .OrderBy(p => p.Name)
                .ThenBy(p => p.PropertyType.Name)
                .ToDictionary(p => p.Name, p => $"{p.PropertyType.Name}/{p.Name}");
        }
#endif
    }
}
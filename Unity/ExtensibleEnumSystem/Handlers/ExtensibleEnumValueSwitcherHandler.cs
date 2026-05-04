using System;
using System.Reflection;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;
using Vortex.Core.ExtensibleEnumSystem.Extensions;
using Vortex.Unity.UI.StateSwitcher;
#if UNITY_EDITOR
using System.Linq;
#endif

namespace Vortex.Unity.ExtensibleEnumSystem.Handlers
{
    /// <summary>
    /// Мост из <see cref="ExtEnumData{T}"/> в <see cref="UIStateSwitcher"/>.
    /// Подписывается на <see cref="IReactiveData.OnUpdateData"/> от значения и при изменении
    /// дёргает <c>switcher.Set(ExtEnumData.Index)</c>.
    ///
    /// Конфигурация (рефлексивная, как в <c>DataCapturer</c>):
    /// <list type="bullet">
    /// <item>source — MonoBehaviour-источник, на котором есть свойство типа <see cref="ExtEnumData{T}"/></item>
    /// <item>property — имя этого свойства (выпадашка фильтрует по типу <see cref="ExtEnumData{T}"/>)</item>
    /// <item>switcher — целевой UIStateSwitcher</item>
    /// </list>
    /// </summary>
    public class ExtensibleEnumValueSwitcherHandler : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour source;

        [SerializeField, Tooltip("Имя свойства типа ExtEnumData<T> на source")]
        private string property;

        [SerializeField] private UIStateSwitcher switcher;

        private object _extEnumData;
        private PropertyInfo _indexProperty;
        private IReactiveData _reactive;

        private void Awake()
        {
            if (source == null || string.IsNullOrEmpty(property))
            {
                Debug.LogError($"[ExtensibleEnumValueSwitcherHandler] {name}: source or property not assigned", this);
                enabled = false;
                return;
            }

            var prop = source.GetType().GetProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[ExtensibleEnumValueSwitcherHandler] {name}: property '{property}' not found on {source.GetType().Name}", this);
                enabled = false;
                return;
            }

            _extEnumData = prop.GetValue(source);
            if (_extEnumData == null)
            {
                Debug.LogError($"[ExtensibleEnumValueSwitcherHandler] {name}: property '{property}' returned null", this);
                enabled = false;
                return;
            }

            _indexProperty = _extEnumData.GetType().GetProperty(nameof(ExtEnumData<ExtensibleEnum>.Index));
            if (_indexProperty == null)
            {
                Debug.LogError($"[ExtensibleEnumValueSwitcherHandler] {name}: '{property}' is not an ExtEnumData<>", this);
                enabled = false;
                return;
            }

            _reactive = _extEnumData as IReactiveData;
            if (_reactive != null)
                _reactive.OnUpdateData += OnDataChanged;
        }

        private void Start() => OnDataChanged();

        private void OnDestroy()
        {
            if (_reactive != null)
                _reactive.OnUpdateData -= OnDataChanged;
            _reactive = null;
            _extEnumData = null;
            _indexProperty = null;
        }

        private void OnDataChanged()
        {
            if (_indexProperty == null || switcher == null) return;
            var index = (int)_indexProperty.GetValue(_extEnumData);
            if (index >= 0)
                switcher.Set(index);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-utility: список свойств source, чей тип — наследник <see cref="ExtEnumData{T}"/>.
        /// </summary>
        private string[] GetExtEnumDataProperties()
        {
            if (source == null) return Array.Empty<string>();
            return source.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
                .Where(p => IsExtEnumDataType(p.PropertyType))
                .Select(p => p.Name)
                .ToArray();
        }

        private static bool IsExtEnumDataType(Type t)
        {
            while (t != null && t != typeof(object))
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ExtEnumData<>))
                    return true;
                t = t.BaseType;
            }
            return false;
        }
#endif
    }
}

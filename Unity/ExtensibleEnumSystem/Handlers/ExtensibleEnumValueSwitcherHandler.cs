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
    /// дёргает <c>switcher.Set(stateValue.Index)</c>.
    ///
    /// Конфигурация (рефлексивная, как в <c>DataCapturer</c>):
    /// <list type="bullet">
    /// <item>source — MonoBehaviour-источник, на котором есть свойство типа <see cref="ExtEnumData{T}"/></item>
    /// <item>property — имя этого свойства (выпадашка фильтрует по типу StateValue&lt;&gt;)</item>
    /// <item>switcher — целевой UIStateSwitcher</item>
    /// </list>
    /// </summary>
    public class StateValueSwitcherHandler : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour source;

        [SerializeField, Tooltip("Имя свойства типа StateValue<TAxis> на source")]
        private string property;

        [SerializeField] private UIStateSwitcher switcher;

        private object _stateValue;
        private PropertyInfo _indexProperty;
        private IReactiveData _reactive;

        private void Awake()
        {
            if (source == null || string.IsNullOrEmpty(property))
            {
                Debug.LogError($"[StateValueSwitcherHandler] {name}: source or property not assigned", this);
                enabled = false;
                return;
            }

            var prop = source.GetType().GetProperty(property);
            if (prop == null)
            {
                Debug.LogError($"[StateValueSwitcherHandler] {name}: property '{property}' not found on {source.GetType().Name}", this);
                enabled = false;
                return;
            }

            _stateValue = prop.GetValue(source);
            if (_stateValue == null)
            {
                Debug.LogError($"[StateValueSwitcherHandler] {name}: property '{property}' returned null", this);
                enabled = false;
                return;
            }

            _indexProperty = _stateValue.GetType().GetProperty(nameof(ExtEnumData<ExtensibleEnum>.Index));
            if (_indexProperty == null)
            {
                Debug.LogError($"[StateValueSwitcherHandler] {name}: '{property}' is not a StateValue<>", this);
                enabled = false;
                return;
            }

            _reactive = _stateValue as IReactiveData;
            if (_reactive != null)
                _reactive.OnUpdateData += OnStateChanged;
        }

        private void Start() => OnStateChanged();

        private void OnDestroy()
        {
            if (_reactive != null)
                _reactive.OnUpdateData -= OnStateChanged;
            _reactive = null;
            _stateValue = null;
            _indexProperty = null;
        }

        private void OnStateChanged()
        {
            if (_indexProperty == null || switcher == null) return;
            var index = (int)_indexProperty.GetValue(_stateValue);
            if (index >= 0)
                switcher.Set(index);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-utility: список свойств source, чей тип — наследник <see cref="ExtEnumData{T}"/>.
        /// </summary>
        private string[] GetStateValueProperties()
        {
            if (source == null) return Array.Empty<string>();
            return source.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
                .Where(p => IsStateValueType(p.PropertyType))
                .Select(p => p.Name)
                .ToArray();
        }

        private static bool IsStateValueType(Type t)
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

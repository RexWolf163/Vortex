using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using CoreValve = Vortex.Core.StateValve.StateValve;
using ValveMode = Vortex.Core.StateValve.ValveMode;

namespace Vortex.Unity.StateValve
{
    /// <summary>
    /// Unity-обёртка над Core-<c>StateValve</c>: создаёт клапан в <see cref="Awake"/> из сериализованных
    /// <c>mode</c>/<c>invert</c>, делегирует <see cref="Open"/>/<see cref="Close"/>, отдаёт реактивный
    /// <see cref="State"/>.
    ///
    /// <c>whiteList</c> (непустой) фильтрует входящие ключи: имя не из списка — отклонение с
    /// <c>Debug.LogError</c> (ошибка проводки не должна прятаться). Пустой список — фильтра нет.
    /// Пустой/<c>null</c> ключ проходит к ядру и там fail-fast.
    ///
    /// Реализует <see cref="IDataStorage"/> — слабая привязка: <see cref="GetData{T}"/> отдаёт
    /// <see cref="IStateValve"/> (производителям) или <see cref="BoolData"/> <see cref="State"/>
    /// (потребителям). <see cref="OnUpdateLink"/> поднимается один раз в <see cref="Awake"/> (ссылки
    /// готовы); само значение <see cref="State"/> потребитель слушает через её собственный <c>OnUpdate</c>.
    /// </summary>
    public class StateValveHandler : MonoBehaviour, IStateValve, IDataStorage
    {
        [SerializeField] private ValveMode mode = ValveMode.And;
        [SerializeField] private bool invert;

        [InfoBox("Непустой список — фильтр входящих Open/Close (ключ не из списка → Error-лог и отклонение). Пустой — без фильтра.")]
        [SerializeField] private string[] whiteList = Array.Empty<string>();

        private CoreValve _valve;

        public event Action OnUpdateLink;

        public BoolData State => _valve?.State;

        public string[] GetWhiteList() => whiteList;

        [ShowInInspector, HideInEditorMode, ReadOnly, LabelText("Keys (runtime)")]
        private IReadOnlyDictionary<string, bool> KeysDebug => _valve?.Keys;

        private void Awake()
        {
            _valve = new CoreValve(mode, invert);
            OnUpdateLink?.Invoke();
        }

        public void Open(string id)
        {
            if (Filtered(id))
                return;
            _valve.Open(id);
        }

        public void Close(string id)
        {
            if (Filtered(id))
                return;
            _valve.Close(id);
        }

        public T GetData<T>() where T : class =>
            typeof(T) == typeof(IStateValve) ? this as T :
            typeof(T) == typeof(BoolData) ? State as T :
            null;

        private bool Filtered(string id)
        {
            if (whiteList.Length == 0 || string.IsNullOrEmpty(id) || whiteList.Contains(id))
                return false;

            Debug.LogError($"[StateValve] key '{id}' is not in whiteList of '{name}' — ignored.", this);
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.DataModelSystem;
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
        [SerializeField]
        [InfoBox("Переключает свитчер по факту наличия-отсутствия данных в контейнере\n<b>Опционально</b>")]
        [StateSwitcher(typeof(SwitcherState))]
        protected UIStateSwitcher dataSwitcher;

        [DataModel, ShowInInspector, HideInEditorMode]
        protected List<Object> Data = new();

        public event Action OnUpdateLink;

        protected virtual void OnEnable() => dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);

        protected virtual void OnDisable()
        {
        }

        public void SetData(Object data)
        {
            Data.Clear();
            Data.Add(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
            OnUpdateLink?.Invoke();
        }

        public void SetData(Object[] data)
        {
            Data.Clear();
            if (data is { Length: > 0 })
                Data.AddRange(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
            OnUpdateLink?.Invoke();
        }

        public void AddData(Object data)
        {
            foreach (var o in Data.ToArray())
            {
                if (o.GetType() != data.GetType())
                    continue;
                Data.Remove(o);
            }

            Data.Add(data);
            dataSwitcher?.Set(IsEmpty() ? SwitcherState.Off : SwitcherState.On);
        }

        public T GetData<T>() where T : class => Data.FirstOrDefault(o => o is T) as T;

        /// <summary>
        /// Возвращает TRUE, если имеется хотя бы один NULL элемент в данных
        /// либо данных вообще нет
        /// Используется для проверки "отзыва данных".
        /// По этому сигналу производится переключение состояния UIStateSwitcher
        /// (показать/скрыть UI, включить/выключить элемент).
        /// </summary>
        private bool IsEmpty()
        {
            if (Data == null || Data.Count == 0)
                return true;
            foreach (var o in Data)
                if (o == null)
                    return true;

            return false;
        }
    }
}
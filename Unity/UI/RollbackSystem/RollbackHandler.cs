using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.Unity.UI.RollbackSystem
{
    /// <summary>
    /// Хэндлер отката экрана: объединяет источники (<see cref="RollbackSource"/>), показывает признак
    /// несохранённых изменений и ведёт два пути — <see cref="Save"/> и <see cref="Rollback"/>. Выключение
    /// экрана — это «Откатить».
    ///
    /// Работа реактивная: источники сообщают об изменениях сами, покадровых проверок нет. Точка отката
    /// фиксируется в конце кадра включения — к этому моменту контролы успевают получить свои значения.
    ///
    /// Запрос на выход — твинер: при включении мгновенно свёрнут, <see cref="CallExit"/> при изменениях
    /// разворачивает его. Сворачивание запроса и закрытие окна после выбора игрока — забота вёрстки.
    /// </summary>
    public class RollbackHandler : MonoBehaviour
    {
        [SerializeReference, HideReferenceObjectPicker]
        private RollbackSource[] sources = new RollbackSource[0];

        [SerializeField, StateSwitcher(typeof(SwitcherState)),
         Tooltip("Off — изменений нет, On — есть несохранённые изменения.")]
        private UIStateSwitcher switcher;

        [SerializeField, Tooltip("Запрос на выход: разворачивается CallExit при наличии изменений.")]
        private TweenerHub exitRequest;

        [SerializeField, Tooltip("Выход без изменений: CallExit, когда откатывать нечего.")]
        private UnityEvent onExit;

        /// <summary>Есть несохранённые изменения хотя бы в одном источнике.</summary>
        public bool HasChanges { get; private set; }

        private void OnEnable()
        {
            exitRequest.Back(true);
            foreach (var source in sources)
                source?.Init(Refresh);
            HasChanges = false;
            switcher.Set(SwitcherState.Off);
            TimeController.Call(Capture, this);
        }

        private void OnDisable()
        {
            TimeController.RemoveCall(this);
            if (HasChanges)
                Rollback();
            foreach (var source in sources)
                source?.DeInit();
        }

        /// <summary>Сохранить: текущее состояние становится точкой отката.</summary>
        public void Save() => Capture();

        /// <summary>Откатить к точке отката; состояние после отката становится новой точкой.</summary>
        public void Rollback()
        {
            foreach (var source in sources)
            {
                if (source == null)
                    continue;
                try
                {
                    source.Rollback();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RollbackHandler] Откат источника {source.GetType().Name} не удался.", this);
                    Debug.LogException(e);
                }
            }

            Capture();
        }

        /// <summary>Первый источник указанного типа. <c>null</c> — такого в списке нет.</summary>
        public T GetSource<T>() where T : RollbackSource
        {
            foreach (var source in sources)
                if (source is T typed)
                    return typed;
            return null;
        }

        /// <summary>Выход: при изменениях — развернуть запрос, иначе — <see cref="onExit"/>.</summary>
        public void CallExit()
        {
            if (HasChanges)
                exitRequest.Forward();
            else
                onExit?.Invoke();
        }

        private void Capture()
        {
            foreach (var source in sources)
                source?.Capture();
            Refresh();
        }

        private void Refresh()
        {
            var any = false;
            foreach (var source in sources)
                if (source is { HasChanges: true })
                {
                    any = true;
                    break;
                }

            HasChanges = any;
            switcher.Set(any ? SwitcherState.On : SwitcherState.Off);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (sources == null || sources.Length == 0)
            {
                Debug.LogWarning("[RollbackHandler] Список источников пуст — откатывать нечего.", this);
                return;
            }

            foreach (var source in sources)
                if (source == null)
                    Debug.LogError("[RollbackHandler] Пустой элемент в списке источников.", this);
        }
#endif
    }
}
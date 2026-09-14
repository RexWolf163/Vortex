using System;
using Sirenix.OdinInspector;

namespace Vortex.Unity.UI.RollbackSystem
{
    /// <summary>
    /// Источник отката — одна область, которую <see cref="RollbackHandler"/> умеет откатить: запоминает точку
    /// отката, следит за своими данными по событиям и возвращается к точке.
    ///
    /// Признак изменений кэшируется и пересчитывается только по событиям источника (<see cref="Refresh"/>);
    /// хэндлер уведомляется, лишь когда признак сменился. Сравнение точное: «поменял и вернул» — изменений нет.
    /// До фиксации точки изменений нет, откат пустой.
    /// </summary>
    [Serializable]
    public abstract class RollbackSource
    {
        private Action _onChanged;
        private bool _captured;

        /// <summary>Состояние отличается от точки отката.</summary>
        public bool HasChanges { get; private set; }

        /// <summary>Хэндлер включён, источник подписан на свои события.</summary>
        protected bool IsInitialized { get; private set; }

#if UNITY_EDITOR
        /// <summary>
        /// Строка с типом источника в списке хэндлера: у источников без сериализуемых полей элемент списка иначе
        /// пуст.
        /// </summary>
        [ShowInInspector, DisplayAsString, HideLabel, PropertyOrder(-1)]
        private string SourceType => GetType().Name;
#endif

        internal void Init(Action onChanged)
        {
            _onChanged = onChanged;
            _captured = false;
            HasChanges = false;
            IsInitialized = true;
            Subscribe();
        }

        internal void DeInit()
        {
            Unsubscribe();
            IsInitialized = false;
            _onChanged = null;
            _captured = false;
            HasChanges = false;
        }

        internal void Capture()
        {
            MakeCheckpoint();
            _captured = true;
            SetChanged(false);
        }

        internal void Rollback()
        {
            if (_captured)
                Restore();
        }

        /// <summary>Пересчитать признак изменений. Реализация вызывает из обработчиков своих событий.</summary>
        protected void Refresh() => SetChanged(_captured && DiffersFromCheckpoint());

        /// <summary>Подписаться на события своих данных; в обработчиках — <see cref="Refresh"/>.</summary>
        protected abstract void Subscribe();

        protected abstract void Unsubscribe();

        /// <summary>Запомнить текущее состояние как точку отката.</summary>
        protected abstract void MakeCheckpoint();

        /// <summary>Текущее состояние отличается от точки отката.</summary>
        protected abstract bool DiffersFromCheckpoint();

        /// <summary>Вернуть состояние точки отката.</summary>
        protected abstract void Restore();

        private void SetChanged(bool value)
        {
            if (HasChanges == value)
                return;
            HasChanges = value;
            _onChanged?.Invoke();
        }
    }
}
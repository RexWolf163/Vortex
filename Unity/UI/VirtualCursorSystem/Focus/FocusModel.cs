using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Runtime-реестр фокус-навигации виртуального курсора. Хранит LIFO-стек активных
    /// <see cref="FocusGroup"/> (топ — последняя push'нутая через <c>OnEnable</c>) и
    /// текущую активную цель фокуса (глобально, не per-group). Реактивно; НЕ сохраняется.
    /// Владение реактивным полем закреплено за <c>VirtualCursorFocusController</c> через
    /// <see cref="SetOwner"/> — извне <c>Set</c> не проходит.
    ///
    /// Target'ы не хранятся плоским списком — они живут внутри своих <see cref="FocusGroup"/>
    /// (регистрируются <c>FocusTargetComponent</c>). <see cref="ActiveGroup"/> определяет,
    /// среди каких target'ов идёт <c>Navigate</c>.
    ///
    /// Внешним потребителям — read-only: подписываться на <c>CurrentFocus.OnUpdate</c>
    /// (смена фокуса — включая переходы между группами и обнуление), читать
    /// <see cref="Groups"/>/<see cref="ActiveGroup"/>. Мутации — internal, зовутся
    /// контроллером/<see cref="FocusGroup"/>.
    /// </summary>
    public class FocusModel : IReactiveData
    {
        public event Action OnUpdateData;

        private readonly List<FocusGroup> _groups = new();

        /// <summary>Снапшот LIFO-стека групп (read-only). Топ = <c>Groups[^1]</c>.</summary>
        public IReadOnlyList<FocusGroup> Groups => _groups;

        /// <summary>Активная группа — верхняя в LIFO-стеке. <c>null</c>, если стек пуст.</summary>
        public FocusGroup ActiveGroup => _groups.Count > 0 ? _groups[^1] : null;

        /// <summary>Текущая цель фокуса или <c>null</c>, если фокуса нет / стек пуст.</summary>
        public FocusTargetData CurrentFocus { get; } = new(null);

        public FocusModel()
        {
            CurrentFocus.OnUpdateData += Raise;
        }

        private void Raise() => OnUpdateData?.Invoke();

        /// <summary>Закрепить владельца за реактивным полем. Зовётся контроллером один раз.</summary>
        public void SetOwner(object owner)
        {
            CurrentFocus.SetOwner(owner);
        }

        /// <summary>Push группы в LIFO-стек. Idempotent: повторное добавление без эффекта.</summary>
        internal void PushGroup(FocusGroup group)
        {
            if (group == null || _groups.Contains(group)) return;
            _groups.Add(group);
            Raise();
        }

        /// <summary>Снять группу из LIFO. Возвращает true, если она была активной (топ).</summary>
        internal bool RemoveGroup(FocusGroup group)
        {
            if (group == null) return false;
            var wasActive = _groups.Count > 0 && ReferenceEquals(_groups[^1], group);
            if (!_groups.Remove(group)) return false;
            Raise();
            return wasActive;
        }

        /// <summary>Прямая установка текущего фокуса. Контроллер зовёт на SetFocusInternal / ClearFocus.</summary>
        internal void SetCurrent(IFocusTarget target, object owner)
        {
            CurrentFocus.Set(target, owner);
        }
    }
}

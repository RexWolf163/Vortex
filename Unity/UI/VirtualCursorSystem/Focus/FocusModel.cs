using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Unity.AppSystem.System.TimeSystem;

namespace Vortex.Unity.UI.VirtualCursorSystem.Focus
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
    ///
    /// <b>Батчинг <see cref="OnUpdateData"/>.</b> Смена групп порождает серию изменений за один вызов
    /// (стек → снятие фокуса → новая цель); агрегатное событие собирает их в одно — не чаще раза за волну
    /// <see cref="TimeController.Accumulate"/> (LateUpdate) и уже с итоговым состоянием.
    /// <c>CurrentFocus.OnUpdate</c> остаётся синхронным — на каждое изменение фокуса.
    /// </summary>
    public class FocusModel : IReactiveData
    {
        /// <summary>
        /// Любое изменение модели: смена фокуса или состава стека групп. Батчится: одно событие на волну
        /// <see cref="TimeController.Accumulate"/> (LateUpdate), сколько бы изменений ни случилось до неё.
        /// </summary>
        public event Action OnUpdateData;

        /// <summary>
        /// Есть изменения, о которых подписчики <see cref="OnUpdateData"/> ещё не уведомлены. Снимается в
        /// <see cref="Flush"/>; повторный <see cref="Flush"/> без новых изменений ничего не поднимает.
        /// </summary>
        private bool _dirty;

        /// <summary>Стек групп; верх стека — последний элемент.</summary>
        private readonly List<FocusGroup> _groups = new();

        /// <summary>Снапшот стека групп (read-only). Топ = <c>Groups[^1]</c>; порядок — LIFO
        /// внутри тиров приоритета, высокий приоритет всегда ближе к топу.</summary>
        public IReadOnlyList<FocusGroup> Groups => _groups;

        /// <summary>
        /// Активная группа — ближайшая к топу стека, у которой <see cref="FocusGroup.Ignore"/>=false.
        /// Проверка идёт сверху вниз (<c>[^1]</c>→<c>[0]</c>) — Ignore-группы невидимы для Navigate,
        /// но остаются в стеке. <c>null</c>, если стек пуст или все группы Ignored.
        /// </summary>
        public FocusGroup ActiveGroup
        {
            get
            {
                for (var i = _groups.Count - 1; i >= 0; i--)
                {
                    var g = _groups[i];
                    if (g != null && !g.Ignore) return g;
                }

                return null;
            }
        }

        /// <summary>Текущая цель фокуса или <c>null</c>, если фокуса нет / стек пуст.</summary>
        public FocusTargetData CurrentFocus { get; } = new(null);

        /// <summary>Модель с пустым стеком и без фокуса; смена фокуса транслируется в <see cref="OnUpdateData"/>.</summary>
        public FocusModel()
        {
            CurrentFocus.OnUpdateData += Raise;
        }

        /// <summary>Есть ли текущий фокус. Наличие фокуса — признак режима навигации.</summary>
        public bool HasFocus() => CurrentFocus.Value != null;

        /// <summary>Отметить изменение и запланировать <see cref="Flush"/> на волну <see cref="TimeController.Accumulate"/>.</summary>
        private void Raise()
        {
            _dirty = true;
            TimeController.Accumulate(Flush, this);
        }

        /// <summary>
        /// Поднять <see cref="OnUpdateData"/> один раз за накопленные изменения. Изменения, внесённые во время
        /// волны после планирования (например, отложенная авто-передача фокуса), уходят в следующую волну;
        /// если они успели до Flush этой — следующий Flush пустой.
        /// </summary>
        private void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            OnUpdateData?.Invoke();
        }

        /// <summary>Закрепить владельца за реактивным полем. Зовётся контроллером один раз.</summary>
        public void SetOwner(object owner)
        {
            CurrentFocus.SetOwner(owner);
        }

        /// <summary>
        /// Push группы в стек с учётом приоритета. Idempotent: повторное добавление без эффекта.
        /// Алгоритм — пузырёк: группа добавляется к топу и бабблится вниз, пока предыдущий элемент
        /// имеет СТРОГО больший приоритет (чтобы быть «выше» неё). Между равными приоритетами —
        /// обычный LIFO (позже push'нутый — выше). При <c>priority=0</c> у всех — поведение
        /// эквивалентно простому <c>Add</c>.
        /// </summary>
        internal void PushGroup(FocusGroup group)
        {
            if (group == null || _groups.Contains(group)) return;

            // Вставка позицией «тир приоритета + конец LIFO внутри тира». Идём с конца; каждая
            // предыдущая группа с СТРОГО большим приоритетом остаётся выше нас (мы встаём под
            // неё). Равная — пропускаем новичка выше (LIFO внутри тира). Строго меньшая — тоже
            // уходит под нас (мы заходим в её тир сверху).
            var p = group.Priority;
            var pos = _groups.Count;
            while (pos > 0 && _groups[pos - 1].Priority > p)
                pos--;
            _groups.Insert(pos, group);
            Raise();
        }

        /// <summary>
        /// Снять группу из стека. Возвращает true, если она была активной на момент удаления
        /// (с учётом <see cref="FocusGroup.Ignore"/> у соседей — активной считается не
        /// обязательно верхняя, а первая не-Ignored сверху).
        /// </summary>
        internal bool RemoveGroup(FocusGroup group)
        {
            // Сравнение по ссылке: уничтоженная группа (fake-null) тоже должна сниматься из стека.
            if (ReferenceEquals(group, null)) return false;
            // Сравнение с ActiveGroup до удаления: сам List.Remove — O(n), один проход; снятие
            // попутно «вычищает» элемент из любых потенциальных приоритетных подочередей (у нас
            // единый список, никаких parallel-queue не ведём — чистка исчерпывается Remove'ом).
            var wasActive = ReferenceEquals(ActiveGroup, group);
            if (!_groups.Remove(group)) return false;
            Raise();
            return wasActive;
        }

        /// <summary>Прямая установка текущего фокуса (<c>null</c> — снять). Проходит только с ключом владельца — контроллера.</summary>
        internal void SetCurrent(IFocusTarget target, object owner)
        {
            CurrentFocus.Set(target, owner);
        }
    }
}
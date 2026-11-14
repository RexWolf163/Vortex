using System;
using UnityEngine;
using Vortex.Unity.CutsceneSystem.Bus;
using Vortex.Unity.CutsceneSystem.Models;
using Vortex.Unity.UI.Misc;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.CutsceneSystem.Handlers
{
    /// <summary>
    /// Хэндлер контента катсцены: переводит номер фазы в состояние свитчера. Стоит на префабе контента,
    /// управляющие данные берёт из <c>DataStorage</c> его корня.
    ///
    /// Состояния свитчера идут строго по порядку фаз: нулевое — пустое исходное (катсцена ещё не показана),
    /// дальше по одному на фазу. Состояний должно быть ровно «число фаз + 1».
    ///
    /// Фаза с автопереходом сообщает о своём конце вызовом <see cref="Next"/> — из состояния свитчера
    /// (<c>EventFire</c>) либо из профильного хэндлера
    /// </summary>
    public class CutsceneContentHandler : DataStorageView<CutsceneControl>
    {
        [SerializeField, Tooltip("Свитчер фаз: пустое исходное состояние и по состоянию на каждую фазу.")]
        private UIStateSwitcher switcher;

        /// <summary>
        /// Фаза, состояние которой сейчас выставлено. Её называет сигнал автоперехода
        /// </summary>
        private int _shownPhase;

        protected override void OnDataUpdated() => ShowPhase(Data.Phase.Value);

        /// <summary>
        /// Показать фазу. По умолчанию — сразу; наследник может отложить переключение
        /// </summary>
        protected virtual void ShowPhase(int phase) => SetState(phase);

        /// <summary>
        /// Перевести свитчер в состояние фазы
        /// </summary>
        protected void SetState(int phase)
        {
            var states = switcher.States.Length;
            if (phase < states)
            {
                //До переключения: источник автоперехода может сработать прямо в момент входа в состояние
                _shownPhase = phase;
                switcher.Set(phase);
                return;
            }

            var message =
                $"[CutsceneContentHandler] '{name}': фаза {phase}, а состояний у свитчера {states} — их должно быть «число фаз + 1».";
#if UNITY_EDITOR
            //В редакторе рассогласование цепочки и контента — остановка, а не тихий пропуск
            throw new InvalidOperationException(message);
#else
            Debug.LogError(message, this);
#endif
        }

        /// <summary>
        /// Автопереход: сообщить шине, что показанная фаза доиграла. Защиту от дребезга обходит. Если цепочка
        /// с этой фазы уже ушла (игрок успел нажать «дальше», контент догоняет цепочку), шина сигнал отбросит.
        ///
        /// Выключенный контент сигнала не подаёт: его показ закрыт, а отложенный вызов (твин, таймер) иначе
        /// попал бы в следующую катсцену, стоящую на фазе с тем же номером
        /// </summary>
        public void Next()
        {
            if (this == null || !isActiveAndEnabled)
                return;

            CutsceneBus.Next(_shownPhase);
        }
    }
}

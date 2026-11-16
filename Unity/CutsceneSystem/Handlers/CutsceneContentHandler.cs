using UnityEngine;
using Vortex.Unity.CutsceneSystem.Bus;
using Vortex.Unity.StateGraphSystem.Views;

namespace Vortex.Unity.CutsceneSystem.Handlers
{
    /// <summary>
    /// Хэндлер контента катсцены: источник автоперехода. Стоит на корне префаба контента рядом со
    /// <see cref="StateGraphView"/>, который показывает граф катсцены.
    ///
    /// Узел с автопереходом сообщает о своём конце вызовом <see cref="Next"/> — из состояния свитчера
    /// (<c>EventFire</c>) либо из профильного хэндлера (для Spine — <c>SpineCutsceneAutoNext</c>). В графе у
    /// такого узла переход «по Next»
    /// </summary>
    public class CutsceneContentHandler : MonoBehaviour
    {
        [SerializeField, Tooltip("Показ графа катсцены. Не задан — берётся с этого же объекта.")]
        private StateGraphView view;

        private void Awake()
        {
            if (view == null)
                view = GetComponent<StateGraphView>();
        }

        /// <summary>
        /// Автопереход: сообщить шине, что показанный узел доиграл. Защиту от дребезга обходит. Если показ
        /// с этого узла уже ушёл (игрок успел нажать «дальше», контент догоняет), шина сигнал отбросит.
        ///
        /// Выключенный контент сигнала не подаёт: его показ закрыт, а отложенный вызов (твин, таймер) иначе
        /// попал бы в следующую катсцену
        /// </summary>
        public void Next()
        {
            if (this == null || !isActiveAndEnabled || view == null)
                return;

            var node = view.ShownNodeId;
            if (node != null)
                CutsceneBus.Next(node);
        }
    }
}

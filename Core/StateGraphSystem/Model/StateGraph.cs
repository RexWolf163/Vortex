using System;
using System.Collections.Generic;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.StateGraphSystem.Controllers;

namespace Vortex.Core.StateGraphSystem.Model
{
    /// <summary>
    /// Автомат состояний: структура графа (узлы и переходы) и текущее состояние одного экземпляра.
    /// Запись MultiInstance — каждый потребитель работает со своей копией из БД.
    /// Логики в модели нет: всё меняет <see cref="StateGraphController"/>.
    /// </summary>
    public class StateGraph : Record
    {
        /// <summary>Узлы графа</summary>
        public StateNode[] Nodes { get; internal set; } = new StateNode[0];

        /// <summary>Id стартового узла</summary>
        public string StartNode { get; internal set; }

        /// <summary>
        /// Id текущего узла. Пусто — автомат не запущен или завершён.
        /// Закрыт на контроллер (owner-lock)
        /// </summary>
        public StringData CurrentNode { get; } = new(string.Empty, StateGraphController.OwnerKey);

        /// <summary>Идентификатор экземпляра — владелец, которого получают условия переходов</summary>
        public string InstanceId { get; } = Crypto.GetNewGuid();

        /// <summary>Экземпляр активен (потребитель включён) и принимает сигналы</summary>
        public bool IsActive { get; internal set; }

        /// <summary>
        /// Автомат завершён: вход в конечный узел или принудительная остановка.
        /// Естественное и принудительное завершение не различаются
        /// </summary>
        public event Action OnFinished;

        #region Рабочее состояние контроллера

        /// <summary>Текущий узел исполнения (null — не запущен / завершён)</summary>
        internal StateNode Node { get; set; }

        /// <summary>Условия текущего узла запущены</summary>
        internal bool IsEntered { get; set; }

        /// <summary>Идёт запуск условий узла — сигналы условий в это время не обрабатываются</summary>
        internal bool IsEntering { get; set; }

        /// <summary>Номер входа в узел: отсекает запоздалые сигналы условий прошлых входов</summary>
        internal int EntryVersion { get; set; }

        /// <summary>В неактивном состоянии экземпляр сохраняет состояние (режим Resume)</summary>
        internal bool RetainWhileInactive { get; set; }

        /// <summary>Индекс узлов по Id; строится после успешной проверки графа</summary>
        internal Dictionary<string, StateNode> Index { get; set; }

        /// <summary>Проверка графа провалена — экземпляр не запускается</summary>
        internal bool IsInvalid { get; set; }

        internal void CallOnFinished() => OnFinished?.Invoke();

        #endregion

        public override string GetDataForSave() => null;

        public override void LoadFromSaveData(string data)
        {
        }
    }
}

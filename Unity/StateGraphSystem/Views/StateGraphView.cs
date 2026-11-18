using System;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.StateGraphSystem.Controllers;
using Vortex.Core.StateGraphSystem.Model;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.StateGraphSystem.Views
{
    /// <summary>Поведение View при включении</summary>
    public enum OnEnableBehavior
    {
        /// <summary>Каждое включение — запуск со стартового узла; при выключении условия останавливаются</summary>
        Restart,

        /// <summary>Включение продолжает с того места, где остановились; при выключении условия работают</summary>
        Resume
    }

    /// <summary>
    /// Показ автомата состояний: на первом включении берёт свою копию графа из БД (дождавшись её загрузки)
    /// и держит её до уничтожения. Текущий узел показывается состоянием свитчера с тем же именем.
    /// Не запущенный автомат показывается стартовым узлом, завершённый — последним показанным.
    /// Модель отдаётся потребителям через <see cref="IDataStorage"/>: управление (Next / ForceNode / Stop) —
    /// их забота, View только включает/выключает экземпляр и показывает его
    /// </summary>
    [HideMonoScript]
    public partial class StateGraphView : MonoBehaviour, IDataStorage
    {
        [SerializeField, Required, DbRecord(typeof(StateGraph), RecordTypes.MultiInstance)]
        private string graph;

        [SerializeField] private OnEnableBehavior onEnableBehavior = OnEnableBehavior.Restart;

        [SerializeField, Required] private UIStateSwitcher switcher;

        [SerializeField, Min(0f), Tooltip(
             "Минимальное время между переключениями состояний, секунды (без учёта Time.timeScale). " +
             "Узлы, пришедшие во время ожидания, не копятся: по его окончании показывается последний. " +
             "Нужно, когда смена состояния — переход на твинах. 0 — без задержки.")]
        private float switchDelay;

        private StateGraph _model;

        /// <summary>Узел, ждущий показа по истечении <see cref="switchDelay"/> (null — не ждёт)</summary>
        private string _pendingNode;

        /// <summary>Время, раньше которого следующее состояние не выставляется</summary>
        private float _nextSwitchAt;

        /// <summary>Ждём загрузки БД, чтобы взять копию графа</summary>
        private bool _waitingDatabase;

        /// <summary>Id узла, показанного последним (null — ещё ничего не показано)</summary>
        private string _shownNode;

        /// <summary>Модель появилась (копия взята из БД)</summary>
        public event Action OnUpdateLink;

        public T GetData<T>() where T : class => _model as T;

        /// <summary>
        /// Id узла, состояние которого показано сейчас (null — ничего не показано). С задержкой переключения
        /// может отставать от текущего узла автомата
        /// </summary>
        public string ShownNodeId => _shownNode;

        private void OnEnable()
        {
            if (_model != null)
            {
                Bind();
                return;
            }

            if (_waitingDatabase)
                return;
            _waitingDatabase = true;
            // Если БД уже загружена, обработчик вызывается сразу
            Database.OnInit += OnDatabaseReady;
        }

        private void OnDisable()
        {
            if (_model == null)
                return;

            _model.CurrentNode.OnUpdate -= OnCurrentNode;
            _model.Deactivate(onEnableBehavior == OnEnableBehavior.Resume);
            _pendingNode = null;
        }

        private void Update()
        {
            if (_pendingNode != null && Time.unscaledTime >= _nextSwitchAt)
                Show(_pendingNode);
        }

        private void OnDestroy()
        {
            if (_waitingDatabase)
                Database.OnInit -= OnDatabaseReady;
            // В Resume условия работают и у выключенного View — гасим их окончательно
            _model?.Deactivate(false);
        }

        private void OnDatabaseReady()
        {
            _waitingDatabase = false;
            _model = Database.GetNewRecord<StateGraph>(graph);
            if (_model == null)
                return;

            OnUpdateLink?.Invoke();
            if (isActiveAndEnabled)
                Bind();
        }

        private void Bind()
        {
            // Первое переключение после включения — сразу: переходов в этом включении ещё не было
            _nextSwitchAt = float.NegativeInfinity;
            _model.CurrentNode.OnUpdate += OnCurrentNode;
            _model.Activate();
            // Текущий узел мог не смениться (повторный запуск того же узла, узел ForceNode из выключенного
            // состояния) — тогда события не будет, показываем явно
            Refresh();
        }

        private void OnCurrentNode(string nodeId) => Refresh();

        private void Refresh()
        {
            var nodeId = _model.CurrentNode.Value;
            if (string.IsNullOrEmpty(nodeId))
            {
                // Завершён — остаётся последний показанный; ещё не запущен — стартовый узел
                if (_shownNode != null)
                    return;
                nodeId = _model.StartNode;
            }

            Show(nodeId);
        }

        private void Show(string nodeId)
        {
            if (switchDelay > 0f && Time.unscaledTime < _nextSwitchAt)
            {
                _pendingNode = nodeId;
                return;
            }

            _pendingNode = null;
            var node = _model.GetNode(nodeId);
            if (node == null)
                return; // граф с ошибками: проверка графа уже вывела их в лог

            var state = switcher.GetState(node.Name);
            if (state < 0)
            {
                Debug.LogError($"[StateGraphView] Switcher has no state «{node.Name}» for graph «{_model.Name}».",
                    this);
                return;
            }

            // До переключения: источник сигнала из состояния может сработать прямо в момент входа в него
            _shownNode = nodeId;
            _nextSwitchAt = Time.unscaledTime + switchDelay;
            switcher.Set(state);
        }
    }
}

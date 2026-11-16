namespace Vortex.Core.StateGraphSystem.Validation
{
    /// <summary>Тяжесть замечания проверки графа</summary>
    public enum IssueSeverity
    {
        /// <summary>Граф работоспособен, но, вероятно, настроен не так, как задумано</summary>
        Warning,

        /// <summary>Граф неработоспособен — экземпляр не запустится</summary>
        Error
    }

    /// <summary>Вид замечания проверки графа</summary>
    public enum IssueKind
    {
        /// <summary>Стартовый узел не задан или не найден</summary>
        NoStartNode,

        /// <summary>Пустой или повторяющийся Id узла</summary>
        InvalidId,

        /// <summary>Пустое имя узла (пустое значение зарезервировано как нейтральное — «узла нет»)</summary>
        EmptyName,

        /// <summary>Повторяющееся имя узла</summary>
        DuplicateName,

        /// <summary>Переход ведёт на несуществующий узел</summary>
        DanglingTarget,

        /// <summary>Цикл из автопереходов</summary>
        AutoCycle,

        /// <summary>Узел недостижим из стартового</summary>
        UnreachableNode,

        /// <summary>Переход стоит после безусловного и никогда не будет выбран</summary>
        ShadowedTransition
    }

    /// <summary>
    /// Замечание проверки графа. Привязано к узлу (<see cref="NodeId"/>) и, если относится к переходу,
    /// к его номеру в узле (<see cref="TransitionIndex"/>, иначе -1)
    /// </summary>
    public class StateGraphIssue
    {
        public IssueSeverity Severity { get; }
        public IssueKind Kind { get; }

        /// <summary>Id узла, к которому относится замечание (null — к графу в целом)</summary>
        public string NodeId { get; }

        /// <summary>Номер перехода в узле или -1</summary>
        public int TransitionIndex { get; }

        public string Message { get; }

        public StateGraphIssue(IssueSeverity severity, IssueKind kind, string nodeId, int transitionIndex,
            string message)
        {
            Severity = severity;
            Kind = kind;
            NodeId = nodeId;
            TransitionIndex = transitionIndex;
            Message = message;
        }

        public override string ToString() => $"{Severity}: {Message}";
    }
}

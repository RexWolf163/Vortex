namespace Vortex.NaniExtensions.CustomVariablesDashboard
{
    /// <summary>
    /// Вид операции над переменной Naninovel — общий словарь панели переменных для сценариев и квестов.
    /// </summary>
    public enum NaniVariableOperation
    {
        /// <summary>Переменной присваивается значение</summary>
        Write,

        /// <summary>Переменная сравнивается в условии: есть оператор и операнд</summary>
        Compare,

        /// <summary>Ожидание, пока сравнение станет истинным</summary>
        Wait,

        /// <summary>Переменная участвует в выражении без сравнения</summary>
        Read,

        /// <summary>Употребление не распознано</summary>
        Other
    }

    /// <summary>
    /// Контракт «ссылка на переменную Naninovel»: объект квеста (условие, логика) сообщает панели переменных
    /// (<c>Tools/Vortex/Debug/Custom Variables Dashboard</c>), с какой переменной и как он работает. Поведение объекта
    /// в игре контракт не меняет; место в квесте панель определяет обходом пресета сама.
    /// </summary>
    public interface INaniVariableReference
    {
        /// <summary>Имя переменной (с учётом регистра, как в Naninovel)</summary>
        string VariableName { get; }

        /// <summary>Вид операции</summary>
        NaniVariableOperation Operation { get; }

        /// <summary>Операнд как есть, например <c>&gt;=3</c> или <c>true</c>; может быть пустым</summary>
        string Operand { get; }
    }
}

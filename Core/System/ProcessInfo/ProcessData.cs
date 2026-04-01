namespace Vortex.Core.System.ProcessInfo
{
    /// <summary>
    /// Класс индицируемых данных какого-то процесса
    ///
    /// В целях оптимизации обращения нет инкапсуляции полей.
    /// Контроль за адекватностью места внесения изменений лежит на программисте
    /// </summary>
    public class ProcessData
    {
        public string Name;
        public int Progress;
        public int Size;

        public ProcessData()
        {
        }

        public ProcessData(string name)
        {
            Name = name;
        }
    }
}
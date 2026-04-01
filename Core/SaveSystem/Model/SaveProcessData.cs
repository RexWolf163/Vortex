using Vortex.Core.System.ProcessInfo;

namespace Vortex.Core.SaveSystem.Model
{
    /// <summary>
    /// Класс полных данных по процессам загрузки или сохранения
    /// </summary>
    public class SaveProcessData
    {
        public SaveProcessData(ProcessData global)
        {
            Global = global;
        }

        public ProcessData Global { get; }
        public ProcessData Module { get; internal set; }
    }
}
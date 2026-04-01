namespace Vortex.Core.SaveSystem.Abstraction
{
    /// <summary>
    /// Структура сохраняемых данных
    /// </summary>
    public struct SaveFolder
    {
        public string Id;
        public SaveData[] DataSet;
    }
}
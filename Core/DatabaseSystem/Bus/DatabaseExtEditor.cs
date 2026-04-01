namespace Vortex.Core.DatabaseSystem.Bus
{
    public partial class Database
    {
        /// <summary>
        /// Возвращает активный драйвер базы данных
        /// </summary>
        /// <returns></returns>
        public static IDriver GetDriver() => Driver;
    }
}
namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        /// <summary>Подробные логи сетевого модуля (запуск, попытки, итоги, тела ответов).</summary>
        public bool NetworkDebugMode { get; private set; }
    }
}

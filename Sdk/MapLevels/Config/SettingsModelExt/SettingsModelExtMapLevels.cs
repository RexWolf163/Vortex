namespace Vortex.Core.SettingsSystem.Model
{
    /// <summary>
    /// Расширение SettingsModel полями пакета MapLevels.
    /// Заполняется CopyFrom из MapLevelsSettings ScriptableObject.
    /// </summary>
    public partial class SettingsModel
    {
        /// <summary>
        /// Глубина выгрузки уровней (hop >= MapLevelsUnloadDistance → выгрузка).
        /// </summary>
        public int MapLevelsUnloadDistance { get; private set; }

        /// <summary>
        /// AssemblyQualifiedName реализации IMapLevelsController, выбранной в настройках.
        /// </summary>
        public string MapLevelsControllerTypeName { get; private set; }
    }
}

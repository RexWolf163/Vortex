namespace Vortex.Unity.EditorTools.Abstraction
{
    /// <summary>
    /// Маркерный интерфейс. MonoBehaviour, реализующий этот интерфейс,
    /// будет обработан CollectionEditor при выключенном глобальном режиме
    /// (ToolsSettings.GlobalCollectionRendering == false).
    /// </summary>
    public interface IUseVortexCollectionRendering
    {
    }
}

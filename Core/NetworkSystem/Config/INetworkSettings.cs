namespace Vortex.Core.NetworkSystem.Config
{
    /// <summary>
    /// Все настройки связи: адреса, значения по умолчанию, ограничения и защита. Данные без логики:
    /// выбор и проверку выполняет контроллер.
    /// </summary>
    public interface INetworkSettings : IEndpointSettings, IRequestDefaults, ILimitSettings, ISecuritySettings
    {
    }
}

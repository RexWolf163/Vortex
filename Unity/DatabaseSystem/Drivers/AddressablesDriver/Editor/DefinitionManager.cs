#if UNITY_EDITOR
using Vortex.Unity.Extensions.Editor;

namespace Vortex.Unity.DatabaseSystem.Drivers.AddressablesDriver.Editor
{
    /// <summary>
    /// Выставление ключа для экранирования
    /// </summary>
    public class DefinitionManager : INeedPackage
    {
        public string GetPackageName() => "com.unity.addressables";

        public string GetDefineString() => "ENABLE_ADDRESSABLES";
    }
}
#endif
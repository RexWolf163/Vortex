#if UNITY_EDITOR
using UnityEditor;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Unity.LoggerSystem
{
    [InitializeOnLoad]
    public partial class LogDriver
    {
        static LogDriver()
        {
            if (!Log.SetDriver(new LogDriver()))
                return;

            Log.Print(new LogData(LogLevel.Warning, "Log driver connected", "LogDriver"));
        }
    }
}
#endif
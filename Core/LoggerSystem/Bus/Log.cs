using System;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.LoggerSystem.Bus
{
    public class Log : SystemController<Log, IDriver>
    {
        public static void Print(LogData log)
        {
            Driver.Print(log);
        }

        public static void Print(LogLevel level, string message, Object source)
        {
            Driver.Print(new LogData(level, message, source));
        }

        protected override void OnDriverConnect()
        {
            //Ignore
        }

        protected override void OnDriverDisconnect()
        {
            //Ignore
        }
    }
}
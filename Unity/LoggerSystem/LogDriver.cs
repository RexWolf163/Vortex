using System;
using UnityEngine;
using Vortex.Core.LoggerSystem;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Unity.LoggerSystem
{
    /// <summary>
    /// Драйвер вывода данных для Unity
    /// </summary>
    public partial class LogDriver : IDriver
    {
        public event Action OnInit;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            Log.SetDriver(new LogDriver());
        }

        /// <summary>
        /// Вывод сообщения в консоль
        /// </summary>
        /// <param name="log">структура с данными для вывода</param>
        public void Print(LogData log)
        {
            var source = log.Source as string ?? log.Source?.GetType().Name ?? "Unknown";
            switch (log.Level)
            {
                case LogLevel.Common:
                    Debug.Log($"[{source}] {log.Message}");
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning($"[{source}] {log.Message}");
                    break;
                case LogLevel.Error:
                    Debug.LogError($"[{source}] {log.Message}");
                    break;
            }
        }

        public void Init()
        {
            OnInit?.Invoke();
        }

        public void Destroy()
        {
            //Ignore
        }
    }
}
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Unity.SettingsSystem
{
    public partial class SettingsDriver
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            if (!Settings.SetDriver(Instance))
            {
                if (Settings.HasDriver() && Application.isPlaying)
                    Debug.LogWarning(
                        "[SettingsDriver] не удалось задать драйвер для сервиса Settings. Драйвер уже установлен");
                if (!Settings.HasDriver() && Application.isPlaying)
                    Debug.LogWarning("[SettingsDriver] не удалось задать драйвер для сервиса Settings");
            }
            else
                Dispose();
        }
    }
}
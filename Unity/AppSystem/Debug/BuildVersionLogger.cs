using UnityEngine;

namespace Vortex.Unity.AppSystem.DebugSystem
{
    /// <summary>
    /// Пишет версию билда в лог на запуске. Нужен, чтобы по присланному Player.log сразу
    /// было видно, на какой сборке воспроизведён баг: строка встаёт в самом начале лога, до инициализации
    /// систем.
    /// </summary>
    public static class BuildVersionLogger
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Bootstrap()
        {
            // buildGUID пуст в редакторе и уникален на каждую сборку — по нему различаются билды с
            // одинаковым Application.version (перевыпуск без бампа версии).
            var buildId = string.IsNullOrEmpty(Application.buildGUID) ? "editor" : Application.buildGUID;
            Debug.Log($"[BuildVersion] {Application.version} | build {buildId} | " +
                      $"{Application.platform} | unity {Application.unityVersion}");
        }
    }
}
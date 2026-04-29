using System.IO;
using UnityEngine;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        /// <summary>
        /// Установить превью, которое будет записано первой строкой следующего Save.
        /// One-shot: сбрасывается в null после Save.
        /// Driver-specific API — НЕ часть IDriver.
        /// </summary>
        /// <param name="base64">Base64-строка превью или null/empty чтобы очистить.</param>
        public void SetPendingPreview(string base64)
        {
            _pendingPreview = base64;
        }

        /// <summary>
        /// Прочитать превью указанного сейва.
        /// Возвращает пустую строку если превью отсутствует или сейва нет.
        /// Driver-specific API — НЕ часть IDriver.
        /// </summary>
        public string GetPreview(string guid)
        {
            var path = GetSaveFilePath(guid);
            if (!File.Exists(path))
                return string.Empty;

            try
            {
                using var sr = new StreamReader(path);
                var firstLine = sr.ReadLine();
                return firstLine ?? string.Empty;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FileSystemDriver] Ошибка чтения превью сейва {guid}: {e.Message}");
                return string.Empty;
            }
        }
    }
}

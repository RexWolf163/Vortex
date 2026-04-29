using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        /// <summary>
        /// Возвращает все существующие сейвы из in-memory реестра.
        /// </summary>
        public Dictionary<string, SaveSummary> GetIndex() =>
            Saves.ToDictionary(s => s.Key, s => s.Value);

        /// <summary>
        /// Возвращает номер-инкремент последнего сейва.
        /// Считается как количество summary-файлов в папке Saves/.
        /// </summary>
        public int GetNumberLastSave()
        {
            if (_increment >= 0)
                return _increment;

            var dir = GetSavesDirectory();
            _increment = !Directory.Exists(dir)
                ? 0
                : Directory.GetFiles(dir, $"*{SummaryExtension}").Length;
            return _increment;
        }

        /// <summary>
        /// Сканирует папку Saves/, читает все *.summary файлы, заполняет реестр.
        /// </summary>
        private void ScanIndex()
        {
            Saves.Clear();
            var dir = GetSavesDirectory();
            if (!Directory.Exists(dir))
                return;

            var files = Directory.GetFiles(dir, $"*{SummaryExtension}");
            foreach (var file in files)
            {
                var guid = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrEmpty(guid)) continue;

                try
                {
                    var xml = File.ReadAllText(file);
                    var summary = DeserializeSummary(xml);
                    Saves[guid] = summary;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[FileSystemDriver] Не удалось прочитать summary {file}: {e.Message}");
                }
            }
        }
    }
}

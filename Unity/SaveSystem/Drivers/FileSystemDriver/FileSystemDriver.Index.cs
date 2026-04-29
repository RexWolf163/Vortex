using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Unity.FileSystem.Bus;

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
            var path = GetIncrementFilePath();
            if (!File.Exists(path))
            {
                _increment = 0;
                FileBus.CreateFolders(dir);
                var file = File.CreateText(path);
                file.Write(0);
                file.Close();
                return _increment;
            }

            var str = File.ReadAllText(path);
            Int32.TryParse(str, out _increment);
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
                catch (Exception e)
                {
                    Debug.LogError($"[FileSystemDriver] Не удалось прочитать summary {file}: {e.Message}");
                }
            }
        }
    }
}
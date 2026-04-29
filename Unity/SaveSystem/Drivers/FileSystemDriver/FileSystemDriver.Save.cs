using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Unity.SaveSystem.Presets;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        /// <summary>
        /// Сохранить сейв по guid. Превью берётся из _pendingPreview (one-shot)
        /// и записывается первой строкой тела файла.
        /// </summary>
        public void Save(string name, string guid)
        {
            EnsureSavesDirectory();

            // Собираем SavePreset из переданного индекса данных.
            var preset = BuildSavePreset();
            var dataXml = SerializeSavePreset(preset);

            // Превью — обязательная первая строка тела сейва (может быть пустой).
            var preview = _pendingPreview ?? string.Empty;
            _pendingPreview = null; // one-shot

            try
            {
                var savePath = GetSaveFilePath(guid);
                File.WriteAllText(savePath, $"{preview}\n{dataXml}");

                var summary = new SaveSummary(name, DateTime.UtcNow.ToFileTimeUtc());
                var summaryXml = SerializeSummary(summary);
                File.WriteAllText(GetSummaryFilePath(guid), summaryXml);

                if (!Saves.ContainsKey(guid))
                    _increment = GetNumberLastSave() + 1;

                Saves[guid] = summary;
            }
            catch (Exception e)
            {
                Debug.LogError($"[FileSystemDriver] Ошибка записи сейва {guid}: {e.Message}");
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Собирает SavePreset из текущего _saveDataIndex.
        /// </summary>
        private static SavePreset BuildSavePreset()
        {
            var preset = new SavePreset { Data = new List<SaveFolder>() };
            if (_saveDataIndex == null) return preset;

            foreach (var (folderId, dataDict) in _saveDataIndex)
            {
                var folder = new SaveFolder
                {
                    Id = folderId,
                    DataSet = new SaveData[dataDict.Count]
                };

                var i = 0;
                foreach (var (key, value) in dataDict)
                {
                    folder.DataSet[i++] = new SaveData
                    {
                        Id = key,
                        Data = value
                    };
                }

                preset.Data.Add(folder);
            }

            return preset;
        }
    }
}

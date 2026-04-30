using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Unity.SaveSystem.Presets;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        /// <summary>
        /// Сохранить сейв по guid. Пишет {guid}.save (сжатое тело) и {guid}.summary.
        /// При новом GUID — увеличивает инкремент и обновляет файл .in.
        /// </summary>
        public void Save(string name, string guid)
        {
            EnsureSavesDirectory();

            // Собираем SavePreset из переданного индекса данных.
            var preset = BuildSavePreset();
            var dataXml = SerializeSavePreset(guid, preset);

            try
            {
                var savePath = GetSaveFilePath(guid);
                File.WriteAllText(savePath, $"{dataXml}");

                var summary = new SaveSummary(name, DateTime.UtcNow.ToFileTimeUtc());
                var summaryXml = SerializeSummary(summary);
                File.WriteAllText(GetSummaryFilePath(guid), summaryXml);

                if (!Saves.ContainsKey(guid))
                {
                    _increment = GetNumberLastSave() + 1;
                    File.WriteAllText(GetIncrementFilePath(), _increment.ToString(CultureInfo.InvariantCulture));
                }

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
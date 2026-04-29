using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        /// <summary>
        /// Загрузить сейв по guid в индекс данных.
        /// Первая строка файла — base64 превью, отбрасывается при загрузке data.
        /// </summary>
        public void Load(string guid)
        {
            _saveDataIndex.Clear();

            var path = GetSaveFilePath(guid);
            if (!File.Exists(path))
            {
                Debug.LogError($"[FileSystemDriver] Сейв с GUID \"{guid}\" не найден ({path}).");
                return;
            }

            string dataXml;
            try
            {
                dataXml = File.ReadAllText(path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FileSystemDriver] Ошибка чтения сейва {guid}: {e.Message}");
                return;
            }

            var preset = DeserializeSavePreset(guid, dataXml);
            if (preset == null)
            {
                Debug.LogError($"[FileSystemDriver] Не удалось десериализовать сейв {guid}.");
                return;
            }

            foreach (var folder in preset.Data)
            {
                var bucket = new Dictionary<string, string>();
                foreach (var data in folder.DataSet)
                    bucket.Add(data.Id, data.Data);

                _saveDataIndex.AddNew(folder.Id, bucket);
            }
        }

        /// <summary>
        /// Удалить сейв и его summary.
        /// </summary>
        public void Remove(string guid)
        {
            var savePath = GetSaveFilePath(guid);
            var summaryPath = GetSummaryFilePath(guid);

            if (!File.Exists(savePath) && !File.Exists(summaryPath))
            {
                Debug.LogError($"[FileSystemDriver] Сейв \"{guid}\" не найден для удаления.");
                return;
            }

            try
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(summaryPath)) File.Delete(summaryPath);
                Saves.Remove(guid);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FileSystemDriver] Ошибка удаления сейва {guid}: {e.Message}");
            }
        }
    }
}
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
        /// Читает {guid}.save, распаковывает и десериализует SavePreset.
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

            LogLoaded(guid);
        }

        /// <summary>
        /// Лог факта загрузки: имя сейва и версия сборки, которой он записан. Версия печатается, только
        /// если проставлена — у сейвов, записанных до появления поля, она пустая, и «version » ни о чём.
        /// Сводка берётся из индекса (заполнен на Init), перечитывать {guid}.summary незачем.
        /// </summary>
        private static void LogLoaded(string guid)
        {
            if (!Saves.TryGetValue(guid, out var summary))
                return;

            var version = summary.Version.IsNullOrWhitespace() ? "" : $" | version {summary.Version}";
            Debug.Log($"[FileSystemDriver] Загружен сейв \"{ShortName(summary.Name)}\"{version} ({guid}).");
        }

        /// <summary>
        /// Имя сейва для лога. Снаружи в имя может быть упаковано превью через разделитель — берём часть
        /// до него: разбирать чужую упаковку драйверу незачем, а тащить base64 картинки в лог тем более.
        /// </summary>
        private static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "<без имени>";

            var cut = name.IndexOf('|');
            return cut >= 0 ? name[..cut] : name;
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
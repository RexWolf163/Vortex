using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SaveSystem;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Unity.SaveSystem.Presets;
using StringExtensions = Sirenix.Utilities.StringExtensions;

namespace Vortex.Unity.SaveSystem
{
    public partial class SaveSystemDriver : IDriver
    {
        private const string SaveKey = "SavesData";
        private const string SavePrefix = "Save-";
        private const string SaveSummaryPrefix = "SaveSummary-";

        private static readonly Dictionary<string, SaveSummary> Saves = new();

        private static Dictionary<string, Dictionary<string, string>> _saveDataIndex;

        public event Action OnInit;

        public void Init()
        {
            Saves.Clear();
            var saves = PlayerPrefs.GetString(SaveKey);
            if (!StringExtensions.IsNullOrWhitespace(saves))
            {
                var ar = saves.Split(';');
                if (ar.Length > 0)
                    foreach (var guid in ar)
                        Saves.Add(guid, GetSaveSummary(guid));
            }

            OnInit?.Invoke();
        }

        public void Destroy()
        {
        }

        /// <summary>
        /// Сохранить сейв по guid
        /// </summary>
        /// <param name="name"></param>
        /// <param name="guid"></param>
        public void Save(string name, string guid)
        {
            var count = _saveDataIndex.Count;
            var save = new SavePreset
            {
                Data = new List<SaveFolder>()
            };
            var list = _saveDataIndex.Keys.ToArray();
            for (var i = 0; i < count; i++)
            {
                var data = _saveDataIndex[list[i]];
                var xmlStruct = new SaveFolder { Id = list[i], DataSet = new SaveData[data.Count] };
                var j = 0;
                foreach (var key in data.Keys)
                {
                    xmlStruct.DataSet[j++] = new SaveData
                    {
                        Id = key,
                        Data = data[key]
                    };
                }

                save.Data.Add(xmlStruct);
            }

            var xmls = new XmlSerializer(typeof(SavePreset));
            var sw = new StringWriter();
            xmls.Serialize(sw, save);
            var saveData = sw.ToString();
            PlayerPrefs.SetString(GetSaveName(guid), saveData.Compress(guid));

            var summary = new SaveSummary(name, DateTime.UtcNow.ToFileTimeUtc());
            xmls = new XmlSerializer(typeof(SaveSummary));
            sw = new StringWriter();
            xmls.Serialize(sw, summary);
            saveData = sw.ToString();
            PlayerPrefs.SetString(GetSaveSummaryName(guid), saveData);

            Saves.Add(guid, summary);
            PlayerPrefs.SetString(SaveKey, string.Join(";", Saves.Keys));
        }

        /// <summary>
        /// Загрузить сейв по guid
        /// </summary>
        /// <param name="guid"></param>
        public void Load(string guid)
        {
            _saveDataIndex.Clear();
            if (!PlayerPrefs.HasKey(GetSaveName(guid)))
            {
                Debug.LogError($"[SaveSystemDriver] save data #{guid} not found.");
                return;
            }

            var saveData = PlayerPrefs.GetString(GetSaveName(guid));
            saveData = saveData.Decompress(guid);

            var xmls = new XmlSerializer(typeof(SavePreset));
            var save = xmls.Deserialize(new StringReader(saveData)) as SavePreset;
            if (save == null)
            {
                Debug.LogError($"[SaveSystemDriver] Error while loading save data from {guid}.]");
                return;
            }

            foreach (var item in save.Data)
            {
                var list = new Dictionary<string, string>();
                foreach (var data in item.DataSet)
                    list.Add(data.Id, data.Data);

                _saveDataIndex.AddNew(item.Id, list);
            }
        }

        public void Remove(string guid)
        {
            if (!PlayerPrefs.HasKey(GetSaveName(guid)))
            {
                Debug.LogError($"[SaveSystemDriver] save summary #{guid} not found.");
                return;
            }

            Saves.Remove(guid);
            PlayerPrefs.SetString(SaveKey, string.Join(";", Saves.Keys));
            PlayerPrefs.DeleteKey(GetSaveName(guid));
            PlayerPrefs.DeleteKey(GetSaveSummaryName(guid));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Получить описание сейва по guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        private SaveSummary GetSaveSummary(string guid)
        {
            if (!PlayerPrefs.HasKey(GetSaveName(guid)))
            {
                Debug.LogError($"[SaveSystemDriver] save summary #{guid} not found.");
                return default;
            }

            var saveData = PlayerPrefs.GetString(GetSaveSummaryName(guid));
            var xmls = new XmlSerializer(typeof(SaveSummary));
            var obj = xmls.Deserialize(new StringReader(saveData));
            if (obj is SaveSummary summary)
                return summary;
            Debug.LogError($"[SaveSystemDriver] Error while loading save summary from {guid}.]");
            return default;
        }

        public void SetIndexLink(Dictionary<string, Dictionary<string, string>> index) => _saveDataIndex = index;

        /// <summary>
        /// Формирование названия сейва по guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        private static string GetSaveName(string guid) => $"{SavePrefix}{guid}";

        /// <summary>
        /// Формирование названия сейва по guid
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        private static string GetSaveSummaryName(string guid) => $"{SaveSummaryPrefix}{guid}";

        /// <summary>
        /// Возвращает все существующие сейвы
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, SaveSummary> GetIndex() => Saves;
    }
}
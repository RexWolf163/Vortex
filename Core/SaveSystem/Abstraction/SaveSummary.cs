using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Vortex.Core.SaveSystem.Abstraction
{
    [XmlRoot]
    public struct SaveSummary
    {
        public SaveSummary(string name, long unixTimestamp)
        {
            Date = DateTime.Now;
            Name = name;
            Version = string.Empty;
            Meta = null;
            UnixTimestamp = unixTimestamp;
        }

        [XmlElement] public string Name { get; set; }

        [XmlElement] public string Version { get; set; }

        [XmlElement]
        public long UnixTimestamp
        {
            get => Date.ToFileTimeUtc();
            set => Date = DateTime.FromFileTimeUtc(value);
        }

        [XmlIgnore] public DateTime Date { get; private set; }

        /// <summary>
        /// Метаданные сейва: превью, маркеры и прочие блоки от независимых контроллеров. Необязательны:
        /// у сейвов, записанных до появления поля (или драйвером без поддержки метаданных), — null.
        /// </summary>
        [XmlArray, XmlArrayItem("Entry")]
        public List<SaveMeta> Meta { get; set; }

        /// <summary>
        /// Значение метаблока по ключу; null — блока нет.
        /// </summary>
        public string GetMeta(string key)
        {
            if (Meta == null)
                return null;
            foreach (var entry in Meta)
                if (entry.Key == key)
                    return entry.Value;
            return null;
        }

        /// <summary>
        /// Записать метаблок; повторный ключ перезаписывает значение. Вызывать на поле, не на копии из свойства:
        /// без списка метаданных он заводится в самой структуре.
        /// </summary>
        public void SetMeta(string key, string value)
        {
            Meta ??= new List<SaveMeta>();
            for (var i = 0; i < Meta.Count; i++)
            {
                if (Meta[i].Key != key)
                    continue;
                Meta[i] = new SaveMeta { Key = key, Value = value };
                return;
            }

            Meta.Add(new SaveMeta { Key = key, Value = value });
        }
    }

    /// <summary>
    /// Метаблок сводки сейва: ключ и строковое значение.
    /// </summary>
    public struct SaveMeta
    {
        [XmlAttribute] public string Key;
        [XmlText] public string Value;
    }
}

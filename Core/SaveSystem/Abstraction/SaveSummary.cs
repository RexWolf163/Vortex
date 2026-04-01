using System;
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
            UnixTimestamp = unixTimestamp;
        }

        [XmlElement] public string Name { get; set; }

        [XmlElement]
        public long UnixTimestamp
        {
            get => Date.ToFileTimeUtc();
            set => Date = DateTime.FromFileTimeUtc(value);
        }

        [XmlIgnore] public DateTime Date { get; private set; }
    }
}
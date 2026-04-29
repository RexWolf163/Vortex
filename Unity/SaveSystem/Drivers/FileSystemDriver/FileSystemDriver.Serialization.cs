using System.IO;
using System.Xml.Serialization;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Unity.SaveSystem.Presets;

namespace Vortex.Unity.SaveSystem.Drivers.FileSystemDriver
{
    public sealed partial class FileSystemDriver
    {
        private static string SerializeSavePreset(string guid, SavePreset preset)
        {
            var xmls = new XmlSerializer(typeof(SavePreset));
            using var sw = new StringWriter();
            xmls.Serialize(sw, preset);
            return sw.ToString().Compress(guid);
        }

        private static SavePreset DeserializeSavePreset(string guid, string xml)
        {
            xml = xml.Decompress(guid);
            var xmls = new XmlSerializer(typeof(SavePreset));
            using var sr = new StringReader(xml);
            return xmls.Deserialize(sr) as SavePreset;
        }

        private static string SerializeSummary(SaveSummary summary)
        {
            var xmls = new XmlSerializer(typeof(SaveSummary));
            using var sw = new StringWriter();
            xmls.Serialize(sw, summary);
            return sw.ToString();
        }

        private static SaveSummary DeserializeSummary(string xml)
        {
            var xmls = new XmlSerializer(typeof(SaveSummary));
            using var sr = new StringReader(xml);
            return xmls.Deserialize(sr) is SaveSummary summary ? summary : default;
        }
    }
}
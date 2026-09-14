using System.Collections.Generic;
using System.Xml.Serialization;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Core.SaveSystem.Model
{
    /// <summary>
    /// Контейнер глобального хранилища, по образцу <c>SavePreset</c>: папки модулей «ключ модуля → строка
    /// сериализатора». Пишется XML и сжимается, как тело слота. Версии приложения нет: формат данных —
    /// забота модуля.
    /// </summary>
    [XmlRoot("GlobalSave")]
    public class GlobalContainer
    {
        [XmlElement("Module")] public List<SaveData> Modules { get; set; } = new();
    }
}

using System.Collections.Generic;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Sdk.RecordMarksSystem.Models
{
    /// <summary>
    /// Модель меток пакета <c>RecordMarksSystem</c>, живущих per-account. Реализует
    /// <see cref="IGlobalData"/> — рефлексионно подхватывается <c>GlobalSaveController</c>
    /// и переживает слоты, новые игры и рестарты приложения.
    ///
    /// Тип хранения и семантика идентичны <c>RecordMarksSlotData</c> (см. XML-doc там).
    /// </summary>
    public class RecordMarksGlobalData : IGlobalData
    {
        /// <summary>
        /// Хранилище меток: ключ — идентификатор метки, значение — список GUID пресетов
        /// Database, помеченных ею.
        /// </summary>
        public Dictionary<string, List<string>> Data { get; set; } = new();

        /// <summary>Стабильный ключ модуля в глобальном хранилище. Не менять после релиза.</summary>
        public string GetGlobalKey() => "Vortex.RecordMarks.Global";
    }
}

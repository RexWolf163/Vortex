using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.SaveSystem.Abstraction;

namespace Vortex.Sdk.RecordMarksSystem.Models
{
    /// <summary>
    /// Модель меток пакета <c>RecordMarksSystem</c>, живущих per-account. Реализует
    /// <see cref="IGlobalData"/> — рефлексионно подхватывается <c>GlobalSaveController</c>
    /// и переживает слоты, новые игры и рестарты приложения.
    ///
    /// Тип хранения и merge-семантика — как у <c>RecordMarksSlotData</c> (см. XML-doc там), но доступ
    /// закрыт полностью: словарь <see cref="Data"/> — <c>internal</c>, наружу сборки его не прочитать
    /// и не заменить. Единственный писатель и читатель — <c>RecordMarksBus</c> (та же сборка).
    /// </summary>
    public class RecordMarksGlobalData : IGlobalData
    {
        /// <summary>
        /// Хранилище меток: ключ — идентификатор метки, значение — список GUID пресетов Database.
        /// Доступ <c>internal</c> — полный owner-lock: ни ссылку, ни содержимое снаружи сборки не
        /// достать. Сериализуется несмотря на непубличный getter — помечено <c>[IsPOCO]</c>
        /// (<c>GetReadablePropertiesList</c> пускает непубличные свойства только с этим маркером;
        /// сам <c>SerializeController</c> читает/пишет значение рефлексией, обходя <c>internal</c>).
        /// </summary>
        [IsPOCO]
        internal Dictionary<string, List<string>> Data { get; set; } = new();

        /// <summary>Стабильный ключ модуля в глобальном хранилище. Не менять после релиза.</summary>
        public string GetGlobalKey() => "Vortex.RecordMarks.Global";
    }
}

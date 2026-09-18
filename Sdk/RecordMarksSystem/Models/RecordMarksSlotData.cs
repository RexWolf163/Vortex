using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Sdk.Core.GameCore;

namespace Vortex.Sdk.RecordMarksSystem.Models
{
    /// <summary>
    /// Модель меток пакета <c>RecordMarksSystem</c>, живущих per-slot. Реализует
    /// <see cref="GameModel.IGameData"/> — рефлексионно подхватывается <c>GameController</c>
    /// при <c>Init</c>, сериализуется/десериализуется через стандартный контракт save-модели.
    ///
    /// Тип хранения — <see cref="Dictionary{TKey,TValue}"/> со значением-<c>List</c>,
    /// потому что сериализатор Vortex не понимает <c>HashSet</c> (нет реализации <c>IList</c>).
    /// O(1)-операции над множеством GUID обеспечивает <c>RecordMarksBus</c> отдельным
    /// runtime-кешем <c>HashSet</c>, синхронизируемым с <see cref="Data"/> при каждой мутации.
    ///
    /// Merge-семантика Vortex-сериализатора при загрузке слота:
    /// - внешний <see cref="Dictionary{TKey,TValue}"/> мержится — новые метки из SO остаются
    ///   с пустыми списками, если их не было в сейве;
    /// - внутренний <see cref="List{T}"/> replace'ится целиком — состояние берётся из сейва.
    /// </summary>
    public class RecordMarksSlotData : GameModel.IGameData
    {
        /// <summary>
        /// Хранилище меток: ключ — идентификатор метки, значение — список GUID пресетов Database.
        /// Доступ <c>internal</c> — полный owner-lock (зеркально <c>RecordMarksGlobalData</c>): ни
        /// ссылку, ни содержимое снаружи сборки не достать. Сериализуется несмотря на непубличный
        /// getter — помечено <c>[IsPOCO]</c>; <c>SerializeController</c> читает/пишет рефлексией.
        /// </summary>
        [IsPOCO]
        internal Dictionary<string, List<string>> Data { get; set; } = new();
    }
}

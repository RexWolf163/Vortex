using System.Collections.Generic;
using System.Linq;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Sdk.Quests

{
    [POCO]
    public class QuestModels : Core.GameCore.GameModel.IGameData
    {
        /// <summary>
        /// Индекс кестов
        /// Guid => квест
        /// </summary>
        public Dictionary<string, QuestModel> Index { get; internal set; } =
            Database.GetNewRecords<QuestModel>().ToDictionary(q => q.GuidPreset, q => q);
    }
}
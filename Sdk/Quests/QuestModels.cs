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
            Database.GetNewRecords<QuestModel>().ToDictionary(q => q.GuidPreset, WithName);

        /// <summary>
        /// Читаемое имя записи: <c>CopyFrom</c> переносит из пресета только <c>Name</c> (совпадение имён свойств),
        /// поэтому <see cref="QuestModel.QuestName"/> проставляется здесь — иначе он пуст до новой игры или загрузки.
        /// </summary>
        private static QuestModel WithName(QuestModel quest)
        {
            quest.QuestName = quest.Name;
            return quest;
        }
    }
}
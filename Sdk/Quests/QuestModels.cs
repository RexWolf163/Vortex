using System.Collections.Generic;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;

namespace Vortex.Sdk.Quests

{
    [POCO]
    public class QuestModels : Core.GameCore.GameModel.IGameData
    {
        public Dictionary<string, QuestModel> Index { get; internal set; } = new();
    }
}
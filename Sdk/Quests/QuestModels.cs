using System.Collections.Generic;

namespace Vortex.Sdk.Quests

{
    public class QuestModels : Core.GameCore.GameModel.IGameData
    {
        public Dictionary<string, QuestModel> Index { get; internal set; } = new();
    }
}
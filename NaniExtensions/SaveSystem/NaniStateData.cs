using System.Collections.Generic;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.NaniExtensions.SaveSystem
{
    public class NaniStateData : Sdk.Core.GameCore.GameModel.IGameData
    {
        public Dictionary<string, IReactiveData> Variables { get; internal set; } = new();
    }
}
using System;
using Naninovel;
using UnityEngine.Scripting;
using Vortex.Unity.CutsceneSystem.Bus;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: «дальше» — перейти к следующему этапу катсцены. Катсцена не открыта либо ещё действует
    /// защита от дребезга — ничего не делает
    /// </summary>
    /// <example>@cutscene_Next</example>
    [Serializable]
    [CommandAlias("cutscene_Next"), Preserve]
    public class NextCutsceneCommand : Command
    {
        public override UniTask Execute(AsyncToken token = default)
        {
            CutsceneBus.Next();
            return UniTask.CompletedTask;
        }
    }
}

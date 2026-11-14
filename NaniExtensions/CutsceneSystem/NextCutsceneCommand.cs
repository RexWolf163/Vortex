using System;
using Naninovel;
using UnityEngine.Scripting;
using Vortex.Sdk.CutsceneSystem;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: вывести открытую катсцену из зафиксированного лупа на следующую фазу. На сквозной фазе,
    /// на последней фазе и без открытой катсцены ничего не делает — как <see cref="CutsceneController.Next"/>
    /// </summary>
    /// <example>@cutscene_Next</example>
    [Serializable]
    [CommandAlias("cutscene_Next"), Preserve]
    public class NextCutsceneCommand : Command
    {
        public override UniTask Execute(AsyncToken token = default)
        {
            CutsceneController.Next();
            return UniTask.CompletedTask;
        }
    }
}

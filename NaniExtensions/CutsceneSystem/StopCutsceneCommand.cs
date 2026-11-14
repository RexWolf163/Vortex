using System;
using Naninovel;
using UnityEngine.Scripting;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: закрыть открытую катсцену и убрать её фон-актёра. Катсцена не открыта — ничего не делает
    /// </summary>
    /// <example>@cutscene_Stop</example>
    [Serializable]
    [CommandAlias("cutscene_Stop"), Preserve]
    public class StopCutsceneCommand : Command
    {
        public override UniTask Execute(AsyncToken token = default)
        {
            CutsceneStage.Hide();
            return UniTask.CompletedTask;
        }
    }
}

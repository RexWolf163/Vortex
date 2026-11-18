using System;
using Naninovel;
using UnityEngine.Scripting;
using Vortex.Unity.CutsceneSystem.Bus;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: пропустить катсцену — показ закрывается, контент выгружается. Сценарий ждёт,
    /// пока катсцена закроется: шина исполняет команды в конце кадра, и следующая строка сценария не должна
    /// выполниться при ещё открытой катсцене. Катсцена не открыта — ничего не делает
    /// </summary>
    /// <example>@cutscene_Skip</example>
    [Serializable]
    [CommandAlias("cutscene_Skip"), Preserve]
    public class SkipCutsceneCommand : Command
    {
        public override async UniTask Execute(AsyncToken token = default)
        {
            if (CutsceneBus.IsActive)
                await CutsceneWait.Closed(token);
        }
    }
}

using System;
using Naninovel;
using UnityEngine.Scripting;
using Vortex.Unity.CutsceneSystem.Bus;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: остановить катсцену — показ прерывается, контент выгружается. Сценарий ждёт, пока
    /// катсцена закроется. Катсцена не открыта — ничего не делает.
    ///
    /// Прерывание у шины одно — немедленное закрытие показа, поэтому по действию команда совпадает
    /// с <c>@cutscene_Skip</c>. Отдельное имя — для сценариев, где катсцену останавливает сам сценарий,
    /// а не игрок
    /// </summary>
    /// <example>@cutscene_Stop</example>
    [Serializable]
    [CommandAlias("cutscene_Stop"), Preserve]
    public class StopCutsceneCommand : Command
    {
        public override async UniTask Execute(AsyncToken token = default)
        {
            if (CutsceneBus.IsActive)
                await CutsceneWait.Closed(token);
        }
    }
}

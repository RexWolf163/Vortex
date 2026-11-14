using UnityEngine;

namespace Vortex.Unity.CutsceneSystem.Models
{
    /// <summary>
    /// Состояние оркестратора катсцен. Порядок значений — порядок состояний его <c>UIStateSwitcher</c>
    /// </summary>
    public enum CutsceneStageState
    {
        [Tooltip("Катсцены нет")] Off = 0,

        [Tooltip("Контент катсцены загружается")]
        Loading = 1,

        [Tooltip("Катсцена идёт")] On = 2,
    }
}

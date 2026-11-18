using System;
using UnityEngine;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Unity.CutsceneSystem.Bus;
using Vortex.Unity.UIProviderSystem.Model;

namespace Vortex.Unity.CutsceneSystem.UIConditions
{
    /// <summary>
    /// Условие интерфейса: открыт, пока катсцены нет; закрыт, пока катсцена открывается (грузится) или идёт —
    /// по <see cref="CutsceneBus.IsActive"/>. С <see cref="invert"/> — наоборот: открыт только на время катсцены.
    /// Перепроверка — по началу открытия (<see cref="CutsceneBus.OnShowOpening"/>) и концу показа
    /// (<see cref="CutsceneBus.OnShowFinished"/>)
    /// </summary>
    [Serializable]
    public class NoneCutscene : UnityUserInterfaceCondition
    {
        [SerializeField, Tooltip("Наоборот: интерфейс открыт, пока катсцена открывается или идёт, и закрыт без неё.")]
        private bool invert;

        protected override void Run()
        {
            CutsceneBus.OnShowOpening += OnShowEvent;
            CutsceneBus.OnShowFinished += OnShowEvent;
            RunCallback();
        }

        public override void DeInit()
        {
            CutsceneBus.OnShowOpening -= OnShowEvent;
            CutsceneBus.OnShowFinished -= OnShowEvent;
        }

        private void OnShowEvent(string key) => RunCallback();

        /// <summary>Надпись в инспекторе отражает режим: с инверсией условие ждёт идущую катсцену</summary>
        protected override string GetLabel() => invert ? "ActiveCutscene" : nameof(NoneCutscene);

        public override ConditionAnswer Check() => CutsceneBus.IsActive != invert
            ? ConditionAnswer.Close
            : ConditionAnswer.Open;
    }
}

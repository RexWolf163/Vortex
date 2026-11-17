using System;
using Naninovel;
using UnityEngine;
using UnityEngine.Scripting;
using Vortex.Unity.CutsceneSystem.Bus;

namespace Vortex.NaniExtensions.CutsceneSystem
{
    /// <summary>
    /// Nani-команда: «дальше» — сигнал <c>Next</c> графу катсцены. Катсцена не открыта — ничего не делает.
    ///
    /// Пока заперт замок «дальше» (<see cref="CutsceneBus.IsLocked"/>: узел с автопереходом доигрывает),
    /// команда ждёт и сигнал не подаёт: иначе при промотке «дальше» увело бы граф с вводного узла вместо
    /// следующего, и катсцена отстала бы от текста. Вводная анимация идёт по игровому времени и при
    /// промотке ускоряется вместе с текстом.
    ///
    /// После сигнала сценарий выдерживает паузу <see cref="Delay"/> реального времени. Шина исполняет «дальше»
    /// на проходе оркестратора, и две просьбы за один проход сливаются в одну: без паузы соседние команды
    /// терялись бы. Время реальное: промотка ускоряет игровое время, а паузу не сокращает
    /// </summary>
    /// <example>@cutscene_Next</example>
    [Serializable]
    [CommandAlias("cutscene_Next"), Preserve]
    public class NextCutsceneCommand : Command
    {
        /// <summary>
        /// Пауза сценария после сигнала, секунды реального времени
        /// </summary>
        private const float Delay = 0.2f;

        public override async UniTask Execute(AsyncToken token = default)
        {
            if (!CutsceneBus.IsActive)
                return;

            //Сигнал команды не сольётся с автопереходом, который отпустил замок: тот уже исполнен
            await CutsceneWait.Unlocked(token);

            //Катсцену закрыли, пока команда ждала автоперехода
            if (!CutsceneBus.IsActive)
                return;

            CutsceneBus.Next();

            var player = Engine.GetServiceOrErr<IScriptPlayer>();

            //Как таймер @wait, но и при промотке: ради неё пауза и нужна. Хотя бы кадр ждётся всегда —
            //за него оркестратор успевает исполнить просьбу
            var startTime = Engine.Time.UnscaledTime;
            while (Application.isPlaying && !player.Completing && token.EnsureNotCanceledOrCompleted())
            {
                await AsyncUtils.WaitEndOfFrame(token);
                if (Engine.Time.UnscaledTime - startTime >= Delay)
                    break;
            }
        }
    }
}

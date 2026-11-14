using UnityEngine;

namespace Vortex.Unity.CutsceneSystem.Handlers
{
    /// <summary>
    /// Хэндлер контента катсцены, у которой смена фазы — переход на твинах (смена спрайтов через слои
    /// old и current). Следующее состояние выставляется не раньше, чем через <see cref="switchDelay"/> после
    /// предыдущего: иначе переход оборвался бы на полпути и кадр дал бы скачок яркости.
    ///
    /// Фазы, пришедшие во время ожидания, не теряются и не перескакиваются: состояния выставляются по
    /// одному, строго по порядку. Первая фаза показывается сразу — до неё переходов не было.
    ///
    /// Задержка и длительность твинов друг о друге не знают: согласовать их — дело верстальщика
    /// </summary>
    public class CutsceneSpriteContentHandler : CutsceneContentHandler
    {
        [SerializeField, Min(0f),
         Tooltip("Минимальное время между переключениями фаз, секунды. Не меньше длительности твинов перехода.")]
        private float switchDelay = 0.5f;

        /// <summary>
        /// Фаза, состояние которой сейчас выставлено
        /// </summary>
        private int _shown;

        /// <summary>
        /// Фаза, до которой нужно дойти
        /// </summary>
        private int _target;

        /// <summary>
        /// Время, раньше которого следующее состояние не выставляется
        /// </summary>
        private float _nextSwitchAt;

        protected override void ShowPhase(int phase)
        {
            _target = phase;
            Step();
        }

        private void Update()
        {
            if (_shown != _target)
                Step();
        }

        private void Step()
        {
            //Сброс и возврат назад переходом не сопровождаются — ждать нечего
            if (_target <= _shown)
            {
                _shown = _target;
                SetState(_shown);
                return;
            }

            //Из пустого исходного состояния — сразу: переходов ещё не было
            if (_shown > 0 && Time.unscaledTime < _nextSwitchAt)
                return;

            _shown++;
            _nextSwitchAt = Time.unscaledTime + switchDelay;
            SetState(_shown);
        }
    }
}

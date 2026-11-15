using Spine;
using Spine.Unity;
using UnityEngine;
using Vortex.Unity.CutsceneSystem.Handlers;
using AnimationState = Spine.AnimationState;

namespace Vortex.SpineExtensions.Cutscenes
{
    /// <summary>
    /// Автопереход фазы катсцены по концу Spine-анимации: когда текущая анимация трека отыграла проход, шине
    /// уходит сигнал «дальше». Сигнал идёт через хэндлер контента: тот называет шине показанную фазу, и
    /// сигнал от фазы, с которой цепочка уже ушла, отбрасывается.
    ///
    /// Работает, пока включён, и срабатывает один раз за включение. Ставится на объект, который включает
    /// состояние фазы с автопереходом; в фазах, ждущих команды, его быть не должно
    /// </summary>
    public class SpineCutsceneAutoNext : MonoBehaviour
    {
        [SerializeField, Tooltip("Хэндлер контента катсцены — через него уходит сигнал «дальше».")]
        private CutsceneContentHandler content;

        [SerializeField, Tooltip("Скелет в UI. Задаётся либо он, либо Skeleton Animation.")]
        private SkeletonGraphic skeletonGraphic;

        [SerializeField, Tooltip("Скелет в мире. Задаётся либо он, либо Skeleton Graphic.")]
        private SkeletonAnimation skeletonAnimation;

        [SerializeField, Range(0, 10), Tooltip("Трек, конец анимации которого ждём.")]
        private int track;

        private AnimationState _state;
        private bool _fired;

        private void OnEnable()
        {
            _fired = false;
            _state = SpineCutsceneSkeleton.GetState(skeletonGraphic, skeletonAnimation);
            _state.Complete += OnComplete;
        }

        private void OnDisable()
        {
            if (_state != null)
                _state.Complete -= OnComplete;
            _state = null;
        }

        private void OnComplete(TrackEntry entry)
        {
            //Уходящая в миксе анимация тоже сообщает о своём конце — ждём только текущую
            if (_fired || entry.TrackIndex != track || entry != SpineCutsceneSkeleton.GetTrack(_state, track))
                return;

            _fired = true;
            content.Next();
        }
    }
}

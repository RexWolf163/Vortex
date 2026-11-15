using Spine.Unity;
using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;
using AnimationState = Spine.AnimationState;
using Random = UnityEngine.Random;

namespace Vortex.SpineExtensions.UIs
{
    /// <summary>
    /// Хэндлер для создания рассинхрона однотипных анимаций
    /// </summary>
    public class SpineRandomizationStart : MonoBehaviour
    {
        [AutoLink, SerializeField] private SkeletonGraphic skeletonGraphic;
        [AutoLink, SerializeField] private SkeletonAnimation skeletonAnimation;

        [SerializeField, Range(0, 10)] private int channelAnimation;

        private void OnEnable()
        {
            if (skeletonGraphic == null && skeletonAnimation == null) return;
            TimeController.Accumulate(RandomShift, this);
        }

        private void OnDisable()
        {
            TimeController.RemoveCall(this);
        }

        private void RandomShift()
        {
            //У SkeletonGraphic состояние берётся независимо от версии Spine: в 4.3 его ведёт соседний компонент
            var graphicState = skeletonGraphic != null ? SpineCompat.GetAnimationState(skeletonGraphic) : null;
            if (graphicState != null)
                ApplyForState(graphicState);

            if (skeletonAnimation == null) return;

            //В 4.3 оба поля ведут на один объект — тогда состояние у них общее и уже сдвинуто
            var animationState = skeletonAnimation.AnimationState;
            if (!ReferenceEquals(animationState, graphicState))
                ApplyForState(animationState);
        }

        private void ApplyForState(AnimationState state)
        {
            var track = SpineCompat.GetTrack(state, channelAnimation);
            if (track == null) return;
            track.TrackTime = Random.Range(0f, track.Animation.Duration);
        }
    }
}
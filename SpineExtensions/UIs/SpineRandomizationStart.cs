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
            if (skeletonGraphic != null)
                ApplyForState(skeletonGraphic.AnimationState);

            if (skeletonAnimation != null)
                ApplyForState(skeletonAnimation.AnimationState);
        }

        private void ApplyForState(AnimationState state)
        {
            var track = state.GetCurrent(channelAnimation);
            if (track == null) return;
            track.TrackTime = Random.Range(0f, track.Animation.Duration);
        }
    }
}
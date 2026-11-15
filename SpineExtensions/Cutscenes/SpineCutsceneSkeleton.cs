using Spine;
using Spine.Unity;
using UnityEngine;
using AnimationState = Spine.AnimationState;

namespace Vortex.SpineExtensions.Cutscenes
{
    /// <summary>
    /// Доступ к скелету катсцены, заданному одним из двух полей: UGUI либо Mesh.
    ///
    /// Обращения не зависят от версии рантайма Spine (4.2 и 4.3). В 4.3 <c>SkeletonGraphic</c> анимацию
    /// не ведёт — её состояние живёт в компоненте <c>SkeletonAnimation</c> на том же объекте, поэтому
    /// состояние берётся через интерфейс <see cref="IAnimationStateComponent"/>, общий для обеих версий
    /// </summary>
    internal static class SpineCutsceneSkeleton
    {
        public static AnimationState GetState(SkeletonGraphic graphic, SkeletonAnimation animation) =>
            graphic != null ? GetState(graphic) : animation.AnimationState;

        public static SkeletonDataAsset GetDataAsset(SkeletonGraphic graphic, SkeletonAnimation animation)
        {
            if (graphic != null)
                return graphic.skeletonDataAsset;
            return animation != null ? animation.skeletonDataAsset : null;
        }

        /// <summary>
        /// Текущая запись трека; <c>null</c> — трек пуст. Замена <c>GetCurrent</c> (4.2) / <c>GetTrack</c> (4.3)
        /// </summary>
        public static TrackEntry GetTrack(AnimationState state, int trackIndex)
        {
            var tracks = state.Tracks;
            return trackIndex >= 0 && trackIndex < tracks.Count ? tracks.Items[trackIndex] : null;
        }

        /// <summary>
        /// Состояние анимации UGUI-скелета: в 4.2 его ведёт сам <c>SkeletonGraphic</c>, в 4.3 — соседний
        /// компонент анимации
        /// </summary>
        private static AnimationState GetState(Component graphic)
        {
            var component = graphic as IAnimationStateComponent ?? graphic.GetComponent<IAnimationStateComponent>();
            return component.AnimationState;
        }
    }
}

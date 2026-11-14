using Spine.Unity;
using AnimationState = Spine.AnimationState;

namespace Vortex.SpineExtensions.Cutscenes
{
    /// <summary>
    /// Доступ к скелету катсцены, заданному одним из двух полей: UGUI либо Mesh
    /// </summary>
    internal static class SpineCutsceneSkeleton
    {
        public static AnimationState GetState(SkeletonGraphic graphic, SkeletonAnimation animation) =>
            graphic != null ? graphic.AnimationState : animation.AnimationState;

        public static SkeletonDataAsset GetDataAsset(SkeletonGraphic graphic, SkeletonAnimation animation)
        {
            if (graphic != null)
                return graphic.skeletonDataAsset;
            return animation != null ? animation.skeletonDataAsset : null;
        }
    }
}

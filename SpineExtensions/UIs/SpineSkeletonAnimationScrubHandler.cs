using Spine.Unity;

namespace Vortex.SpineExtensions.UIs
{
    /// <summary>
    /// Скраб Spine-анимации по <see cref="Vortex.Core.Extensions.ReactiveValues.FloatData"/>
    /// для <see cref="SkeletonAnimation"/> (MeshRenderer-вариант).
    /// Вся логика — в <see cref="SpineAnimationScrubHandlerBase{T}"/>.
    /// </summary>
    public class SpineSkeletonAnimationScrubHandler : SpineAnimationScrubHandlerBase<SkeletonAnimation>
    {
    }
}

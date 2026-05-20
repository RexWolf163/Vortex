using System;
using Spine.Unity;

namespace Vortex.SpineExtensions.TweenerSystem
{
    /// <summary>
    /// Случайная взвешенная анимация Spine для <see cref="SkeletonAnimation"/> (MeshRenderer-вариант).
    /// Вся логика — в <see cref="SpineAnimationRandomLogicBase{T}"/>.
    /// </summary>
    [Serializable]
    public class SpineSkeletonAnimationRandomLogic : SpineAnimationRandomLogicBase<SkeletonAnimation>
    {
    }
}

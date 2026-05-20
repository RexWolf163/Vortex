using System;
using Spine.Unity;

namespace Vortex.SpineExtensions.TweenerSystem
{
    /// <summary>
    /// Одиночная анимация Spine для <see cref="SkeletonAnimation"/> (MeshRenderer-вариант).
    /// Вся логика — в <see cref="SpineAnimationLogicBase{T}"/>.
    /// </summary>
    [Serializable]
    public class SpineSkeletonAnimationLogic : SpineAnimationLogicBase<SkeletonAnimation>
    {
    }
}

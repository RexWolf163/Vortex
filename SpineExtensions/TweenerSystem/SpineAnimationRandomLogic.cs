using System;
using Spine.Unity;

namespace Vortex.SpineExtensions.TweenerSystem
{
    /// <summary>
    /// Случайная взвешенная анимация Spine для <see cref="SkeletonGraphic"/> (UGUI-вариант).
    /// Вся логика — в <see cref="SpineAnimationRandomLogicBase{T}"/>.
    /// </summary>
    [Serializable]
    public class SpineAnimationRandomLogic : SpineAnimationRandomLogicBase<SkeletonGraphic>
    {
    }
}

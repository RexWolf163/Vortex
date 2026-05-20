using System;
using Spine.Unity;

namespace Vortex.SpineExtensions.TweenerSystem
{
    /// <summary>
    /// Одиночная анимация Spine для <see cref="SkeletonGraphic"/> (UGUI-вариант).
    /// Вся логика — в <see cref="SpineAnimationLogicBase{T}"/>.
    /// </summary>
    [Serializable]
    public class SpineAnimationLogic : SpineAnimationLogicBase<SkeletonGraphic>
    {
    }
}

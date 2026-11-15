using System;
using Spine;
using Spine.Unity;
using UnityEngine;
using AnimationState = Spine.AnimationState;

namespace Vortex.SpineExtensions
{
    /// <summary>
    /// Совместимость с рантаймами Spine 4.2 и 4.3: обращения, которые в этих версиях устроены по-разному,
    /// собраны здесь, чтобы остальной код пакета от версии не зависел. Символов условной компиляции нет:
    /// пакет собирается под любую из версий без настройки.
    ///
    /// В 4.3 анимацию и отрисовку развели по разным компонентам: <c>SkeletonGraphic</c> больше не ведёт
    /// анимацию, её состояние живёт в компоненте <c>SkeletonAnimation</c> на том же объекте. Поэтому
    /// состояние анимации берётся через интерфейс <see cref="IAnimationStateComponent"/>, общий для
    /// обеих версий, а не через свойство конкретного компонента.
    ///
    /// Метод, который между версиями только переименован, находится по имени один раз, при первом обращении
    /// </summary>
    public static class SpineCompat
    {
        /// <summary>
        /// Сброс слотов скелета в исходную позу: <c>SetupPoseSlots</c> (4.3) либо <c>SetSlotsToSetupPose</c> (4.2)
        /// </summary>
        private static Action<Skeleton> _resetSlots;

        /// <summary>
        /// Состояние анимации скелета. Скелет — компонент Spine любой версии: либо он сам ведёт анимацию
        /// (4.2, а также <c>SkeletonAnimation</c> в 4.3), либо её ведёт соседний компонент на том же объекте
        /// (<c>SkeletonGraphic</c> и <c>SkeletonRenderer</c> в 4.3). <c>null</c> — анимацию у объекта вести некому
        /// </summary>
        public static AnimationState GetAnimationState(Component skeleton)
        {
            var component = skeleton as IAnimationStateComponent ?? skeleton.GetComponent<IAnimationStateComponent>();
            return component?.AnimationState;
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
        /// Вернуть слоты скелета в исходную позу — после смены скина
        /// </summary>
        public static void ResetSlots(Skeleton skeleton)
        {
            _resetSlots ??= BindSkeletonMethod("SetupPoseSlots", "SetSlotsToSetupPose");
            _resetSlots(skeleton);
        }

        /// <summary>
        /// Метод скелета без параметров под первым из имён, которое есть в установленном рантайме
        /// </summary>
        private static Action<Skeleton> BindSkeletonMethod(params string[] names)
        {
            foreach (var name in names)
            {
                var method = typeof(Skeleton).GetMethod(name, Type.EmptyTypes);
                if (method != null)
                    return (Action<Skeleton>)Delegate.CreateDelegate(typeof(Action<Skeleton>), method);
            }

            throw new MissingMethodException(nameof(Skeleton), names[0]);
        }
    }
}

using System;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Misc;

namespace Vortex.SpineExtensions.UIs
{
    /// <summary>
    /// Базовый хэндлер «скраб» Spine-анимации по значению <see cref="FloatData"/> (0..1).
    /// Общий для SkeletonGraphic (UGUI) и SkeletonAnimation (MeshRenderer); подкласс отличается
    /// только типом поля <see cref="skeleton"/>.
    ///
    /// Анимация ставится на указанный трек с <c>TimeScale = 0</c> (заморозка штатного хода),
    /// а <c>TrackTime</c> выставляется руками каждый раз при изменении <c>Data.Value</c>.
    /// Spine штатным пайплайном применит позу в <c>LateUpdate</c>.
    ///
    /// Источник <see cref="FloatData"/> — любой <see cref="Vortex.Core.System.Abstractions.IDataStorage"/>
    /// (поле <c>source</c> в инспекторе), наполнение через <see cref="DataStorageView{T}"/>.
    /// </summary>
    public abstract class SpineAnimationScrubHandlerBase<TSkeleton> : DataStorageView<FloatData>
        where TSkeleton : MonoBehaviour, IAnimationStateComponent, IHasSkeletonDataAsset
    {
        [SerializeField, ClassFilter(typeof(IAnimationStateComponent), typeof(IHasSkeletonDataAsset))]
        protected TSkeleton skeleton;

        [SerializeField, ValueSelector("GetListAnimations")]
        private string animationName;

        [SerializeField, Range(0, 10), Tooltip("Канал AnimationState для скраб-трека.")]
        private byte channel = 1;

        private TrackEntry _track;

        protected override void Init()
        {
            base.Init();
            if (skeleton == null || string.IsNullOrEmpty(animationName)) return;

            _track = skeleton.AnimationState.SetAnimation(channel, animationName, false);
            _track.TimeScale = 0;
            Apply();
        }

        protected override void DeInit()
        {
            if (skeleton != null && skeleton.AnimationState != null)
                skeleton.AnimationState.SetEmptyAnimation(channel, 0);
            _track = null;
            base.DeInit();
        }

        protected override void OnDataUpdated() => Apply();

        /// <summary>
        /// Применяет текущее <c>Data.Value</c> к позиции трека.
        /// </summary>
        private void Apply()
        {
            if (_track?.Animation == null || Data == null) return;
            _track.TrackTime = Mathf.Clamp01(Data.Value) * _track.Animation.Duration;
        }

#if UNITY_EDITOR
        private string[] GetListAnimations()
        {
            var data = skeleton != null && skeleton.SkeletonDataAsset != null
                ? skeleton.SkeletonDataAsset.GetSkeletonData(true)
                : null;
            return data?.Animations.Select(a => a.Name).ToArray() ?? Array.Empty<string>();
        }
#endif
    }
}

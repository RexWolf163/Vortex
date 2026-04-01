using System;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.NaniExtensions.CutsceneSystem.Models
{
    [Serializable]
    public class CutscenePhase
    {
        /// <summary>
        /// Ключ анимации которую запустим на этой фазе
        /// </summary>
        [field: SerializeField, ValueDropdown("GetAnimations")]
        public string AnimationKey { get; set; } = string.Empty;

        /// <summary>
        /// Будет ли анимация залупой?
        /// </summary>
        [field: SerializeField]
        public bool AnimationLooped { get; set; } = false;

        /// <summary>
        /// Звук эмбиент который будем запускать на этой фазе
        /// </summary>
        [field: SerializeField, DbRecord(typeof(Sound))]
        public string AmbientAudioPack { get; set; } = null;

#if UNITY_EDITOR

        private SkeletonDataAsset _data;

        public void InjectSkeleton(SkeletonDataAsset skeletonData)
        {
            _data = skeletonData;
        }

        private string[] GetAnimations() =>
            _data == null
                ? Array.Empty<string>()
                : _data.GetAnimationStateData().SkeletonData.Animations.Select(a => a.Name).ToArray();
#endif
    }
}
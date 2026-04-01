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
    public class EventToAudioData
    {
        [field: SerializeField, ValueDropdown("GetEvents")]
        public string EventName { get; set; } = string.Empty;

        [field: SerializeField, DbRecord(typeof(Sound))]
        public string AudioPack { get; set; } = null;

#if UNITY_EDITOR

        private SkeletonDataAsset _data;

        public void InjectSkeleton(SkeletonDataAsset skeletonData)
        {
            _data = skeletonData;
        }

        private string[] GetEvents() =>
            _data == null
                ? Array.Empty<string>()
                : _data.GetAnimationStateData().SkeletonData.Events.Select(a => a.Name).ToArray();
#endif
    }
}
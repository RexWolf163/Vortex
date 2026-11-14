using System;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.CutsceneSystem.Models
{
    /// <summary>
    /// Звук, привязанный к событию анимации катсцены. Звуки событий лёгкие и повторяются между катсценами,
    /// поэтому берутся из общей базы звуков, а не грузятся вместе с катсценой
    /// </summary>
    [Serializable]
    public class EventToAudioData
    {
        [field: SerializeField, ValueDropdown("GetEvents")]
        public string EventName { get; internal set; } = string.Empty;

        /// <summary>
        /// Запись звука в базе
        /// </summary>
        [field: SerializeField, DbRecord(typeof(Sound))]
        public string AudioPack { get; internal set; } = null;

#if UNITY_EDITOR

        private Func<SkeletonDataAsset> _skeleton;

        /// <summary>
        /// Источник скелета катсцены для выпадашки событий
        /// </summary>
        public void InjectSkeleton(Func<SkeletonDataAsset> skeleton)
        {
            _skeleton = skeleton;
        }

        private string[] GetEvents()
        {
            var data = _skeleton?.Invoke();
            return data == null
                ? Array.Empty<string>()
                : data.GetAnimationStateData().SkeletonData.Events.Select(a => a.Name).ToArray();
        }
#endif
    }
}

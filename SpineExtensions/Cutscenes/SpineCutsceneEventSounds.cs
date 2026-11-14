using System;
using Sirenix.OdinInspector;
using Spine;
using Spine.Unity;
using UnityEngine;
using Vortex.Core.AudioSystem.Bus;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;
using AnimationState = Spine.AnimationState;
using Event = Spine.Event;
#if UNITY_EDITOR
using System.Linq;
#endif

namespace Vortex.SpineExtensions.Cutscenes
{
    /// <summary>
    /// Звуки событий Spine-анимации катсцены: на событие скелета играет запись звука из базы. Таблица
    /// «событие → звук» настраивается в префабе контента.
    ///
    /// Звуки событий лёгкие и повторяются между катсценами, поэтому берутся из общей базы и вместе с контентом
    /// не выгружаются. На паузе скелет заморожен (<c>SpinePauseHandler</c>) — событий, а значит и новых звуков, нет
    /// </summary>
    public class SpineCutsceneEventSounds : MonoBehaviour
    {
        [Serializable]
        private class Entry
        {
            [SerializeField, ValueDropdown("GetEvents")]
            private string eventName;

            [SerializeField, DbRecord(typeof(Sound))]
            private string sound;

            public string EventName => eventName;
            public string SoundId => sound;

#if UNITY_EDITOR
            private Func<string[]> _events;

            /// <summary>
            /// Источник имён событий скелета для выпадашки
            /// </summary>
            public void InjectEvents(Func<string[]> events) => _events = events;

            private string[] GetEvents() => _events != null ? _events() : new string[0];
#endif
        }

        [SerializeField, Tooltip("Скелет в UI. Задаётся либо он, либо Skeleton Animation.")]
        private SkeletonGraphic skeletonGraphic;

        [SerializeField, Tooltip("Скелет в мире. Задаётся либо он, либо Skeleton Graphic.")]
        private SkeletonAnimation skeletonAnimation;

        [SerializeField] private Entry[] sounds = new Entry[0];

        private AnimationState _state;

        private void OnEnable()
        {
            _state = SpineCutsceneSkeleton.GetState(skeletonGraphic, skeletonAnimation);
            _state.Event += OnEvent;
        }

        private void OnDisable()
        {
            if (_state != null)
                _state.Event -= OnEvent;
            _state = null;
        }

        private void OnEvent(TrackEntry entry, Event e)
        {
            foreach (var item in sounds)
            {
                if (item.EventName != e.Data.Name)
                    continue;
                if (!string.IsNullOrEmpty(item.SoundId))
                    AudioController.PlaySound(item.SoundId);
                return;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            foreach (var item in sounds)
                item.InjectEvents(GetEventNames);
        }

        private string[] GetEventNames()
        {
            var asset = SpineCutsceneSkeleton.GetDataAsset(skeletonGraphic, skeletonAnimation);
            return asset == null
                ? new string[0]
                : asset.GetSkeletonData(true).Events.Select(eventData => eventData.Name).ToArray();
        }
#endif
    }
}

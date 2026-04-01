using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Unity.AudioSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.NaniExtensions.CutsceneSystem.Models
{
    [CreateAssetMenu(menuName = "MusicIdol/Sex Scene/Sex Scene Data")]
    public class CutsceneData : ScriptableObject
    {
        public const string AddressablePathKey = "SexScenes/{0}";


        [field: SerializeField] public SkeletonDataAsset SkeletonDataAsset { get; set; } = null;
        [field: SerializeField] public List<CutscenePhase> Phases { get; set; } = new();

        [field: SerializeField] public List<EventToAudioData> EventToAudioDatas { get; set; } = new();

        [field: SerializeField, DbRecord(typeof(Music))]
        public List<string> SexSceneAmbients { get; private set; } = new();

#if UNITY_EDITOR
        [ContextMenu("Sync Spine Phases")]

#if ODIN_INSPECTOR
        [GUIColor("#88ff88"), Button(ButtonSizes.Large), PropertyOrder(-100)]
#endif
        private void SyncWithSpine()
        {
            var animations = SkeletonDataAsset.GetSkeletonData(false).Animations.ToArray().Select(a => a.Name)
                .ToHashSet();
            var listRemove = new HashSet<CutscenePhase>();
            foreach (var phase in Phases)
            {
                if (animations.Contains(phase.AnimationKey))
                    continue;
                listRemove.Add(phase);
            }

            foreach (var phase in listRemove)
            {
                Phases.Remove(phase);
            }

            var phases = Phases.Select(p => p.AnimationKey).ToArray();
            foreach (var animation in animations)
            {
                if (phases.Contains(animation))
                    continue;
                Phases.Add(new CutscenePhase()
                {
                    AnimationKey = animation,
                });
            }

            var listEventsRemove = new List<EventToAudioData>();
            var events = SkeletonDataAsset.GetSkeletonData(false).Events.ToArray().Select(a => a.Name)
                .ToHashSet();
            var listCheck = new List<string>();
            foreach (var ev in EventToAudioDatas)
            {
                if (listCheck.Contains(ev.EventName))
                {
                    listEventsRemove.Add(ev);
                    continue;
                }

                listCheck.Add(ev.EventName);
                if (events.Contains(ev.EventName))
                    continue;
                listEventsRemove.Add(ev);
            }

            foreach (var ev in listEventsRemove)
            {
                EventToAudioDatas.Remove(ev);
            }


            var eventsData = EventToAudioDatas.Select(p => p.EventName).ToArray();
            foreach (var ev in events)
            {
                if (eventsData.Contains(ev))
                    continue;
                EventToAudioDatas.Add(new EventToAudioData()
                {
                    EventName = ev,
                });
            }
        }

        private void OnValidate()
        {
            foreach (var scenePhase in Phases)
                scenePhase.InjectSkeleton(SkeletonDataAsset);
            foreach (var eventData in EventToAudioDatas)
                eventData.InjectSkeleton(SkeletonDataAsset);
        }
#endif
    }
}
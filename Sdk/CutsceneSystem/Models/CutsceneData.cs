using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vortex.Sdk.CutsceneSystem.Models
{
    /// <summary>
    /// Описание катсцены: скелет, последовательность фаз, звуки событий анимации и музыка. Тяжёлые ассеты (скелет,
    /// эмбиенты, музыка) лежат за <see cref="AssetHandle{T}"/> — сам ассет описания лёгкий; звуки событий — ссылки
    /// на записи общей базы звуков. Ключ катсцены в <c>CutsceneCatalog</c> — имя ассета
    /// </summary>
    [CreateAssetMenu(fileName = "Cutscene", menuName = "Vortex/Cutscenes/Cutscene Data")]
    public class CutsceneData : ScriptableObject
    {
        [SerializeReference] private AssetHandle<SkeletonDataAsset> skeleton;

        /// <summary>
        /// Скелет катсцены, один на все фазы
        /// </summary>
        public AssetHandle<SkeletonDataAsset> Skeleton => skeleton;

        [field: SerializeField] public List<CutscenePhase> Phases { get; internal set; } = new();

        [field: SerializeField] public List<EventToAudioData> EventToAudioDatas { get; internal set; } = new();

        [SerializeField] private CutsceneSound music = new();

        /// <summary>
        /// Музыка катсцены
        /// </summary>
        public CutsceneSound Music => music;

#if UNITY_EDITOR
        private SkeletonDataAsset _editorSkeleton;
        private bool _editorSkeletonResolved;

        /// <summary>
        /// Скелет в режиме редактирования — без загрузки через хэндл. Прямая ссылка отдаётся как есть; у ленивой
        /// реализации ассет берётся по GUID её <c>AssetReference</c> (поле <c>reference</c>). Результат
        /// кешируется до следующей правки ассета
        /// </summary>
        private SkeletonDataAsset GetEditorSkeleton()
        {
            if (_editorSkeletonResolved)
                return _editorSkeleton;

            _editorSkeletonResolved = true;
            _editorSkeleton = ResolveEditorSkeleton();
            return _editorSkeleton;
        }

        private SkeletonDataAsset ResolveEditorSkeleton()
        {
            if (skeleton == null)
                return null;
            if (skeleton.Asset != null)
                return skeleton.Asset;

            using var serialized = new SerializedObject(this);
            var guid = serialized.FindProperty(nameof(skeleton))
                .FindPropertyRelative("reference.m_AssetGUID")?.stringValue;
            return string.IsNullOrEmpty(guid)
                ? null
                : AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }

        [ContextMenu("Sync Spine Phases")]
        [GUIColor("#88ff88"), Button(ButtonSizes.Large), PropertyOrder(-100)]
        private void SyncWithSpine()
        {
            var skeletonData = GetEditorSkeleton().GetSkeletonData(false);

            var animations = skeletonData.Animations.ToArray().Select(a => a.Name).ToHashSet();
            Phases.RemoveAll(phase => !animations.Contains(phase.AnimationKey));

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

            var events = skeletonData.Events.ToArray().Select(a => a.Name).ToHashSet();
            var listEventsRemove = new List<EventToAudioData>();
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

            EditorUtility.SetDirty(this);
            OnValidate();
        }

        private void OnValidate()
        {
            //Сам скелет здесь не ищем: выпадашки спросят его, когда их откроют
            _editorSkeletonResolved = false;
            foreach (var scenePhase in Phases)
                scenePhase.InjectSkeleton(GetEditorSkeleton);
            foreach (var eventData in EventToAudioDatas)
                eventData.InjectSkeleton(GetEditorSkeleton);
        }
#endif
    }
}

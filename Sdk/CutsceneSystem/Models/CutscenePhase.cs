using System;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;

namespace Vortex.Sdk.CutsceneSystem.Models
{
    /// <summary>
    /// Фаза катсцены: какая анимация играет и что звучит фоном
    /// </summary>
    [Serializable]
    public class CutscenePhase
    {
        /// <summary>
        /// Что делать с эмбиентом прошлой фазы, если у этой фазы эмбиент не задан
        /// </summary>
        public enum EmptyAmbientMode
        {
            /// <summary>
            /// Эмбиент прошлой фазы продолжает звучать
            /// </summary>
            Inherit,

            /// <summary>
            /// Эмбиент прошлой фазы выключается
            /// </summary>
            Stop
        }

        /// <summary>
        /// Ключ анимации фазы в скелете катсцены
        /// </summary>
        [field: SerializeField, ValueDropdown("GetAnimations")]
        public string AnimationKey { get; internal set; } = string.Empty;

        /// <summary>
        /// Зафиксированный луп: фаза крутится до команды перехода. Без галочки фаза сквозная — отыграв один проход,
        /// сама переключается на следующую
        /// </summary>
        [field: SerializeField]
        public bool AnimationLooped { get; internal set; } = false;

        /// <summary>
        /// Эмбиент фазы
        /// </summary>
        [field: SerializeField]
        public CutsceneSound Ambient { get; internal set; } = new();

        /// <summary>
        /// Поведение при пустом <see cref="Ambient"/>: наследовать эмбиент прошлой фазы или выключить его
        /// </summary>
        [field: SerializeField, ShowIf("@Ambient == null || Ambient.IsEmpty")]
        public EmptyAmbientMode EmptyAmbient { get; internal set; } = EmptyAmbientMode.Inherit;

#if UNITY_EDITOR

        private Func<SkeletonDataAsset> _skeleton;

        /// <summary>
        /// Источник скелета катсцены для выпадашки анимаций
        /// </summary>
        public void InjectSkeleton(Func<SkeletonDataAsset> skeleton)
        {
            _skeleton = skeleton;
        }

        private string[] GetAnimations()
        {
            var data = _skeleton?.Invoke();
            return data == null
                ? Array.Empty<string>()
                : data.GetAnimationStateData().SkeletonData.Animations.Select(a => a.Name).ToArray();
        }
#endif
    }
}

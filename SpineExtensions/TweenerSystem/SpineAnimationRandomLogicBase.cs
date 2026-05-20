using System;
using System.Linq;
using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem;
using Random = UnityEngine.Random;

namespace Vortex.SpineExtensions.TweenerSystem
{
    /// <summary>
    /// Базовый класс взвешенной случайной анимации Spine — общий для SkeletonGraphic (UGUI) и
    /// SkeletonAnimation (MeshRenderer). Конкретные подклассы отличаются только типом поля skeleton;
    /// все источники данных идут через интерфейсы Spine (<see cref="IAnimationStateComponent"/>,
    /// <see cref="IHasSkeletonDataAsset"/>).
    ///
    /// Логика работает по бинарному принципу жесткого переключателя.
    /// Если передается 0, то выставляется animationIdle0
    /// Если передается 1 - animationIdle1
    /// Если любое промежуточное значение - animationFrw или animationBack в зависимости от _isForward
    ///
    /// Анимации задаются списками <see cref="SpineAnimationVariant"/> с весами — конкретная анимация
    /// выбирается случайным образом с вероятностью, пропорциональной весу.
    /// </summary>
    public abstract class SpineAnimationRandomLogicBase<TSkeleton> : TweenLogic
        where TSkeleton : MonoBehaviour, IAnimationStateComponent, IHasSkeletonDataAsset
    {
        [SerializeField, OnValueChanged("UpdateSkeleton"),
         ClassFilter(typeof(IAnimationStateComponent), typeof(IHasSkeletonDataAsset))]
        protected TSkeleton skeleton;

        [SerializeField, Range(0, 10)] private byte animationChannel = 1;

        [SerializeField, OnValueChanged("UpdatePercents", true)] [InfoBox("Анимация в положении Back")]
        private SpineAnimationVariant[] animationsIdle0 = new SpineAnimationVariant[0];

        [SerializeField, OnValueChanged("UpdatePercents", true)] [InfoBox("Анимация в положении Forward")]
        private SpineAnimationVariant[] animationsIdle1 = new SpineAnimationVariant[0];

        [SerializeField, OnValueChanged("UpdatePercents", true)] [InfoBox("Анимация перехода в Forward")]
        private SpineAnimationVariant[] animationsFrw = new SpineAnimationVariant[0];

        [SerializeField, OnValueChanged("UpdatePercents", true)] [InfoBox("Анимация перехода в Back")]
        private SpineAnimationVariant[] animationsBack = new SpineAnimationVariant[0];

        [SerializeField,
         InfoBox(
             "Играть анимацию только на совпадающей исходной анимации. Если не совпадает - не вызывать изменений")]
        private bool skipIfNotEqual;

        private bool _isForwardState;
        private bool _isRunningState;

        /// <summary>
        /// Проверка на условие запуска анимации
        /// </summary>
        private bool CheckStartAnimationForDirection(bool forwardDirection)
        {
            if (forwardDirection)
            {
                if (animationsFrw == null || animationsFrw.Length == 0)
                    return false;
            }
            else
            {
                if (animationsBack == null || animationsBack.Length == 0)
                    return false;
            }

            if (!skipIfNotEqual)
                return true;

            if (forwardDirection)
                return animationsIdle0.Any(v =>
                    skeleton.AnimationState.Tracks.Any(t => t?.Animation?.Name == v.Name));

            return animationsIdle1.Any(v =>
                skeleton.AnimationState.Tracks.Any(t => t?.Animation?.Name == v.Name));
        }

        /// <summary>
        /// Взвешенный случайный выбор анимации.
        /// </summary>
        private string GetAnimation(SpineAnimationVariant[] variants)
        {
            if (variants == null || variants.Length == 0)
                return null;

            var sum = 0;
            foreach (var v in variants) sum += v.Weight;
            if (sum <= 0)
                return null;

            var roll = Random.Range(0, sum);
            var acc = 0;
            foreach (var v in variants)
            {
                acc += v.Weight;
                if (roll < acc) return v.Name;
            }

            return variants[^1].Name;
        }

        protected override void SetValue(float value)
        {
            if (value == 0f) _isForwardState = false;

            if (value >= 1f) _isForwardState = true;

            if (value is <= 0 or >= 1f || _isRunningState) return;

            if (!_isForwardState && CheckStartAnimationForDirection(true))
            {
                var animationFrw = GetAnimation(animationsFrw);
                if (animationFrw == null)
                    return;
                CancelIdleReroll();
                skeleton.AnimationState.SetAnimation(animationChannel, animationFrw, false);
                var animationIdle1 = GetAnimation(animationsIdle1);
                if (!animationIdle1.IsNullOrWhitespace())
                {
                    skeleton.AnimationState.AddAnimation(animationChannel, animationIdle1, true, 0);
                    ScheduleIdleReroll(animationsIdle1, animationIdle1,
                        GetAnimationDuration(animationFrw));
                }
                else
                    skeleton.AnimationState.AddEmptyAnimation(animationChannel, 0, 0);

                _isRunningState = true;
                _isForwardState = !_isForwardState;
            }
            else if (_isForwardState && CheckStartAnimationForDirection(false))
            {
                var animationBack = GetAnimation(animationsBack);
                if (animationBack == null)
                    return;
                CancelIdleReroll();
                skeleton.AnimationState.SetAnimation(animationChannel, animationBack, false);
                var animationIdle0 = GetAnimation(animationsIdle0);
                if (!animationIdle0.IsNullOrWhitespace())
                {
                    skeleton.AnimationState.AddAnimation(animationChannel, animationIdle0, true, 0);
                    ScheduleIdleReroll(animationsIdle0, animationIdle0,
                        GetAnimationDuration(animationBack));
                }
                else
                    skeleton.AnimationState.AddEmptyAnimation(animationChannel, 0, 0);

                _isRunningState = true;
                _isForwardState = !_isForwardState;
            }
        }

        protected override void SwitchOn() => skeleton.gameObject.SetActive(true);

        protected override void SwitchOff() => skeleton.gameObject.SetActive(false);

        protected override void OnStart()
        {
            if (_isRunningState)
            {
                _isRunningState = false;
                return;
            }

            CancelIdleReroll();
            var animationIdle0 = GetAnimation(animationsIdle0);
            if (!animationIdle0.IsNullOrWhitespace())
            {
                skeleton.AnimationState.SetAnimation(animationChannel, animationIdle0, true);
                ScheduleIdleReroll(animationsIdle0, animationIdle0, 0f);
            }
            else
                skeleton.AnimationState.SetEmptyAnimation(animationChannel, 0);
        }

        protected override void OnEnd()
        {
            if (_isRunningState)
            {
                _isRunningState = false;
                return;
            }

            CancelIdleReroll();
            var animationIdle1 = GetAnimation(animationsIdle1);
            if (!animationIdle1.IsNullOrWhitespace())
            {
                skeleton.AnimationState.SetAnimation(animationChannel, animationIdle1, true);
                ScheduleIdleReroll(animationsIdle1, animationIdle1, 0f);
            }
            else
                skeleton.AnimationState.SetEmptyAnimation(animationChannel, 0);
        }

        /// <summary>
        /// Планирует пересчёт случайной idle-анимации по длительности текущей.
        /// Если в блоке только один вариант (или ноль) — TimeController не запускается.
        /// </summary>
        private void ScheduleIdleReroll(SpineAnimationVariant[] block, string currentIdle, float initialDelay)
        {
            if (block == null || block.Length <= 1) return;
            var dur = GetAnimationDuration(currentIdle);
            if (dur <= 0f) return;
            TimeController.Call(() => RerollIdle(block), initialDelay + dur, this);
        }

        /// <summary>
        /// Колбэк TimeController: выбирает новую случайную idle-анимацию из блока и перепланирует следующий пересчёт.
        /// </summary>
        private void RerollIdle(SpineAnimationVariant[] block)
        {
            if (skeleton == null) return;
            var nextIdle = GetAnimation(block);
            if (string.IsNullOrEmpty(nextIdle)) return;
            skeleton.AnimationState.SetAnimation(animationChannel, nextIdle, true);
            var dur = GetAnimationDuration(nextIdle);
            if (dur <= 0f) return;
            TimeController.Call(() => RerollIdle(block), dur, this);
        }

        /// <summary>
        /// Отменяет запланированный пересчёт idle-анимации.
        /// </summary>
        private void CancelIdleReroll() => TimeController.RemoveCall(this);

        /// <summary>
        /// Длительность Spine-анимации по имени. 0 — если не найдена или скелет не готов.
        /// </summary>
        private float GetAnimationDuration(string animName)
        {
            if (string.IsNullOrEmpty(animName) || skeleton == null || skeleton.SkeletonDataAsset == null)
                return 0f;
            var data = skeleton.SkeletonDataAsset.GetSkeletonData(true);
            var anim = data?.FindAnimation(animName);
            return anim?.Duration ?? 0f;
        }

#if UNITY_EDITOR

        [NonSerialized] private bool _inspectorInited;

        /// <summary>
        /// Лениво заполняет <see cref="SpineAnimationVariant.List"/> на первом рисовании инспектора.
        /// Аналог <c>[OnInspectorInit]</c> — нужен, потому что <c>OnInspectorInit</c> через state-updater
        /// биндится к свойству родительской коллекции (<c>TweenLogic[]</c>) и не разрешает методы
        /// конкретного подкласса. <c>[OnInspectorGUI]</c> зовётся уже в контексте этого инстанса.
        /// </summary>
        [OnInspectorGUI, PropertyOrder(int.MinValue)]
        private void EditorInit()
        {
            if (_inspectorInited) return;
            _inspectorInited = true;
            UpdateSkeleton();
        }

        /// <summary>
        /// Пересчёт долей вероятности у всех массивов вариантов для инспектора.
        /// Вызывается Odin'ом через <see cref="OnValueChangedAttribute"/>.
        /// </summary>
        private void UpdatePercents()
        {
            RecalcPercents(animationsIdle0);
            RecalcPercents(animationsIdle1);
            RecalcPercents(animationsFrw);
            RecalcPercents(animationsBack);
            UpdateSkeleton();
        }

        private void UpdateSkeleton()
        {
            var list = GetListAnimations();
            foreach (var v in animationsIdle0) v.List = list;
            foreach (var v in animationsIdle1) v.List = list;
            foreach (var v in animationsFrw) v.List = list;
            foreach (var v in animationsBack) v.List = list;
        }

        private static void RecalcPercents(SpineAnimationVariant[] variants)
        {
            if (variants == null || variants.Length == 0)
                return;
            var sum = variants.Sum(v => v.Weight);
            if (sum <= 0)
            {
                foreach (var v in variants) v._percent = 0;
                return;
            }

            foreach (var v in variants)
                v._percent = (float)v.Weight / sum;
        }

        private string[] GetListAnimations()
        {
            var data = skeleton != null && skeleton.SkeletonDataAsset != null
                ? skeleton.SkeletonDataAsset.GetSkeletonData(true)
                : null;
            return data?.Animations.Select(a => a.Name).ToArray() ?? Array.Empty<string>();
        }
#endif
    }

    /// <summary>
    /// Вариант анимации с весом для взвешенного случайного выбора.
    /// Вынесен из generic-базы наружу, чтобы все закрытые типы базы делили один и тот же тип элемента.
    /// </summary>
    [Serializable, HideReferenceObjectPicker, ClassLabel("$Label")]
    internal class SpineAnimationVariant
    {
        [SerializeField, HideLabel, HorizontalGroup, Range(0, 100)]
        private int weight = 50;

        public int Weight => weight;

        [SerializeField, ValueSelector("GetListAnimations"), HideLabel]
        private string name;

        public string Name => name;

#if UNITY_EDITOR
        private string[] GetListAnimations() => List;

        internal string[] List;

        internal float _percent;

        [ShowInInspector, HideLabel, HorizontalGroup]
        private string Percent => Mathf.Round(_percent * 100) + "%";

        private string Label() => $"{(string.IsNullOrEmpty(name) ? "[NOT SET]" : name)} {Percent}";
#endif
    }
}
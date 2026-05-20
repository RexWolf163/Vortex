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
    /// Запускает анимацию спайна.
    /// Если активен флаг, то запускает анимацию только при активной указанной анимации.
    ///
    /// Логика работает по бинарному принципу жесткого переключателя.
    /// Если передается 0, то выставляется animationIdle0
    /// Если передается 1 - animationIdle1
    /// Если любое промежуточное значение - animationFrw или animationBack в зависимости от _isForward
    ///
    /// Если анимация не указана, не воздействует на spine
    ///
    /// Анимации задаются списками <see cref="AnimationVariant"/> с весами — конкретная анимация
    /// выбирается случайным образом с вероятностью, пропорциональной весу.
    /// </summary>
    [Serializable]
    public class SpineAnimationRandomLogic : TweenLogic
    {
        [SerializeField] private SkeletonGraphic skeleton;

        [SerializeField, Range(0, 10)] private byte animationChannel = 1;

        [SerializeField, OnValueChanged("UpdatePercents", true)] [InfoBox("Анимация в положении Back")]
        private AnimationVariant[] animationsIdle0 = Array.Empty<AnimationVariant>();

        [SerializeField, OnValueChanged("UpdatePercents", true)] [InfoBox("Анимация в положении Forward")]
        private AnimationVariant[] animationsIdle1 = Array.Empty<AnimationVariant>();

        [SerializeField, OnValueChanged("UpdatePercents", true)] [InfoBox("Анимация перехода в Forward")]
        private AnimationVariant[] animationsFrw = Array.Empty<AnimationVariant>();

        [SerializeField, OnValueChanged("UpdatePercents", true)] [InfoBox("Анимация перехода в Back")]
        private AnimationVariant[] animationsBack = Array.Empty<AnimationVariant>();

        [SerializeField,
         InfoBox(
             "Играть анимацию только на совпадающей исходной анимации. Если не совпадает - не вызывать изменений")]
        private bool skipIfNotEqual;

        /// <summary>
        /// Проверка на условие запуска анимации
        /// </summary>
        /// <param name="forwardDirection"></param>
        /// <returns></returns>
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
        private string GetAnimation(AnimationVariant[] variants)
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

        private bool _isForwardState;
        private bool _isRunningState;

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
        /// <param name="block">Блок вариантов (используется как owner отложенного вызова).</param>
        /// <param name="currentIdle">Имя текущей запущенной idle-анимации.</param>
        /// <param name="initialDelay">Дополнительная задержка перед началом цикла (например, длительность Frw/Back-перехода).</param>
        private void ScheduleIdleReroll(AnimationVariant[] block, string currentIdle, float initialDelay)
        {
            if (block == null || block.Length <= 1) return;
            var dur = GetAnimationDuration(currentIdle);
            if (dur <= 0f) return;
            TimeController.Call(() => RerollIdle(block), initialDelay + dur, block);
        }

        /// <summary>
        /// Колбэк TimeController: выбирает новую случайную idle-анимацию из блока и перепланирует следующий пересчёт.
        /// </summary>
        private void RerollIdle(AnimationVariant[] block)
        {
            if (skeleton == null) return;
            var nextIdle = GetAnimation(block);
            if (string.IsNullOrEmpty(nextIdle)) return;
            skeleton.AnimationState.SetAnimation(animationChannel, nextIdle, true);
            var dur = GetAnimationDuration(nextIdle);
            if (dur <= 0f) return;
            TimeController.Call(() => RerollIdle(block), dur, block);
        }

        /// <summary>
        /// Отменяет запланированные пересчёты для обоих idle-блоков.
        /// </summary>
        private void CancelIdleReroll()
        {
            if (animationsIdle0 != null) TimeController.RemoveCall(animationsIdle0);
            if (animationsIdle1 != null) TimeController.RemoveCall(animationsIdle1);
        }

        /// <summary>
        /// Длительность Spine-анимации по имени. 0 — если не найдена или скелет не готов.
        /// </summary>
        private float GetAnimationDuration(string animName)
        {
            if (string.IsNullOrEmpty(animName) || skeleton == null || skeleton.SkeletonData == null)
                return 0f;
            var anim = skeleton.SkeletonData.FindAnimation(animName);
            return anim?.Duration ?? 0f;
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
        }

        private static void RecalcPercents(AnimationVariant[] variants)
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

#if UNITY_EDITOR
        private string[] GetListAnimations()
        {
            return skeleton?.SkeletonData.Animations.Select(a => a.Name).ToArray() ?? Array.Empty<string>();
        }
#endif

        /// <summary>
        /// Вариант анимации с весом для взвешенного случайного выбора.
        /// </summary>
        [Serializable, HideReferenceObjectPicker, ClassLabel("$Label")]
        private class AnimationVariant
        {
            [SerializeField, HideLabel, HorizontalGroup, Range(1, 100)]
            private int weight = 1;

            public int Weight => weight;

            internal float _percent;

            [ShowInInspector, HideLabel, HorizontalGroup]
            private string Percent => Mathf.Round(_percent * 100) + "%";

            [SerializeField, ValueSelector("GetListAnimations"), HideLabel]
            private string name;

            public string Name => name;

#if UNITY_EDITOR
            private string Label() => $"{(string.IsNullOrEmpty(name) ? "[NOT SET]" : name)} {Percent}";
#endif
        }
    }
}

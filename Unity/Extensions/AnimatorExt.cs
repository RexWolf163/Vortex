using System.Collections.Generic;
using UnityEngine;

namespace Vortex.Unity.Extensions
{
    public static class AnimatorExt
    {
        /// <summary>
        /// Возвращает список параметров аниматора
        /// </summary>
        /// <param name="animator"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static string[] GetAnimatorParameters<T>(this Animator animator)
        {
            var type = AnimatorControllerParameterType.Bool;

            if (typeof(T) == typeof(int))
                type = AnimatorControllerParameterType.Int;
            else if (typeof(T) == typeof(float))
                type = AnimatorControllerParameterType.Float;
            else if (typeof(T) == typeof(bool))
                type = AnimatorControllerParameterType.Bool;

            var result = new List<string>();
            if (animator == null) return result.ToArray();

            var rac = animator.runtimeAnimatorController;
            while (rac is AnimatorOverrideController aoc)
                rac = aoc.runtimeAnimatorController;

            if (rac is not UnityEditor.Animations.AnimatorController ac) return result.ToArray();

            foreach (var param in ac.parameters)
                if (param.type == type)
                    result.Add(param.name);

            return result.ToArray();
        }

        /// <summary>
        /// Возвращает список триггеров аниматора
        /// </summary>
        /// <param name="animator"></param>
        /// <returns></returns>
        public static string[] GetAnimatorTriggers(this Animator animator)
        {
            var result = new List<string>();
            if (animator == null) return result.ToArray();

            var rac = animator.runtimeAnimatorController;
            while (rac is AnimatorOverrideController aoc)
                rac = aoc.runtimeAnimatorController;

            if (rac is not UnityEditor.Animations.AnimatorController ac) return result.ToArray();

            foreach (var param in ac.parameters)
                if (param.type == AnimatorControllerParameterType.Trigger)
                    result.Add(param.name);

            return result.ToArray();
        }
    }
}
using System;
using Vortex.Sdk.CharacterViewSystem.Abstractions;
using Vortex.Sdk.CharacterViewSystem.Models;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Sdk.CharacterViewSystem.Presets
{
    /// <summary>
    /// Базовый пресет персонажа. Содержит общие доменные параметры,
    /// не зависящие от представления и контекста спауна.
    /// </summary>
    [Serializable]
    public abstract class CharacterPreset<TModel> : RecordPreset<TModel>
        where TModel : BaseCharacterModel<TModel>, new()
    {
        [SerializeField] private float speed;

        /// <summary>
        /// Максимальная скорость движения персонажа.
        /// Опорный параметр. Реальная скорость на View регулируется коэффициентами View.
        /// </summary>
        public float Speed => speed;

        [SerializeReference, HideReferenceObjectPicker]
        private CharacterBehavior[] behaviors = new CharacterBehavior[0];

        /// <summary>
        /// Глубокая копия набора моделей поведения.
        /// При обнаружении null-элемента — фатальная ошибка с App.Exit().
        /// </summary>
        public CharacterBehavior[] Behaviors
        {
            get
            {
                var result = new CharacterBehavior[behaviors.Length];
                for (var i = 0; i < behaviors.Length; i++)
                {
                    var beh = behaviors[i];
                    if (beh == null)
                    {
                        Debug.LogError($"[{GetType().Name}] {name}: behaviors[{i}] не реализует CharacterBehavior.");
                        App.Exit();
                        return Array.Empty<CharacterBehavior>();
                    }

                    result[i] = beh.DeepCopy();
                }

                return result;
            }
        }
    }
}
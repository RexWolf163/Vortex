using System;
using Vortex.Sdk.CharacterViewSystem.Abstractions;
using Vortex.Sdk.CharacterViewSystem.Models;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Sdk.CharacterViewSystem.Presets
{
    [CreateAssetMenu(fileName = "PcPreset", menuName = "Database/PcPreset")]
    [Serializable]
    public partial class PcPreset : RecordPreset<PlayableCharacter>
    {
        [SerializeField] private float speed;

        /// <summary>
        /// Максимальная скорость движения персонажа.
        /// Это базовый опорный параметр. Реальная скорость на View должна регулироваться коэффициентами View,
        /// опирающимися на этот параметр
        /// </summary>
        public float Speed => speed;

        [SerializeReference, HideReferenceObjectPicker]
        private CharacterBehavior[] behaviors = new CharacterBehavior[0];

        public ICharacterBehavior[] Behaviors
        {
            get
            {
                var ar = new ICharacterBehavior[behaviors.Length];
                var c = ar.Length;
                for (int i = 0; i < c; i++)
                {
                    var beh = behaviors[i] as ICharacterBehavior;
                    if (beh == null)
                    {
                        Debug.LogError($"[PcPreset] {name}: behaviors[{i}] не реализует ICharacterBehavior.");
                        App.Exit();
                        return Array.Empty<ICharacterBehavior>();
                    }
                    ar[i] = beh.DeepCopy();
                }

                return ar;
            }
        }


#if UNITY_EDITOR
        private void OnValidate() => type = RecordTypes.Singleton;
#endif
    }
}
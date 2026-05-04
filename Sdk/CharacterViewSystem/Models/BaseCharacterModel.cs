using Vortex.Sdk.CharacterViewSystem.Abstractions;
using Vortex.Sdk.CharacterViewSystem.Handlers;
using Vortex.Sdk.CharacterViewSystem.Models.States;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.ExtensibleEnumSystem.Abstractions;
using Vortex.Core.ExtensibleEnumSystem.Extensions;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.MapLevels.Abstractions;
using Vortex.Unity.Extensions.ReactiveValues;

namespace Vortex.Sdk.CharacterViewSystem.Models
{
    /// <summary>
    /// Класс модели данных персонажа, представленного на карте
    /// </summary>
    public abstract partial class BaseCharacterModel<T> : Record, IMapObject where T : BaseCharacterModel<T>
    {
        public Vector2Data Position { get; internal set; }

        #region PresetValues

        /// <summary>
        /// Максимальная скорость движения персонажа.
        /// Это базовый опорный параметр. Реальная скорость на View должна регулироваться коэффициентами View,
        /// опирающимися на этот параметр
        /// </summary>
        public float Speed { get; internal set; }

        /// <summary>
        /// Набор моделей поведения персонажа (В основном для NPC, но можно и команды для PC указать)
        /// </summary>
        public CharacterBehavior Behaviors { get; internal set; }

        #endregion

        #region DynamicStats

        /// <summary>
        /// Целевой объект персонажа
        /// </summary>
        public MapObjectData Focus { get; internal set; }

        /// <summary>
        /// Режим движения персонажа
        /// </summary>
        public ExtEnumData<MoveState> MoveMode { get; internal set; } = new(MoveState.Stay);

        /// <summary>
        /// Способ движения персонажа
        /// </summary>
        public ExtEnumData<MoveSubStateDirection> MoveDirectionMode { get; internal set; } =
            new(MoveSubStateDirection.Forward);

        /// <summary>
        /// Направление взгляда персонажа
        /// </summary>
        public ExtEnumData<DirectionState> DirectionMode { get; internal set; } = new(DirectionState.East);

        #endregion

        public override string GetDataForSave() => this.SerializeProperties();

        public override void LoadFromSaveData(string data)
        {
            var temp = data.DeserializeProperties<T>();
            this.CopyFrom(temp);
        }

        internal virtual void SetOwner(object key)
        {
            Focus.SetOwner(key);
            MoveMode.SetOwner(key);
            MoveDirectionMode.SetOwner(key);
            DirectionMode.SetOwner(key);
        }
    }
}
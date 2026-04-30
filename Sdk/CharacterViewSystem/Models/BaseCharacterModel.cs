using AppScripts.CharacterViewSystem.Abstractions;
using AppScripts.CharacterViewSystem.Handlers;
using AppScripts.CharacterViewSystem.Models.States;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.Extensions.LogicExtensions.SerializationSystem;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Sdk.MapLevels.Abstractions;
using Vortex.Unity.Extensions.ReactiveValues;

namespace AppScripts.CharacterViewSystem.Models
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
        public ICharacterBehavior Behaviors { get; internal set; }

        #endregion

        /// <summary>
        /// Целевой объект персонажа
        /// </summary>
        public MapObjectData Focus { get; internal set; }

        /// <summary>
        /// Режим движения персонажа
        /// </summary>
        public EnumData<MoveState> MoveMode { get; internal set; } = new(0);

        /// <summary>
        /// Способ движения персонажа
        /// </summary>
        public EnumData<MoveSubStateDirection> MoveDirectionMode { get; internal set; } = new(0);

        /// <summary>
        /// Направление взгляда персонажа
        /// </summary>
        public EnumData<DirectionState> DirectionMode { get; internal set; } = new(0);

        public override string GetDataForSave() => this.SerializeProperties();

        public override void LoadFromSaveData(string data)
        {
            var temp = data.DeserializeProperties<T>();
            this.CopyFrom(temp);
        }
    }
}
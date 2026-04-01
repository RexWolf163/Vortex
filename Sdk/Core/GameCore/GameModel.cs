using System;
using System.Collections.Generic;
using UnityEngine;
using Vortex.Core.ComplexModelSystem;

namespace Vortex.Sdk.Core.GameCore
{
    /// <summary>
    /// Модель глобавльных данных для игры.
    /// </summary>
    public class GameModel : ComplexModel<GameModel.IGameData>
    {
        public GameStates State { get; internal set; }

        /// <summary>
        /// Интерфейс-маркер
        /// </summary>
        public interface IGameData
        {
        }

        private Dictionary<Type, IGameData> backup;

        protected override void BeforeSerialization()
        {
            //Ignore
        }

        protected override void BeforeDeserialization()
        {
            backup = Index;
        }

        protected override void AfterSerialization()
        {
            //Ignore
        }

        protected override void AfterDeserialization()
        {
            if (Index != null) return;
            Index = backup;
            Debug.LogError($"[GameModel] Ошибка при десериализации. Index восстановлен в исходное состояние");
        }
    }
}
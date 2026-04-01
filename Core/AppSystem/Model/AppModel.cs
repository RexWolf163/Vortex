using System;
using Vortex.Core.System.Enums;

namespace Vortex.Core.AppSystem.Model
{
    /// <summary>
    /// Модель данных приложения
    /// </summary>
    public sealed partial class AppModel
    {
        /// <summary>
        /// Состояние приложения
        /// </summary>
        internal AppStates _state;

        /// <summary>
        /// отметка времени начала работы приложения
        /// </summary>
        internal DateTime _startTime;

        internal AppModel()
        {
            _startTime = DateTime.UtcNow;
        }
    }
}
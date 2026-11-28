#if UNITY_EDITOR
using System;

namespace Vortex.Unity.Components.UiComponentDashboardSystem.Editor
{
    /// <summary>
    /// Тип управляющего скрипта и позиция части, в которую на одном GameObject пишут два и более скрипта этого типа.
    /// </summary>
    internal readonly struct DashboardPositionConflict
    {
        /// <summary>Тип управляющих скриптов, пишущих в одну часть.</summary>
        public Type ControlType { get; }

        /// <summary>Позиция части, в которую пишут скрипты.</summary>
        public int Position { get; }

        /// <summary>Создать запись совпадения позиций.</summary>
        /// <param name="controlType">Тип управляющих скриптов, пишущих в одну часть.</param>
        /// <param name="position">Позиция части, в которую пишут скрипты.</param>
        public DashboardPositionConflict(Type controlType, int position)
        {
            ControlType = controlType;
            Position = position;
        }
    }
}
#endif

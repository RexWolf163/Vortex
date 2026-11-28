#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Vortex.Unity.Components.UiComponentDashboardSystem.Editor
{
    /// <summary>
    /// Один управляющий скрипт GameObject группы с вариантом его вывода и номером среди скриптов того же типа
    /// на этом GameObject.
    /// </summary>
    internal sealed class DashboardControlEntry
    {
        /// <summary>Управляющий скрипт.</summary>
        public MonoBehaviour Control { get; }

        /// <summary>Вариант вывода скрипта в группе.</summary>
        public ControlEntryState State { get; }

        /// <summary>Номер скрипта среди скриптов того же типа на GameObject, начиная с 1.</summary>
        public int Ordinal { get; }

        /// <summary>Создать запись скрипта группы; номер меньше 1 — ошибка конфигурации (fast-fail).</summary>
        /// <param name="control">Управляющий скрипт.</param>
        /// <param name="state">Вариант вывода скрипта в группе.</param>
        /// <param name="ordinal">Номер среди скриптов того же типа на GameObject, начиная с 1.</param>
        public DashboardControlEntry(MonoBehaviour control, ControlEntryState state, int ordinal)
        {
            if (ordinal < 1)
            {
                Debug.LogError($"[{nameof(DashboardControlEntry)}] Номер скрипта должен быть не меньше 1, получено {ordinal}.");
                throw new ArgumentOutOfRangeException(nameof(ordinal));
            }

            Control = control;
            State = state;
            Ordinal = ordinal;
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections.Generic;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.Components.UiComponentDashboardSystem.Editor
{
    /// <summary>
    /// Снимок одного <see cref="UIComponent"/> индекса для панели дашборда: сам компонент, записи управляющих скриптов
    /// его GameObject, совпадения позиций между ними и путь от корня, отличающий одноимённые слои.
    /// Признак «динамическая» выводится из отсутствия записей.
    /// </summary>
    internal sealed class DashboardGroup
    {
        /// <summary>Компонент индекса, по GameObject которого собрана группа.</summary>
        public UIComponent Component { get; }

        /// <summary>Записи управляющих скриптов GameObject компонента, только чтение.</summary>
        public IReadOnlyList<DashboardControlEntry> Entries { get; }

        /// <summary>Совпадения позиций между управляющими скриптами одного типа, только чтение.</summary>
        public IReadOnlyList<DashboardPositionConflict> Conflicts { get; }

        /// <summary>Путь от корня дашборда, отличающий одноимённые слои. Пустая строка — имя слоя уникально.</summary>
        public string Path { get; }

        /// <summary>Группа без управляющих скриптов: компонент представляет данные, которыми управляют снаружи.</summary>
        public bool IsDynamic => Entries.Count == 0;

        /// <summary>
        /// Создать снимок группы. Коллекции копируются: последующие изменения переданных коллекций группу не затрагивают.
        /// </summary>
        /// <param name="component">Компонент индекса; <c>null</c> недопустим (ошибка программиста, не проверяется).</param>
        /// <param name="entries">Записи управляющих скриптов его GameObject.</param>
        /// <param name="conflicts">Совпадения позиций между скриптами одного типа.</param>
        /// <param name="path">
        /// Путь от корня; пустая строка, если имя слоя уникально. <c>null</c> недопустим (ошибка программиста, не проверяется).
        /// </param>
        public DashboardGroup(UIComponent component, IEnumerable<DashboardControlEntry> entries,
            IEnumerable<DashboardPositionConflict> conflicts, string path)
        {
            Component = component;
            Entries = new List<DashboardControlEntry>(entries);
            Conflicts = new List<DashboardPositionConflict>(conflicts);
            Path = path;
        }
    }
}
#endif

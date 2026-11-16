#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector;
using UnityEditor;
using Vortex.Core.DatabaseSystem;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Unity.StateGraphSystem.Presets;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.StateGraphSystem.Views
{
    /// <summary>
    /// Сверка состояний свитчера с узлами графа: списки недостающих и лишних состояний
    /// и кнопка Sync, которая добавляет недостающие (пустыми, в конец) и удаляет лишние
    /// </summary>
    public partial class StateGraphView
    {
        /// <summary>GUID записи графа (для подсветки в визуализаторе)</summary>
        internal string EditorGraphGuid => graph;

        [ShowInInspector, ReadOnly, ShowIf("@GetMissingStates().Length > 0"), PropertyOrder(20), HideInPlayMode]
        [ListDrawerSettings(IsReadOnly = true), LabelText("Missing states")]
        private string[] MissingStates => GetMissingStates();

        [ShowInInspector, ReadOnly, ShowIf("@GetExtraStates().Length > 0"), PropertyOrder(21), HideInPlayMode]
        [ListDrawerSettings(IsReadOnly = true), LabelText("Extra states")]
        private string[] ExtraStates => GetExtraStates();

        [Button("Sync switcher states"), PropertyOrder(22), HideInPlayMode]
        [ShowIf("@GetMissingStates().Length > 0 || GetExtraStates().Length > 0")]
        private void Sync()
        {
            var names = GetNodeNames();
            if (names == null || switcher == null)
                return;

            var states = switcher.States.Where(state => names.Contains(state.Name)).ToList();
            foreach (var name in names)
                if (states.All(state => state.Name != name))
                    states.Add(CreateState(name));

            Undo.RecordObject(switcher, "Sync StateGraph states");
            typeof(UIStateSwitcher).GetField("states", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(switcher, states.ToArray());
            EditorUtility.SetDirty(switcher);
            PrefabUtility.RecordPrefabInstancePropertyModifications(switcher);
        }

        private string[] GetMissingStates()
        {
            var names = GetNodeNames();
            if (names == null || switcher == null)
                return new string[0];
            return names.Where(name => switcher.GetState(name) < 0).ToArray();
        }

        private string[] GetExtraStates()
        {
            var names = GetNodeNames();
            if (names == null || switcher == null)
                return new string[0];
            return switcher.States.Select(state => state.Name).Where(name => !names.Contains(name)).ToArray();
        }

        /// <summary>Имена узлов графа в порядке узлов (без пустых и повторов); null — пресет не найден</summary>
        private List<string> GetNodeNames()
        {
            if (string.IsNullOrEmpty(graph))
                return null;
            if ((Database.GetDriver() as IDriverEditor)?.GetPresetForRecord(graph) is not StateGraphPreset preset)
                return null;

            var names = new List<string>();
            foreach (var node in preset.RawNodes)
                if (node != null && !string.IsNullOrEmpty(node.Name) && !names.Contains(node.Name))
                    names.Add(node.Name);
            return names;
        }

        private static UIStateSwitcher.StateData CreateState(string name)
        {
            var state = new UIStateSwitcher.StateData();
            typeof(UIStateSwitcher.StateData).GetField("name", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(state, name);
            return state;
        }
    }
}
#endif

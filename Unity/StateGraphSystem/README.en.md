# StateGraphSystem (Unity)

**Namespace:** `Vortex.Unity.StateGraphSystem.Presets`, `.Views`, `.Editor`
**Assemblies:** `ru.vortex.unity.stategraph`, `ru.vortex.unity.stategraph.editor` (Editor)

## Purpose

Unity tooling for the core state machine (`ru.vortex.stategraph`): a database graph preset, a display component and a visual editor.

- `StateGraphPreset` — preset of the `StateGraph` record (always `MultiInstance`), summary in the inspector
- `StateGraphView` — takes its own graph copy from the database and shows the current node as a `UIStateSwitcher` state
- Sync — reconciles switcher states with graph nodes
- UI Toolkit visualizer: structure editing, node layout, current node highlight in Play Mode

Out of scope: driving the machine (`Next` / `ForceNode` / `Stop`) is the consumer's job; core logic — see the core README.

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Core.StateGraphSystem` | model, controller, graph validation |
| `Vortex.Core.LogicChainsSystem` | `Condition` in transitions |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>`, `DbRecordAttribute` |
| `Vortex.Unity.UI` (misc) | `UIStateSwitcher` |
| `Vortex.Unity.EditorTools` | `ToolsSettings` — visualizer palette |
| Odin Inspector | preset inspector, visualizer side panel |

## Preset

`Database/State Graph` — `StateGraphPreset : RecordPreset<StateGraph>`. Stores structure only:

```
StateGraphPreset
├── startNode : string                         ← picked from nodes; red when unset/missing
└── nodes     : StateNodePreset[]              [SerializeReference]
    ├── id (hidden, created on add), name, nextMiss
    └── transitions : StateTransitionPreset[]  [SerializeReference]
        └── target (picked from nodes), waitNext, conditions : Condition[] [SerializeReference]
```

- The structure can be edited both in the inspector ("Structure" group) and in the visualizer.
- Empty elements (node, transition or condition without a type) do not reach the model and are reported as errors.
- **Summary** in the inspector: start node, node count (end nodes), auto transitions, `Next` transitions, conditions; graph validation issues (errors first). Recomputed after the preset changes.
- The **Open in visualizer** button opens the graph in the visualizer.

## StateGraphView

```csharp
var graph = view.GetData<StateGraph>();   // IDataStorage
graph.Next();
```

| Field | Description |
|---|---|
| `graph` | Graph record (`DbRecord`, `MultiInstance`) |
| `onEnableBehavior` | `Restart` (default) — every enable starts from the start node, conditions stop on disable; `Resume` — continues where it stopped, conditions keep running while the View is disabled |
| `switcher` | `UIStateSwitcher` whose states are named after the nodes |

Lifecycle:

- First `OnEnable` — waits for the database (`Database.OnInit`), takes a copy (`GetNewRecord`), raises `OnUpdateLink`. The copy lives until `OnDestroy`.
- `OnEnable` — subscribes to `CurrentNode`, `Activate()`, explicitly refreshes the display (the event may not come: restart on the same node, a node set by `ForceNode` while disabled).
- `OnDisable` — unsubscribes, `Deactivate(Resume ? true : false)`.
- `OnDestroy` — `Deactivate(false)`: in Resume the conditions kept running while disabled — they are finally shut down.

Display: current node → the switcher state with the same name. A machine not started yet shows the start node, a finished one keeps the last shown node. Missing state — error in the log, display unchanged.

### Sync

Outside Play Mode the View inspector shows **Missing states** (nodes without a state) and **Extra states** (states without a node). The **Sync switcher states** button adds missing states as empty ones at the end and removes extra ones (Undo). Switcher state indices shift after removal — `stateOnEnable` is not corrected.

## Visualizer

Open it with the preset inspector button or `Tools/Vortex/State Graph Visualizer` and pick the preset in the toolbar.

| Action | How |
|---|---|
| Pan | LMB on background or MMB |
| Zoom | Wheel |
| Fit graph | `Frame all` |
| Add node | `Add node` or RMB on background |
| Move node | Drag |
| New transition | Drag from the circle on the node's right to another node |
| Start node, delete node | RMB on a node |
| "On Next" toggle, delete transition | RMB on a transition or its label |
| Delete selection | Delete |

- All edits support Undo; deleting a node also deletes transitions to it.
- Side panel: the selected node or transition is edited with the Odin inspector (including condition type selection); below — the issue list, a click selects the node or transition.
- Notation: START / END tags; auto transitions — solid, "on `Next`" — dashed; transition label — order, `Next`, condition count; border — start / warning / error / selection.
- A transition to a missing node is not drawn: its error shows on the source node border, the transition itself is selected from the issue list.
- **Play Mode:** highlights the current node of the first selected `StateGraphView` of this graph, or, if none is selected, of the one selected before; the View name is shown in the toolbar.
- **Palette** — the `Graph*` colors in `ToolsSettings` (light and dark editor themes).

### Layout

Node positions live in a separate editor asset `StateGraphLayout` "{preset} Layout" next to the preset (the database preset stores structure only). It is linked to the preset by the preset's asset GUID: the preset renames its own file when its name changes, and the link survives. The asset is created on the first node move or add. Nodes without a saved position are laid out automatically: column — distance from the start node, unreachable ones — a separate column.

## Edge cases

| Situation | Behavior |
|---|---|
| View enabled before the database loads | Copy taken on `Database.OnInit`; repeated enable/disable does not duplicate the subscription |
| View disabled before the database loads | Copy taken, `Activate` on the next enable |
| Graph record not found | `Database` error, the View shows nothing |
| Graph with errors | Core errors logged; the View shows the start node if it exists |
| Switcher lacks the node's state | Error logged, display unchanged |
| Sync in Play Mode | Hidden |
| No layout | Automatic layout; the asset is created on the first position edit |

# StateGraphSystem (Core)

**Namespace:** `Vortex.Core.StateGraphSystem.Model`, `.Controllers`, `.Validation`
**Assembly:** `ru.vortex.stategraph`
**Platform:** .NET Standard 2.1+

## Purpose

A general-purpose finite state machine: a graph of nodes and transitions with one observable variable — the current node. The next node is chosen when transition conditions are met (`Condition` from `LogicChainsSystem`) or by the external `Next` signal. The machine does not know what nodes mean — the consumer gives them meaning (cutscene display, UI steps, battle phases, etc.).

- The graph is a `MultiInstance` database record: every consumer takes its own copy
- Two kinds of transitions: auto (fires by itself once conditions are met) and "on `Next`"
- Transition priority is their order in the node; a transition's conditions are AND-ed
- The end is a node without transitions; `OnFinished` event
- Forced transition `ForceNode` (by Id or name) and forced completion `Stop`
- Instance activity: an inactive machine ignores signals; modes with and without state retention
- Graph structure validation shared by runtime and editor

Out of scope:

- Display, presets, visual editor — Unity layer (`ru.vortex.unity.stategraph`)
- The source of the `Next` signal — supplied by the consumer (button, domain bus, script)
- Persisting state to saves (`GetDataForSave()` → `null`)

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Core.DatabaseSystem` | `Record`, graph copy via `Database.GetNewRecord` |
| `Vortex.Core.LogicChainsSystem` | `Condition` — transition conditions |
| `Vortex.Core.Extensions` | `StringData` (owner-lock), `Crypto.GetNewGuid()` |
| `Vortex.Core.LoggerSystem` | graph validation errors, `Next` misses |
| `Vortex.Core.System` | `SystemModel` |

## Usage

```csharp
var graph = Database.GetNewRecord<StateGraph>("graph-guid");   // own copy
graph.CurrentNode.OnUpdate += id => Show(graph.GetNode(id)?.Name);
graph.OnFinished += Close;

graph.Activate();      // first time — start from the start node
graph.Next();          // "next" signal
graph.ForceNode("Final");                     // by name or Id
graph.Deactivate(retainState: true);          // pause keeping the state
graph.Stop();          // forced completion
```

In Unity the copy is usually held by `StateGraphView`, which exposes it via `IDataStorage` (see the Unity package).

## Model

```
StateGraph : Record                       (MultiInstance)
├── Nodes        : StateNode[]
├── StartNode    : string                 ← start node Id
├── CurrentNode  : StringData             ← current node Id; empty — not started / finished (owner-lock)
├── InstanceId   : string                 ← owner passed to conditions (Condition.Init)
├── IsActive     : bool
└── OnFinished   : event Action

StateNode
├── Id           : string                 ← stable, referenced by transitions
├── Name         : string                 ← unique key (the consumer picks its state by it)
├── NextMiss     : NextMissBehavior       ← Ignore (default) / Warning / Error
└── Transitions  : StateTransition[]      ← order = priority

StateTransition
├── Target       : string                 ← target node Id
├── WaitNext     : bool                   ← false — auto, true — on Next
└── Conditions   : Condition[]            ← AND; empty — always met
```

The model holds no logic: everything is changed by `StateGraphController` (extension methods). Properties are `public get; internal set`.

## Execution

### Entering a node

1. Conditions of **all** transitions of the node are started (`Condition.Init(callback, InstanceId)`), including "on `Next`" ones — a timer-like condition has to start in advance.
2. `CurrentNode` is published.
3. Auto transitions are evaluated.

A condition signal raised synchronously from `Init` is not processed until all conditions of the node are started — otherwise the transition would happen mid-start. Late signals from previous entries are cut off by the entry number.

Entering a node without transitions publishes it and immediately finishes the machine.

### Transitions

- **Auto** — on any condition signal the node re-evaluates its auto transitions in order; the first with met conditions fires.
- **On `Next`** — `Next()` picks the first "on `Next`" transition in order whose conditions are met. If none — the node's `NextMiss` reaction (silent / warning / error in the log), the node does not change.
- A transition stops the current node's conditions (`DeInit`), then enters the target.

### Completion

Natural (entering an end node) and forced (`Stop`) completion are the same: conditions stop, `CurrentNode` becomes empty, `OnFinished` is raised.

### Activity

| Operation | Effect |
|---|---|
| `Activate()` | First time, after deactivation without retention, or after completion — start from the start node. Otherwise: a node assigned by `ForceNode` while inactive is entered now; the current node re-evaluates auto transitions (conditions may have been met meanwhile) |
| `Deactivate(true)` | Keep state (Resume): conditions keep running, the transition is postponed until activation; only `ForceNode` (the node becomes current and is entered on activation) and `Stop` are accepted |
| `Deactivate(false)` | No retention (Restart): conditions are stopped and reset; `CurrentNode` stays published until the next start; signals are ignored. Also applies to an instance already deactivated with retention — this is how a consumer finally shuts the conditions down |

An inactive instance ignores `Start`, `Next` and (without retention) `ForceNode` / `Stop`.

### API

| Method | Description |
|---|---|
| `Activate()` / `Deactivate(bool retainState)` | Enable / disable (see "Activity") |
| `Start()` | Start or restart from the start node (active only) |
| `Next()` | "Next" signal |
| `ForceNode(string idOrName)` | Go to a node bypassing conditions; on a finished active instance — start from that node |
| `Stop()` | Forced completion; no-op when not running |
| `GetNode(string id)` / `GetCurrentNode()` | Node lookup |

## Graph validation

`StateGraphValidator.Validate(nodes, startNode)` → list of `StateGraphIssue` (severity, kind, node Id, transition index or -1, message).

| Errors (`Error`) | Warnings (`Warning`) |
|---|---|
| `NoStartNode` — start node not set or not found | `UnreachableNode` — node unreachable from the start |
| `InvalidId` — empty or duplicate Id | `ShadowedTransition` — transition after an unconditional one, never taken |
| `EmptyName` — empty name (empty is the neutral display value) | |
| `DuplicateName` — duplicate name | |
| `DanglingTarget` — transition to a missing node | |
| `AutoCycle` — cycle of auto transitions | |

**Cycles.** Only cycles of auto transitions are forbidden: they would loop the machine without an external signal. A cycle broken by at least one "on `Next`" transition is allowed.

**Shadowing.** An unconditional auto transition leaves the node at once — nothing after it is ever taken. An unconditional "on `Next`" transition takes every `Next` — no "on `Next`" transition after it is ever taken (auto ones still are).

At runtime the graph is validated once per instance — on the first `Start` / `ForceNode`. With errors the instance does not start, errors go to the log.

## Edge cases

| Situation | Behavior |
|---|---|
| Graph with errors | Errors logged, instance does not start (also guards against infinite recursion of an auto-transition cycle) |
| `Next` with no matching transition | Per `NextMiss`: silent / `Warning` / `Error`; node unchanged |
| `ForceNode` to an unknown node | `Error` logged |
| A `CurrentNode` subscriber synchronously calls `Next` / `ForceNode` / `Stop` | Handled; the interrupted entry does not continue |
| A condition fires right inside `Init` | Transition happens after all node conditions are started |
| `Stop` when not running | No-op, `OnFinished` not raised |
| Plain off/on with retention | Continues from the current node without re-entry |
| Activation after completion | Start from the start node |
| Deep copy of conditions | Provided by `Database` (`CopyFrom` → `DeepCopy`): every copy has its own `Condition` instances |

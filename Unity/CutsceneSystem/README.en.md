# CutsceneSystem

> ⚠️ **Experimental package.** Not yet battle-tested in production. The `CutsceneBus` public API and the content prefab contract may change without backward compatibility. Its satellite packages are experimental as well: [NaniExtensions/CutsceneSystem](../../NaniExtensions/CutsceneSystem/README.en.md), [SpineExtensions/Cutscenes](../../SpineExtensions/Cutscenes/README.en.md), [Sdk/CutsceneSystem](../../Sdk/CutsceneSystem/README.en.md).

**Namespace:** `Vortex.Unity.CutsceneSystem.*`
**Assembly:** `ru.vortex.unity.cutscene`

---

## Purpose

Plays a cutscene as a walk through a state graph. The package does not know what a graph node is shown with: Spine, sprite swaps or anything else assembled in the content prefab.

- **The show follows the cutscene graph.** Nodes and transitions are described by a [`StateGraph`](../../Core/StateGraphSystem/README.en.md) database record; every cutscene has its own graph. A node is shown by the `UIStateSwitcher` state with the same name. The graph may branch: transitions are picked by conditions or by the "next" signal.
- **The content holds and shows the graph.** The content prefab root has a [`StateGraphView`](../StateGraphSystem/README.en.md): it takes a graph copy from the database and switches the switcher. The orchestrator signals the graph and closes the show once the graph reaches an end node.
- **Content is a loadable prefab.** It is in memory only while the show runs.
- **One request bus** `CutsceneBus`: open, next, set node, skip. The bus executes nothing: it passes the request to the scene orchestrator and stores the show data.
- **The orchestrator does everything.** It remembers a request and executes it on its own `LateUpdate`. Requests and events are never executed nested inside each other.
- **Interruption is an immediate close** of the show.

Out of scope:

- **What a node is shown and voiced with.** That is the prefab content: `UIStateSwitcher` items, `AudioAdvancedControl` handlers, Spine handlers from `SpineExtensions`.
- **The graph itself.** Its structure, validation and editor belong to [`StateGraphSystem`](../StateGraphSystem/README.en.md).
- **Who starts a cutscene.** A script, UI or game logic calls `CutsceneBus` itself; Naninovel commands live in `NaniExtensions/CutsceneSystem`.
- **Saving.** The show state is not saved.
- **Pause.** Freezing visuals and sound is the content handlers' job.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Core.StateGraphSystem`, `Vortex.Unity.StateGraphSystem` | cutscene graph and its display (`StateGraphView`) |
| `Vortex.Core.DatabaseSystem` | base type of the graph record |
| `Vortex.Core.AssetHandleSystem` | `AssetHandle<GameObject>` — content prefab |
| `Vortex.Core.Extensions` | `StringData`, `IReactiveData` — control data |
| `Vortex.Unity.UI.Misc` | `UIStateSwitcher`, `[StateSwitcher]` |
| `Vortex.Unity.AppSystem` | `TimeController` — deferred refusal of an open that arrived while the slot was empty |
| `Vortex.Unity.EditorTools` | `[ClassLabel]` — index entry label in the inspector |
| `UniTask` | async content loading |

---

## Usage

### 1. Cutscene graph

A `Database/State Graph` record (see [StateGraphSystem](../StateGraphSystem/README.en.md)):

- a node per cutscene phase and an end node `End` without transitions;
- a phase waiting for a command moves on "on `Next`";
- an auto-advancing phase also moves on "on `Next`": the content itself sends the signal (see "The "next" signal");
- branching — several transitions with conditions; priority — transition order in the node.

The graph is edited in the visualizer. Cutscene prefabs and graphs can be created by the [asset factory](../../../Plugins/AssetFactory/README.en.md).

### 2. Content prefab

- The root has a `StateGraphView`: the cutscene graph, the content switcher, `Restart` mode. For content where a node change is a tween transition (sprite swaps) set `switchDelay` (see "Content switch delay").
- If the cutscene has auto transitions — also a `CutsceneContentHandler` on the root: the content sends "next" through it.
- `UIStateSwitcher`: a state per graph node, with the same name, including the empty `End` state.
- A node's visuals and sound are the items and objects of its state.
- A node with an auto transition has a signal source: a `CutsceneContentHandler.Next()` call from the state (`EventFire`) or a dedicated handler (for Spine — `SpineCutsceneAutoNext`).

Ready-made content prefab bases live one layer up, because their sound uses SDK handlers:

- sprite cutscene — [`Sdk/CutsceneSystem/Example`](../../Sdk/CutsceneSystem/README.en.md);
- Spine cutscene — [`SpineExtensions/Cutscenes/Example`](../../SpineExtensions/Cutscenes/README.en.md).

### 3. Scene orchestrator

An object with `CutsceneOrchestrator`:

| Field | Purpose |
|---|---|
| `cutscenes` | index: cutscene key and its content prefab handle. Keys are unique: on a duplicate the editor warns, the first entry is opened |
| `switcher` | orchestrator state switcher: Off / Loading / On |
| `target` | where content is placed; unset — under the orchestrator itself |
| `nextDelay` | debounce, seconds; 0.2 by default |

### 4. Commands

```csharp
CutsceneBus.Open(key);         // open; key — the orchestrator index entry key
CutsceneBus.Next();            // next: Next signal to the graph; entering an end node closes the show
CutsceneBus.SetNode("Final");  // set a node — by name or Id
CutsceneBus.Skip();            // skip — close at once

CutsceneBus.OnShowStarted  += key => { };   // show started: content loaded, the graph shows its first node
CutsceneBus.OnShowFinished += key => { };   // show finished or did not happen
```

A request is executed not at call time but on the nearest orchestrator `LateUpdate`. Events come after that.

---

## Architecture

```
Bus/
  CutsceneBus (static)             — orchestrator slot, request relay, show data and events

Models/
  CutsceneControl                  — show control data: current node name
  CutsceneStageState               — orchestrator states: Off / Loading / On

Handlers/
  CutsceneOrchestrator             — request intake and the whole show: loading, graph signals, closing
  CutsceneContentHandler           — auto-transition source: "next" signal with the shown node Id
```

### Bus and orchestrator

The dependency is one-way: the orchestrator knows the bus, the bus knows only that the slot is taken.

| What the bus has | Purpose |
|---|---|
| Slot | a single holder; while taken, a second candidate is refused |
| Requests | `Open`, `Next`, `Next(node)`, `SetNode`, `Skip`. The bus passes them to the slot holder as events at once and forgets them |
| Show data | current node name (`Control`). Written by the slot holder |
| Show events | `OnShowOpening`, `OnShowStarted`, `OnShowFinished`. Raised by the slot holder |

The bus itself does one thing: it refuses an open that arrives while the slot is empty. It does not wait for an orchestrator: an error is logged at once, `OnShowFinished(key)` — at the end of the frame. The event is deferred so that a subscriber that reopens a cutscene on every refusal does not recurse forever. The deferred event is raised by `TimeController`: without it in the scene it will not come.

Bus event subscribers are reset on game start (`SubsystemRegistration`). Subscribe after that point — e.g. in `OnEnable`.

### Requests and their execution

When a request arrives, the orchestrator only remembers it:

| Request | What is remembered | When it is not accepted |
|---|---|---|
| `Open(key)` | "open requested" and the key. A previous, not yet executed open is pushed out and gets `OnShowFinished`; requests for the previous show's progress are cancelled | — |
| `Next()` | "next requested" | the show is not started yet (content loading); an open, a skip or a close by graph end is pending; less than `nextDelay` since entering the node |
| `Next(node)` | "next requested" | same, but without debounce; instead the node must match the graph's current node |
| `SetNode(node)` | "node requested" with its Id or name; a later request replaces an earlier one. Arrived while the cutscene is opening — waits for the show to start | no cutscene open or opening; a skip is pending; empty name (error logged) |
| `Skip()` | "skip requested". A pending open is cancelled and gets `OnShowFinished` | — |

The graph entering an end node (`OnFinished`) is also only remembered: "graph finished".

The orchestrator executes on its `LateUpdate`, always in the same order:

1. **Skip, open over a running cutscene or a finished graph** — the running one closes, its `OnShowFinished` is raised.
2. **Failed opens** get `OnShowFinished` — those accumulated by this step. A refusal produced by a handler of this event waits for the next pass.
3. **Open** — refusal (`OnShowFinished(key)`) or loading starts and `OnShowOpening(key)`.
4. **Load result** — content is created; its `StateGraphView` takes a graph copy and starts it from the start node; a node requested during loading is set instead of the start node; `OnShowStarted(key)`.
5. **Graph progress** — set node, or if none requested — "next" (`Next` to the graph).
6. **The graph reached its end** on this pass — the show closes right away.

Rules:

- **One "next" request per node.** A request is held until executed; repeats until then add nothing. The player will press again, an auto transition fires for its own node.
- **Graph progress — only what was requested before the pass.** A request arriving during execution (e.g. an auto transition from the state of a just shown node) is executed next frame.
- **Graph progress — only for its own cutscene and only after the show starts.** "Next" before the start is ignored; a node request before the start waits for it and replaces the start node. A request accepted for one cutscene does not carry over to another.
- **Setting a node is more precise than "next".** Both in one pass — the node is set.
- **Condition transitions** are made by the graph itself, at any moment: the orchestrator learns of them from the graph's current node (debounce counts from them too) and of the graph end — from `OnFinished`.
- **No nesting.** An event handler may call the bus: the request is remembered and executed later — in the same pass if its step is still ahead, otherwise next frame.
- **A failure does not stop the pass.** An exception in a step is logged; the remaining steps run.
- **The step order is fixed**, not the arrival order. The difference shows only when different requests arrive in one frame: `Skip` after `Open` cancels that open; `Open` after `Skip` — both are executed.

### Flow

```
CutsceneBus.Open(key)                       — the bus passes the request, the orchestrator remembers it
    ↓ (orchestrator LateUpdate)
[Orchestrator] the running cutscene closes, OnShowFinished(its key)
    ↓
[Orchestrator] key in the index
    └── switcher → Loading, OnShowOpening(key), prefab loading
    ↓ (loading done; nearest LateUpdate)
[Orchestrator] instance under target
    ├── StateGraphView takes a graph copy and shows the start node
    ├── switcher → On
    ├── a node requested during loading — instead of the start node
    └── OnShowStarted(key)
    ↓
[Orchestrator] signals the graph
    ├── CutsceneBus.Next()          — external command → Next
    ├── CutsceneBus.Next(node)      — auto transition from content → Next
    └── CutsceneBus.SetNode(node)   — direct jump → ForceNode
[Graph]        condition transitions — on its own
    ↓ … next nodes …
[Graph] entering an end node → OnFinished
    ↓ (orchestrator LateUpdate)
[Orchestrator] disables and destroys the instance, releases the prefab, switcher → Off
    ├── node name → empty
    └── OnShowFinished(key)
```

### Nodes

- **The graph comes from the content** at show start: the root `StateGraphView` holds the copy. No `StateGraphView`, graph not obtained (no record, database not loaded) or not started (graph errors) — the show closes with an error in the log.
- **"Next"** — a `Next` signal to the graph: it picks the first "on `Next`" transition in order whose conditions are met. None — the node's configured reaction (silent, warning, error), the node does not change.
- **End** — entering a node without transitions: the show closes. That node's state (`End`) is shown before the content unloads.
- **Setting a node** — by Id or name, bypassing conditions. Unknown node — the graph logs an error, the node does not change. The current node — nothing happens.

### The "next" signal and debounce

One signal, two sources:

- `CutsceneBus.Next()` — an external command. Not accepted until `nextDelay` has passed since entering the node; time ignores `Time.timeScale`. It carries no node and does not check it.
- `CutsceneBus.Next(node)` — an auto transition from the content. Bypasses debounce. The content names the Id of the node it shows; the signal is accepted only if the graph is on that same node.

Both checks happen when the request arrives. Both orders are covered:

- An auto transition fired, then a command came — it falls into the new node's debounce window and does not skip it. The window counts from any node entry, including by setting and by conditions; with `nextDelay = 0` there is no debounce.
- A command came, then the previous node's auto transition fired (while the content catches up) — the signal names the old node and is not accepted.

The content handler supplies the node itself: the auto-transition source calls `CutsceneContentHandler.Next()`, which takes the shown node Id from `StateGraphView`. A call from disabled content is ignored: its show is closed, and a deferred signal (tween, timer) must not leak into the next cutscene.

### Show events

| Event | When |
|---|---|
| `OnShowOpening(key)` | the open is accepted by the orchestrator, content loading started |
| `OnShowStarted(key)` | content loaded, the graph shows its first node |
| `OnShowFinished(key)` | the show closed or did not happen: refused open, load failure, cancellation |

- **The cutscene key** is the event parameter: a subscriber tells its own cutscene from others.
- **Order for one cutscene:** `OnShowOpening` → `OnShowStarted` → `OnShowFinished`. `OnShowOpening` is always followed by `OnShowFinished` with the same key, so the pair can drive a "cutscene open" flag, including loading. A refused open gives only `OnShowFinished`.
- **Raised by the orchestrator once its state is consistent:** `OnShowFinished` — after the content unloads, `OnShowStarted` — after the graph starts.
- **`IsActive`** is true from the `Open` call until its `OnShowFinished` is raised. If the cutscene was closed by a new open, `IsActive` is true inside the previous one's `OnShowFinished` handler.
- **Requests from a handler** are remembered and executed later (see "No nesting").
- **Subscribers are isolated.** One subscriber's exception is logged (with stack) and does not affect others. After a subscriber exception in `OnShowOpening` or `OnShowStarted` the cutscene is interrupted — as by `Skip`, next frame.
- **`OnShowStarted` is not raised** if the start failed (content exception on creation or on entering a node).

### Content switch delay

For content where a node change is a tween transition, `StateGraphView` has `switchDelay`. Setting the next state mid-transition would cut it and give a brightness jump.

- The next state is set no earlier than `switchDelay` after the previous one.
- Nodes arriving during the wait do not queue: when it ends, the latest one is shown at once. A jump over several nodes shows no intermediate ones.
- The first node is shown at once: there were no transitions before it.
- Time ignores `Time.timeScale` — like the tweens.
- The delay applies to the display only: the graph moves without waiting.
- An auto transition from a state the content shows late is not accepted: the graph has already left that node.
- The View and the tweens know nothing of each other. Matching `switchDelay` to the tween duration is the layout author's job.

### Interruption

`Skip` closes the show at once — on the nearest orchestrator pass: content unloads, `OnShowFinished` is raised. While content is loading, `Skip` cancels the load.

### Disabling the orchestrator

On `OnDisable` the orchestrator unsubscribes from requests and frees the slot at once. The show does not outlive it:

- **A running show** closes at once, `OnShowFinished` is raised right there; loading is cancelled.
- **Pending opens** get `OnShowFinished`.

The slot is free from the moment of disabling: another orchestrator may take it at once. An orchestrator re-enabled takes the slot again.

---

## Contract

### Guarantees

- **One cutscene.** Opening a new one first closes the running one; its `OnShowFinished` is raised before the new open starts.
- **No nesting.** Requests and the graph end are remembered and executed on the orchestrator `LateUpdate`.
- **One `OnShowFinished(key)` per `Open` call.** `OnShowOpening(key)` and `OnShowStarted(key)` — at most one each and always earlier, in that order.
- **One "next" request — one signal to the graph.** The first accepted request in a node is executed, repeats until then have no effect.
- **One close.** All paths — graph end, skip, failure, orchestrator disable — go through one place: content unloads, show data resets, the event is raised.
- **Content unloads** when the show closes and when the orchestrator is disabled. It is disabled at once, without waiting for destruction at frame end: graph conditions go out with `StateGraphView`, and deferred content signals do not reach the next show.
- **The node name is written in one place.** `CutsceneControl` is locked by the bus owner key; the slot holder reports the node.
- **Signals are bound to the show and the node.** A progress request for one show and an auto transition from an already passed node have no effect on the running show.
- **A stale load affects nothing.** The result of a cancelled load — success or failure — is discarded.
- **The bus does not depend on the orchestrator.** It knows only that the slot is taken.

### Discipline

- **One source drives the cutscene.** The package expects one non-competing playback system: a script, UI or game logic drive the cutscene in turn, not all at once. Show event handlers that open, skip or move cutscenes in response and thus override each other are outside normal use (see "Limitations").
- **One orchestrator.** The bus slot is single; a second orchestrator is refused with an error.
- **Index keys are unique.**
- **The content prefab root has a `StateGraphView`** with the cutscene graph in `Restart` mode; the switcher has a state per graph node, with the same name.
- **The graph has an end node**, otherwise the show closes only by skipping.
- **An auto-transition source sits only in auto nodes**, and such nodes have an "on `Next`" transition.

### Limitations

- A request's result is visible not right after the call but after the orchestrator `LateUpdate`.
- A prefab whose load completes at once (direct reference, warm cache) starts in the same pass as the open: the `Loading` state then does not survive to the end of the frame.
- The graph starts from the start node when the content is created. A node requested during loading is set right after in the same frame: the start node enters and leaves (its conditions start and stop).
- Different requests of one frame run in step order, not arrival order.
- Competing requests from show event handlers are not supported. Examples of what may happen then:
  - an `OnShowFinished` handler calls `Skip` exactly when an open from another handler closes the running cutscene — the open gets two `OnShowFinished` (one with an empty key and an "empty key" error in the log), and `IsActive` may become false while a show runs;
  - a refused open's `OnShowFinished` comes a frame later, after another cutscene's `OnShowOpening` — a subscriber that tracks "cutscene open" by this pair without checking the key closes it too early;
  - one subscriber's exception interrupts the cutscene and with it cancels an open requested by another subscriber.
- The current node cannot be restarted with `SetNode`: setting the same node does nothing.
- The key in an event does not distinguish two consecutive shows of the same cutscene.
- The "next" command does not check the node: with frequent commands the show may outrun sprite content and close before it shows the last nodes.
- A refused second orchestrator does not take the slot when the first leaves — it must be re-enabled.
- If the orchestrator is disabled from its own `Loading` or `On` state, its switcher may stay in that state until the next enable: the switcher refuses a nested `Off` and logs an error.
- `target` must be active: `StateGraphView` takes the graph on `OnEnable`, and under a disabled parent no graph is obtained — the show closes with an error.

---

## Edge cases

| Situation | Behavior |
|---|---|
| `Open` with an empty key | `Debug.LogError`, `OnShowFinished(key)` |
| `Open` with an empty slot | `Debug.LogError` at once, `OnShowFinished(key)` at frame end. An orchestrator enabled later in the same frame does not get this open |
| An `OnShowFinished` subscriber opens a cutscene on every event | at most one open per frame: a refusal produced by the handler is raised next pass |
| Key missing from the index, prefab not set | `Debug.LogError`, `OnShowFinished(key)` |
| Duplicate key in the index | editor warning; the first entry opens |
| `Open` while a cutscene runs | the previous one closes (`OnShowFinished`), then the new one starts loading in the same pass |
| `Open` while the previous one loads | loading is cancelled (`OnShowFinished`), then the new one starts loading |
| Two `Open` in a frame | the last one opens; the first gets `OnShowFinished` |
| `Open`, then `Skip` in one frame | the open is cancelled (`OnShowFinished`); the running cutscene closes |
| `Open` from a show event handler | remembered; executed in the same pass if the open step is still ahead, otherwise next frame |
| Orchestrator disabled while starting a show (from content `Awake`, its handlers, switcher states) | the start stops, content is removed, `OnShowFinished(key)` |
| Prefab load failure, empty prefab, no `StateGraphView` on the root, graph not obtained or not started | `Debug.LogError`, content unloads, `OnShowFinished(key)` |
| Exception while starting the show (content creation, node entry) | exception logged, the show closes, `OnShowFinished(key)`; `OnShowStarted` is not raised |
| Load completed after cancellation, including a failure | the result is discarded, the running show is unaffected |
| `Next` earlier than `nextDelay` after entering a node | not accepted |
| `Next` / `SetNode` / `Skip` without an open cutscene, `Next` before the show starts | nothing |
| `Next` in a node without a matching "on `Next`" transition | the node's configured reaction (silent / warning / error), node unchanged |
| `Next` leads to an end node | the show closes in the same pass, `OnShowFinished(key)` |
| The graph reached an end node by conditions | the show closes on the nearest pass |
| `SetNode` during loading | applied at show start instead of the start node |
| `SetNode` with an empty name | `Debug.LogError`, the request is not remembered |
| `SetNode` with an unknown node | the graph logs an error, node unchanged |
| `SetNode` with the current node | nothing |
| `SetNode` to an end node | the show closes |
| `SetNode` and `Next` in one pass | `SetNode` is executed |
| Progress accepted, but in the same pass another cutscene replaced this one | the request is dropped, the new cutscene does not skip its start node |
| Several `Next` per frame from any sources | one signal to the graph |
| `Next(node)` with a node the graph already left | not accepted |
| `CutsceneContentHandler.Next()` from disabled content | ignored |
| `Skip` during loading | loading cancelled, `OnShowFinished(key)` |
| `Skip` twice per frame | one skip |
| Exception in a show event subscriber | logged; other subscribers get the event; after `OnShowOpening` or `OnShowStarted` the cutscene is interrupted next frame |
| Content exception on a node change | logged; the show continues |
| The switcher lacks a node's state | `StateGraphView` logs an error, display unchanged |
| Orchestrator disabled mid-show | the show closes at disable time, content unloads, slot freed |
| Orchestrator disabled by content during unload | the show is already closing; no second close |
| Orchestrator disabled with a pending open | `OnShowFinished(key)` |
| Second orchestrator | `Debug.LogError`, not registered |
| A new node earlier than `switchDelay` | when the delay ends, the latest node is shown at once |
| Cutscene finished while content waits for the delay | content unloads, the deferred switch does not happen |

---

## Public API

```csharp
namespace Vortex.Unity.CutsceneSystem.Bus
{
    public static class CutsceneBus
    {
        public static CutsceneControl Control { get; }
        public static bool IsActive { get; }      // open accepted, content loading or show running

        public static event Action<string> OnShowOpening;    // parameter — cutscene key
        public static event Action<string> OnShowStarted;    // parameter — cutscene key
        public static event Action<string> OnShowFinished;   // parameter — cutscene key

        public static void Open(string key);      // key — orchestrator index entry key
        public static void Next();                // external command, debounced
        public static void Next(string nodeId);   // auto transition from content; nodeId — the shown node
        public static void SetNode(string node);  // set a node — Id or name
        public static void Skip();                // close the show at once
    }
}

namespace Vortex.Unity.CutsceneSystem.Models
{
    public class CutsceneControl : IReactiveData
    {
        public StringData Node { get; }           // current node name; empty — no show
        public event Action OnUpdateData;         // node changed
    }

    public enum CutsceneStageState { Off, Loading, On }
}

namespace Vortex.Unity.CutsceneSystem.Handlers
{
    public class CutsceneOrchestrator : MonoBehaviour { }     // configured in the inspector

    public class CutsceneContentHandler : MonoBehaviour
    {
        public void Next();                                   // auto transition: the shown node Id — to the bus
    }
}
```

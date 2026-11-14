# CutsceneSystem

**Namespace:** `Vortex.Unity.CutsceneSystem.*`
**Assembly:** `ru.vortex.unity.cutscene`

---

## Purpose

Shows a cutscene as a sequence of phases. The package does not know how a phase is shown: it can be Spine, a sprite swap or anything else assembled in the content prefab.

- **A cutscene is described by a logic chain** (`LogicChain`). There is no phase engine of its own: steps, transitions and waits come from `LogicChainsSystem`.
- **The content is a loadable prefab.** It stays in memory only while the cutscene is shown.
- **Phases are content states.** The phase number is turned into a `UIStateSwitcher` state.
- **One request bus**, `CutsceneBus`: open, next, skip. The bus executes nothing: it passes a request to the scene orchestrator and keeps the show data.
- **The orchestrator does everything.** It remembers a request and executes it on its own `LateUpdate`. Requests and events never execute nested inside each other.
- **Fly-through instead of a cut.** An interruption is a fast pass of the chain to its final step.

Out of scope:

- **Phase branching.** The sequence is strictly linear.
- **How a phase is shown and how it sounds.** That is the prefab's content: `UIStateSwitcher` items, `AudioAdvancedControl` handlers, Spine handlers from `SpineExtensions`.
- **Who starts a cutscene.** A script, UI or game logic call `CutsceneBus` themselves; Naninovel commands live in `NaniExtensions/CutsceneSystem`.
- **Saving.** The show state is not part of a save.
- **Pause.** Freezing visuals and sound is up to the content handlers.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Core.LogicChainsSystem`, `Vortex.Unity.LogicChainsSystem` | the chain, running it, the `UnityLogicAction` link base, the chain id given to a link and a condition |
| `Vortex.Unity.LogicConditionsSystem` | the `UnityCondition` condition base |
| `Vortex.Core.DatabaseSystem`, `Vortex.Unity.DatabaseSystem` | getting a chain by key, `[DbRecord]` in the orchestrator index |
| `Vortex.Core.AssetHandleSystem` | `AssetHandle<GameObject>` — the content prefab |
| `Vortex.Core.Extensions` | `IntData`, `IReactiveData` — the control data |
| `Vortex.Unity.UI.Misc` | `DataStorage`, `DataStorageView<T>`, `UIStateSwitcher`, `[StateSwitcher]` |
| `Vortex.Unity.AppSystem` | `TimeController` — deferred refusal event for an open that arrived while the slot was empty |
| `UniTask` | asynchronous content loading |

---

## Usage

### 1. The cutscene chain

`Create → Database → Logic Chain`. A step per phase and one final step:

```
1. Phase 1
   Actions:    Cutscene: next phase
   Connector → Phase 2
      Conditions: Cutscene: wait «Next»

2. Phase 2
   Actions:    Cutscene: next phase
   Connector → Completion
      Conditions: Cutscene: wait «Next»

3. Completion
   Actions:    Cutscene: complete
   Connector → _CompleteChain
      Conditions: — (empty)
```

The chain rules are checked on open (see "Chain validation").

### 2. The content prefab

- On the root — `DataStorage` and `CutsceneContentHandler`. If a phase change is a tween transition (a sprite swap), use `CutsceneSpriteContentHandler` instead (see "Content switch delay").
- `UIStateSwitcher`: state zero is empty (the cutscene is not shown yet), then one state per phase, strictly in phase order. The switcher's start state is the empty one.
- A phase's visuals and sound are the items and objects of its state.
- A phase with auto-transition has a signal source: a call to `CutsceneContentHandler.Next()` from the state (`EventFire`) or a dedicated handler (for Spine — `SpineCutsceneAutoNext`).

Ready-made content prefab bases live one layer up, because their sound uses SDK handlers:

- a sprite cutscene — [`Sdk/CutsceneSystem/Example`](../../Sdk/CutsceneSystem/README.en.md);
- a Spine cutscene — [`SpineExtensions/Cutscenes/Example`](../../SpineExtensions/Cutscenes/README.en.md).

### 3. The orchestrator in the scene

An object with `CutsceneOrchestrator`:

| Field | Purpose |
|---|---|
| `cutscenes` | the index: a chain record and the handle of its content prefab |
| `switcher` | the orchestrator's state switcher: Off / Loading / On |
| `target` | where the content is placed; not set — under the orchestrator itself |
| `nextDelay` | debounce, seconds; 0.2 by default |

### 4. Commands

```csharp
CutsceneBus.Open(chainGuid);   // open; the key is the LogicChain record id
CutsceneBus.Next();            // next
CutsceneBus.Skip();            // skip

CutsceneBus.OnShowStarted  += key => { };   // the show started: content loaded, chain running
CutsceneBus.OnShowFinished += key => { };   // the show finished or did not happen
```

A request executes not at the moment of the call but on the orchestrator's next `LateUpdate`. Events arrive after that.

---

## Architecture

```
Bus/
  CutsceneBus (static)             — the orchestrator slot, request relay, show data and events

Models/
  CutsceneControl                  — show control data: the phase number
  CutsceneStageState               — orchestrator states: Off / Loading / On

LogicChains/
  CutscenePhase                    — the "Phase" link
  CutsceneComplete                 — the "Completion" link
  CutsceneReadyCondition           — the "Ready to proceed" condition

Controllers/
  CutsceneChainValidator           — chain validation before running

Handlers/
  CutsceneOrchestrator             — takes requests and runs the whole show: loading, chain, phases, closing
  CutsceneContentHandler           — phase number → content switcher state
  CutsceneSpriteContentHandler     — the same, with a delay between switches: for tween transitions
```

### The bus and the orchestrator

The dependency is one-way: the orchestrator knows the bus, the bus knows about the orchestrator only that the slot is taken.

| What the bus holds | Purpose |
|---|---|
| The slot | a single holder; while the slot is taken, a second claimant is rejected |
| Requests | `Open`, `Next`, `Next(phase)`, `Skip` and chain link signals. The bus passes them to the slot holder at once as an event and keeps no memory of them |
| Show data | the phase number (`Control`), `IsSkipping`, the chain id of the running show. Written by the slot holder, read by the content, links and conditions |
| Show events | `OnShowOpening`, `OnShowStarted`, `OnShowFinished`. Raised by the slot holder |

The bus itself does one thing: it refuses an open that arrived while the slot was empty. It does not wait for an orchestrator: an error is logged at once, `OnShowFinished(key)` is raised at the end of the frame. The event is deferred so that a subscriber reopening a cutscene on every refusal does not fall into endless recursion. The deferred event is raised by `TimeController`: without it in the scene the event will not arrive.

Subscribers of the bus events are reset when the game starts (`SubsystemRegistration`). Subscribe after that moment — in `OnEnable`, for example.

### Requests and their execution

When a request arrives, the orchestrator only remembers it:

| Request | What is remembered | When it is not accepted |
|---|---|---|
| `Open(key)` | "open was requested" and the key. A previous open request not yet executed is displaced and will get `OnShowFinished` | — |
| `Next()` | "next was requested" | the first phase is not started yet (the content is loading, the first chain step has not run); an open or a skip is waiting to execute; a fly-through is in progress; less than `nextDelay` has passed since entering the phase |
| `Next(phase)` | "next was requested" | the same, but without the debounce; instead the phase must match the chain's phase |
| `Skip()` | "skip was requested". An open request not yet executed is cancelled and will get `OnShowFinished` | — |

The orchestrator executes on its `LateUpdate`, always in the same order:

1. **A skip, or an open over a running cutscene** — the running one is driven to the end by fly-through, its `OnShowFinished` is raised.
2. **Opens that will not happen** get `OnShowFinished` — those accumulated by this step. A refusal produced by a handler of this event waits for the next pass.
3. **Open** — a refusal (`OnShowFinished(key)`) or the start of loading and `OnShowOpening(key)`.
4. **Load result** — the content is created, the chain is run, the first phase goes to the content, `OnShowStarted(key)`.
5. **"Next"** — a signal to the chain; then the new phase goes to the content, or the show is closed.

Rules:

- **One "next" request per phase.** A request is held until it is executed; repeated ones before that add nothing. The player will press again, an auto-transition fires for its own phase by itself.
- **"Next" — only what was requested before the pass.** A request that arrives during execution (for example, an auto-transition from the state of the phase just shown) executes on the next frame.
- **"Next" — only for its own cutscene and only after the first phase has started.** While the content is loading and the first chain step has not run, any request is ignored. A request accepted for one cutscene does not carry over to another.
- **No nesting.** An event handler and a chain link may address the bus: the request is remembered and executed later — in the same pass if its step is still ahead, otherwise on the next frame.
- **A failure does not stop the pass.** An exception in a step is logged; the remaining steps execute.
- **The step order is fixed**, not the arrival order. The difference shows only when different requests arrive in one frame: a `Skip` after an `Open` cancels that open; an `Open` after a `Skip` — both execute.

### Flow

```
CutsceneBus.Open(key)                       — the bus passes the request to the orchestrator, which remembers it
    ↓ (the orchestrator's LateUpdate)
[Orchestrator] the running cutscene flies through and closes, OnShowFinished(its key)
    ↓
[Orchestrator] key in the index, chain from the database, chain validation
    └── switcher → Loading, OnShowOpening(key), prefab loading
    ↓ (load completed; the nearest LateUpdate)
[Orchestrator] instance under target, CutsceneControl → the instance's DataStorage
    ├── switcher → On
    └── the chain goes into the registry and is run
[Chain] phase step: CutscenePhase → phase number +1
[Orchestrator] phase number → content; OnShowStarted(key)
    ↓
[Content] CutsceneContentHandler → switcher.Set(phase number)
    ↓
[Chain] CutsceneReadyCondition waits for the "next" signal
    ├── CutsceneBus.Next()       — external command
    └── CutsceneBus.Next(phase)  — auto-transition from the content
    ↓ (the orchestrator's LateUpdate: a signal to the chain) … following phases …
[Chain] final step: CutsceneComplete marks "the show is to be closed"
    ↓ (the chain has returned control)
[Orchestrator] turns the fly-through off, disables and destroys the instance, releases the prefab, switcher → Off
    ├── phase number → 0
    └── OnShowFinished(key)
```

### Links

| Link | In the chain list | What it does |
|---|---|---|
| `CutscenePhase` | `Cutscene: next phase` | increments the phase number by one; not executed during fly-through |
| `CutsceneReadyCondition` | `Cutscene: wait «Next»` | waits for the "next" signal; satisfied immediately during fly-through |
| `CutsceneComplete` | `Cutscene: complete` | marks that the show is to be closed; executed during fly-through too |

The phase number is ordinal: the link carries no data, a phase is simply "the next one".

**A link knows its chain.** `LogicChainsSystem` tells a link and a condition the id of the chain executing them. The bus passes a link's signal to the orchestrator together with that id, and the orchestrator compares it with the chain of its show:

- "Phase" and "Completion" links of a foreign chain or of an already closed show do nothing;
- a condition of such a chain is treated as satisfied at once.

**The "Completion" link does not close the show itself.** It only marks that the show is to be closed; the orchestrator closes it when the chain returns control. So a show is never closed from inside an executing chain, and fly-through mode stays on until the chain ends.

### The "next" signal and debounce

One signal, two sources:

- `CutsceneBus.Next()` — an external command. Not accepted until `nextDelay` has passed since entering the phase; the time is counted regardless of `Time.timeScale`. It carries no phase number and does not check the step.
- `CutsceneBus.Next(phase)` — auto-transition from the content. Bypasses the debounce. The content names the phase it is showing; the signal is accepted only if the chain is on that same phase.

Both checks happen when the request arrives. Both orders are covered:

- An auto-transition fires and a command follows — the command lands in the debounce window of the new phase and does not skip over it. The window is started by the "Phase" link: a step without it, or `nextDelay = 0`, has no protection.
- A command arrives and the previous phase's auto-transition fires after it (while the content is catching up with the chain) — the signal names the old phase and is not accepted.

The content handler supplies the phase itself: an auto-transition source calls `CutsceneContentHandler.Next()`. A call from disabled content is ignored: its show is closed, and a postponed signal (a tween, a timer) must not reach the next cutscene.

### Show events

| Event | When |
|---|---|
| `OnShowOpening(key)` | the orchestrator accepted the open, content loading has started |
| `OnShowStarted(key)` | the content is loaded, the chain is running, the first phase has gone to the content |
| `OnShowFinished(key)` | the show is closed or did not happen: a refused open, a load failure, a cancellation |

- **The cutscene key** is the event parameter: a subscriber can tell its own cutscene from someone else's.
- **The order for one cutscene:** `OnShowOpening` → `OnShowStarted` → `OnShowFinished`. `OnShowOpening` is always followed by `OnShowFinished` with the same key, so this pair can drive a "cutscene is open" flag that covers loading too. A refused open gives only `OnShowFinished`.
- **Raised by the orchestrator once the state is in order:** `OnShowFinished` — after the content is unloaded, `OnShowStarted` — after the chain is run and the first phase is handed over. While the chain is executing, no events are raised. One exception: if a chain link disabled the orchestrator, the `OnShowFinished` of open requests not yet executed is raised at once, from inside the chain.
- **`IsActive`** is true from the `Open` call until its `OnShowFinished` is raised. If a cutscene was closed by opening a new one, `IsActive` is true in the previous one's `OnShowFinished` handler.
- **Requests from a handler** are remembered and executed later (see "No nesting").
- **Subscribers are isolated.** An exception in one is logged (with the stack) and does not affect the others. After an exception in an `OnShowOpening` or `OnShowStarted` subscriber the cutscene is interrupted — as with `Skip`, on the next frame.
- **`OnShowStarted` is not raised** if the start failed (an exception in a link before the first wait) or the chain completed at once without stopping on any condition.

### Content switch delay

`CutsceneSpriteContentHandler` is a `CutsceneContentHandler` subclass for content whose phase change is a tween transition. Setting the next state in the middle of a transition would cut it short and make the frame jump in brightness.

| Field | Purpose |
|---|---|
| `switchDelay` | minimum time between phase switches, seconds; 0.5 by default |

- The next state is set no earlier than `switchDelay` after the previous one.
- Phases that arrive during the wait are neither lost nor skipped: states are set one at a time, strictly in order.
- The first phase is shown at once: there were no transitions before it.
- Time is counted regardless of `Time.timeScale` — the same as the tweens.
- The delay affects the content only: the chain and the phase number do not wait.
- An auto-transition from a state the content shows late is not accepted: the chain has already left that phase.
- The handler and the tweens know nothing about each other. Matching `switchDelay` to the tween duration is the layout designer's job.

### Fly-through

On a `Skip` request the orchestrator turns on the bus parameter `IsSkipping` and gives the chain the "next" signal. The chain then runs by itself: the package's conditions are satisfied immediately, phase links are skipped, the final link marks the end. When the chain returns control, the orchestrator closes the show.

While the content is still loading the chain is not running yet: `Skip` cancels the load.

Fly-through is a parameter of this package rather than a feature of `LogicChainsSystem`: in the shared core it would affect chains whose waits must not be skipped (application loading, for example).

### Disabling the orchestrator

On `OnDisable` the orchestrator unsubscribes from requests and releases the slot at once. The show does not outlive it; whatever the form of disabling, the remaining chain steps run to the final one in fly-through mode:

- **The chain is waiting for the signal** — the cutscene flies through to the end at once, `OnShowFinished` is raised right there.
- **The chain is executing right now** (its own link disabled the orchestrator) — fly-through mode is turned on and the chain reaches the final step by itself. The show is closed by the pass that ran the chain — when it returns control. If it does not reach the final step, the show is closed forcibly with an error in the log.
- **The content is still loading or the show is starting** — the chain is not running, there is nothing to fly through: the show closes at once.
- **Open requests not yet executed** get `OnShowFinished`.

The slot is free from the moment of disabling: another orchestrator can take it right away. An orchestrator enabled again takes the slot anew.

### Chain validation

On open the chain is walked from the start step to the end. Rules:

- the start step is set, every connector leads to an existing step;
- a step has exactly one connector;
- a connector has either no conditions or a single `CutsceneReadyCondition`;
- the `CutsceneComplete` link is in the last step and only there;
- the last step's connector has no conditions;
- steps do not repeat.

A chain that fails validation is not run. The reasons for the strictness: a foreign condition would stall the fly-through, and without the final step the content would not be unloaded. Two conditions on one connector are also rejected because in `LogicChainsSystem` they can fire the transition twice when both are already satisfied on entering the step.

### A stopped chain

An exception in a link stops the chain. Where it ends up depends on the moment:

- **The chain was moving on a "next" signal** — the exception is caught by `LogicChainsSystem`: it logs the message (without the stack) and removes the step's condition. The show can no longer be moved. The orchestrator sees this — the chain has returned control but nobody waits for the "next" signal — and closes the show with an error in the log rather than leaving it hanging.
- **A link before the first wait** (while the show is starting) — the exception reaches the orchestrator: it is logged with the stack, and the show is closed as a start failure.

---

## Contract

### Guarantees

- **One cutscene.** Opening a new one first drives the running one to the end by fly-through; its `OnShowFinished` is raised before the new one starts opening.
- **No nesting.** A request is remembered and executed on the orchestrator's `LateUpdate`; a show is never closed from inside an executing chain.
- **One `OnShowFinished(key)` per `Open` call.** `OnShowOpening(key)` and `OnShowStarted(key)` — at most one each and always earlier, in that order.
- **One "next" request — one transition.** The first accepted request in a phase executes; repeated ones before it executes have no effect.
- **One closing point.** Every path — the final link, a fly-through, a failure, disabling the orchestrator — goes through one place: the content is unloaded, the show data is reset, the event is raised.
- **Fly-through on disabling.** Whatever the form of disabling the orchestrator, the remaining chain steps run to the final one in fly-through mode.
- **Content is unloaded** when the show closes and when the orchestrator is disabled. It is disabled at once, without waiting for destruction at the end of the frame: it will not receive the phase of the next show.
- **The phase number is written in one place.** `CutsceneControl` is locked with the bus's owner key; the phase is reported by the slot holder.
- **Events are tied to a show and a step.** A link of a foreign chain and an auto-transition from a phase already passed have no effect on the running show.
- **A stale load affects nothing.** The result of a cancelled load — success or failure — is discarded.
- **The bus does not depend on the orchestrator.** It only knows that the slot is taken.

### Discipline

- **A cutscene is driven by one source.** The package is built for a single, non-competing playback system: a script, UI or game logic drive a cutscene in turn, not all at once. Show event handlers that respond to an event by opening, skipping or advancing cutscenes themselves and thereby cut across each other are outside normal use (see "Limitations").
- **One orchestrator.** The bus slot is single; a second orchestrator is rejected with an error.
- **The content switcher has "number of phases + 1" states**, in the same order as the steps.
- **`DataStorage` on the content prefab root.**
- **An auto-transition source exists only in phases with auto-transition.**
- **The cutscene chain** follows the validation rules.

### Limitations

- A request's result is visible after the orchestrator's `LateUpdate`, not right after the call.
- A prefab whose load completes at once (a direct reference, a warm cache) starts in the same pass as the open: the `Loading` state then does not last until the end of the frame.
- Different requests of one frame execute in step order, not in arrival order.
- Competing requests from show event handlers are not supported. Examples of what can then happen:
  - an `OnShowFinished` handler calls `Skip` exactly when an open that came from another handler is closing the running cutscene — that open gets two `OnShowFinished` (one with an empty key and an "empty key" error in the log), and `IsActive` may become false while a show is running;
  - the `OnShowFinished` of a refused open arrives a frame later, after the `OnShowOpening` of another cutscene — a subscriber that keeps a "cutscene is open" flag from this pair without comparing keys will clear it too early;
  - an exception in one subscriber interrupts the cutscene and with it cancels an open requested by another subscriber.
- A phase/state mismatch is visible only when there are more phases than states. Extra states go undetected.
- The chain record must be `MultiInstance` (this is how `LogicChainPreset` creates it). For a record of another type the open is refused as "chain not found in the database".
- The current phase cannot be restarted: the switcher ignores setting the same state again.
- The key in an event does not distinguish two shows of the same cutscene in a row.
- The "next" command does not check the step: with frequent commands the chain can run ahead of sprite content, and the cutscene will finish before the content has shown its last phases.
- Chain validation allows a connector without a condition between phases and several "Phase" links in a step: the phases then pass within one pass, and the regular content handler does not show the intermediate one.
- A chain stopped by an exception stays in the `LogicChainsSystem` registry: there is no way to remove it. It does not affect shows.
- A rejected second orchestrator does not take the slot by itself when the first one leaves — it has to be enabled again.
- If the orchestrator is disabled from its own `Loading` or `On` state, its switcher may stay in that state until it is enabled again: the switcher does not accept a nested `Off` and logs an error about it.

---

## Edge cases

| Situation | Behaviour |
|---|---|
| `Open` with an empty key | `Debug.LogError`, `OnShowFinished(key)` |
| `Open` while the slot is empty | `Debug.LogError` at once, `OnShowFinished(key)` at the end of the frame. An orchestrator enabled later in the same frame does not get this open |
| An `OnShowFinished` subscriber opens a cutscene on every event | at most one open per frame: a refusal produced by the handler is raised on the next pass |
| Key not in the index, prefab not set, chain not in the database | `Debug.LogError`, `OnShowFinished(key)` |
| The chain failed validation | `Debug.LogError` with the reason, `OnShowFinished(key)` |
| An exception while opening (a corrupted chain record, the database driver) | the exception is logged, `OnShowFinished(key)` |
| `Open` while a cutscene is running | the previous one flies through to the end (`OnShowFinished`), then the new one starts loading in the same pass |
| `Open` while the previous one is loading | the load is cancelled (`OnShowFinished`), then the new one starts loading |
| Two `Open` calls in one frame | the last one opens; the first gets `OnShowFinished` |
| `Open`, then `Skip` in one frame | the open is cancelled (`OnShowFinished`); the running cutscene flies through |
| `Open` from a show event handler | remembered; executes in the same pass if the open step is still ahead, otherwise on the next frame |
| The orchestrator is disabled while it is starting the show (from the content's `Awake`, its handlers, switcher states) | the start stops, the chain is not run, the content is removed, `OnShowFinished(key)` |
| Prefab load failure, empty prefab, no `DataStorage` on the root | `Debug.LogError`, content unloaded, `OnShowFinished(key)` |
| An exception while starting the show (content data binding, a link before the first wait) | the exception is logged, the show is closed, `OnShowFinished(key)`; `OnShowStarted` is not raised |
| A load completes after being cancelled, including with a failure | the result is discarded, the running show is not touched |
| `Next` earlier than `nextDelay` after entering the phase | not accepted |
| `Next` / `Skip` with no open cutscene, `Next` before the first phase has started | nothing |
| `Next` was accepted, and in the same pass the cutscene was replaced by another | the request is dropped, the new cutscene does not skip its first phase |
| Several `Next` calls in one frame from any sources | one transition |
| `Next(phase)` with a phase the chain has already left | not accepted |
| `CutsceneContentHandler.Next()` from disabled content | ignored |
| A link after `CutsceneComplete` in the last step | executes while the show is still open: the show closes when the chain returns control; during a fly-through the link sees `IsSkipping` |
| `Skip` during loading | the load is cancelled, `OnShowFinished(key)` |
| `Skip` twice in one frame | one skip |
| An exception in a show event subscriber | logged; the other subscribers get the event; after `OnShowOpening` or `OnShowStarted` the cutscene is interrupted on the next frame |
| An exception in a chain link in the middle of a show | the chain is stopped; the show is closed with an error in the log, `OnShowFinished(key)` |
| Orchestrator disabled mid-show | fly-through to the end at the moment of disabling, unload, slot released |
| Orchestrator disabled by a chain link | the remaining steps run to the final one in fly-through mode; the show closes when the chain returns control |
| Orchestrator disabled and enabled again within one pass | the show reaches the final step by fly-through, the slot is the orchestrator's again |
| Orchestrator disabled by the content while it is being unloaded | the show is already closing; no second close |
| Orchestrator disabled with an open request not yet executed | `OnShowFinished(key)` |
| A second orchestrator | `Debug.LogError`, not registered |
| More phases than content switcher states | an exception in the log in the editor; `Debug.LogError` in a build; the state does not change, the show continues |
| A new phase earlier than `switchDelay` (`CutsceneSpriteContentHandler`) | the state is set once the delay expires; several phases go one by one, each with its own delay |
| The cutscene completes while the content waits out the delay | the content is unloaded, the postponed switch does not happen |
| Fly-through did not reach the final step | `Debug.LogError`, the show is closed forcibly |

---

## Public API

```csharp
namespace Vortex.Unity.CutsceneSystem.Bus
{
    public static class CutsceneBus
    {
        public static CutsceneControl Control { get; }
        public static bool IsSkipping { get; }
        public static bool IsActive { get; }      // an open is accepted, the content is loading or the show is running

        public static event Action<string> OnShowOpening;    // the parameter is the cutscene key
        public static event Action<string> OnShowStarted;    // the parameter is the cutscene key
        public static event Action<string> OnShowFinished;   // the parameter is the cutscene key

        public static void Open(string key);      // the key is the LogicChain record id
        public static void Next();                // external command, with debounce
        public static void Next(int phase);       // auto-transition from the content; phase — the phase shown
        public static void Skip();
    }
}

namespace Vortex.Unity.CutsceneSystem.Models
{
    public class CutsceneControl : IReactiveData
    {
        public IntData Phase { get; }             // 0 — no phases yet
        public event Action OnUpdateData;         // the phase number has changed
    }

    public enum CutsceneStageState { Off, Loading, On }
}

namespace Vortex.Unity.CutsceneSystem.LogicChains
{
    public class CutscenePhase : UnityLogicAction { }
    public class CutsceneComplete : UnityLogicAction { }
    public class CutsceneReadyCondition : UnityCondition { }
}

namespace Vortex.Unity.CutsceneSystem.Handlers
{
    public class CutsceneOrchestrator : MonoBehaviour { }     // configured in the inspector

    public class CutsceneContentHandler : DataStorageView<CutsceneControl>
    {
        public void Next();                                   // auto-transition
        protected virtual void ShowPhase(int phase);          // show a phase; a subclass may postpone it
        protected void SetState(int phase);                   // put the switcher into the phase state
    }

    public class CutsceneSpriteContentHandler : CutsceneContentHandler { }   // switchDelay — in the inspector
}
```

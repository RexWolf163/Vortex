# TimeSystem

Deferred call dispatcher and application time source.

## Purpose

Centralized deferred call management, same-type action batching, managed timers.

- Deferred action invocation after a specified interval
- Same-type call accumulation (batching)
- Managed timers with pause and cancellation
- Per-frame time caching (`Date`, `Time`, `Timestamp`)
- Time conversion (Unix seconds, ticks → `DateTime`)

Out of scope: coroutines, animations, interpolation (see `TweenerSystem`).

## Dependencies

- `UnityEngine` — `MonoBehaviour`, `DontDestroyOnLoad`
- `Sirenix.OdinInspector` — debug display of queues

---

## TimeController

Central deferred call dispatcher. `MonoBehaviour`, auto-created via `[RuntimeInitializeOnLoadMethod]`.

### Architecture

```
TimeController (MonoBehaviour, auto-create)
├── _queue           — Dictionary<object, QueuedAction>  (with owner, overwrites)
├── _anonymousQueue  — List<QueuedAction>                (no owner, FIFO)
├── NextWaveQueue    — Dictionary<object, Action>         (Accumulate)
├── ReadyQueue       — List<QueuedAction>                  (current-wave snapshot: Owner + Action)
├── HotRemovedOwners — HashSet<object>                     (cancellations arriving during the wave)
├── RemoveBuffer     — List<object>                        (buffer)
└── RemoveIndices    — List<int>                           (buffer)
```

Processing cycle:

```
Update()      → TimeSync?.Invoke()
LateUpdate()  → SetTimeValue()
              → RunNextWave()          // Accumulate batch
              → CheckQueue()           // every 0.1s (StepTime)
```

### Contract

**Input:**
- `Action` + optional delay (`float stepSecs`) + optional owner (`T owner where T : class`)

**Output:**
- Action invocation after delay expires
- Cached time: `Date`, `Time`, `Timestamp`

**Guarantees:**
- Anonymous (no owner): FIFO order, cannot be cancelled
- With owner: overwrites previous call from the same owner
- `Accumulate`: executes once per `LateUpdate`, keeps the last action
- Exception in one callback does not block others (`try/catch` + `Debug.LogError`)
- `_nextTimer` optimization: `CheckQueue` is skipped when no actions are ready
- `Call(null, owner)` — removes pending call for owner from the queue
- **Reentrant cancellation during a wave:** `RemoveCall(owner)` invoked from an action that is executing right now in the current wave correctly suppresses the not-yet-executed actions of the same owner from the `ReadyQueue` snapshot. While the wave runs (`_inWave`), the cancelled owner goes into `HotRemovedOwners`, and its action will not fire from the already-taken snapshot. Other actions of the wave run normally. Without this mechanism, a cancellation "from inside the wave" was late (the snapshot was already taken) and the action fired despite `RemoveCall`.

**Limitations:**
- Granularity ~100ms (`StepTime`). When `stepSecs <= 0`, check is forced on the current `LateUpdate`
- Owner is constrained to `where T : class` — value types are rejected at compile time

### Usage

#### Deferred calls

```csharp
// No owner (FIFO, cannot cancel)
TimeController.Call(() => Refresh());

// With delay, no owner
TimeController.Call(() => Refresh(), 0.5f);

// With owner (overwrites, cancellable)
TimeController.Call(() => Save(), this);
TimeController.Call(() => Save(), 2f, this);

// Cancel by owner
TimeController.RemoveCall(this);
```

> ⚠️ `Call` runs on wall-clock (`DateTime.UtcNow`) and is **unaware of game pause**:
> a scheduled call fires even while the game is paused or the app is unfocused
> (project pause is state-based, `Time.timeScale` is never zeroed). For deferred actions
> that must "freeze" together with the game, use `Timer` —
> see [Pattern: pausable deferred action](#pattern-pausable-deferred-action).

#### Accumulation

```csharp
// Multiple calls per frame — only the last one executes
TimeController.Accumulate(() => Sync(), this);
TimeController.Accumulate(() => Sync(), this);
// Sync() is called once in the next LateUpdate
```

#### Time

```csharp
DateTime now    = TimeController.Date;        // UtcNow, cached per frame
double seconds  = TimeController.Time;         // seconds, 0.01 precision
long unixMs     = TimeController.Timestamp;    // Unix milliseconds

DateTime local  = TimeController.DateFromSeconds(unixSec);
DateTime local  = TimeController.DateFromTicks(ticks);
```

#### Per-frame callbacks (FixedUpdate)

In addition to deferred calls, `TimeController` keeps a `FixUpdateIndex` registry of callbacks invoked every frame on `FixedUpdate`:

```csharp
// Register a callback invoked every FixedUpdate
TimeController.AddCallback(Tick);

// Remove from the registry
TimeController.RemoveCallback(Tick);
```

Callbacks are invoked in `FixedUpdate` in registration order; an exception in one callback is isolated (`try/catch` + `Debug.LogException`) and does not block the rest.

### Edge Cases

- **StepTime (0.1s):** `CheckQueue` runs every ~100ms. When `stepSecs <= 0`, check is forced on the current `LateUpdate`.
- **Buffers:** `ReadyQueue`, `RemoveBuffer`, `RemoveIndices`, `HotRemovedOwners` — static, reusable, no GC pressure.
- **`RemoveCall` from an executing action:** safe. Cancelling an owner during the wave is guaranteed to suppress its not-yet-executed actions in the current snapshot (via `HotRemovedOwners`) and to remove it from `NextWaveQueue`.
- **Timestamp when Date.Year <= 1:** returns `0` (guard against `DateTimeOffset` on uninitialized date).

---

## Timer

Managed timer with pause support. Automatically registers with `TimeController.Call` using `owner = this` on creation.

### Architecture

```
Timer (class)
├── End        — DateTime   (trigger moment, recalculated on Resume)
├── Duration   — TimeSpan   (full duration, immutable)
├── Remains    — TimeSpan   (remaining, from DateTime.UtcNow; frozen when paused)
├── IsComplete — bool       (true after trigger)
├── IsPaused   — bool       (true between SetPause and Resume)
└── → TimeController.Call(CallAction, seconds, this)
```

### Contract

**Input:**
- Duration (`float` seconds, `TimeSpan`, or `DateTime` target moment) + callback `Action`

**Output:**
- Callback invocation on expiry
- State: `Remains`, `IsComplete`, `IsPaused`, `GetTimePassed()`

**Guarantees:**
- `SetPause`/`Resume` — no-op when `IsComplete`, already paused, or not paused
- `Remains` is computed from `DateTime.UtcNow` (real time, not frame cache)
- Callback is invoked through `TimeController` — exception isolation

**Limitations:**
- No cancellation method. To cancel: `SetPause()` without `Resume()`
- Callback precision is determined by `TimeController.StepTime` (~100ms)

### Usage

```csharp
// Creation
var timer = new Timer(5f, onComplete);
var timer = new Timer(TimeSpan.FromMinutes(1), onComplete);
var timer = new Timer(targetDateTime, onComplete);

// State
TimeSpan left   = timer.Remains;
TimeSpan passed = timer.GetTimePassed();

// Pause / resume
timer.SetPause();   // RemoveCall(this), freeze Remains, IsPaused = true
timer.Resume();     // End = UtcNow + Remains, re-register with Call
```

Lifecycle:

```
new Timer(5f, cb)
  → End = UtcNow + 5s
  → TimeController.Call(CallAction, 5f, this)
  → ... 5 seconds ...
  → CallAction(): IsComplete = true, cb?.Invoke()
```

```
SetPause()
  → TimeController.RemoveCall(this)
  → _remains = End - UtcNow  (via property read before IsPaused = true)
  → IsPaused = true

Resume()
  → End = UtcNow + _remains
  → IsPaused = false
  → TimeController.Call(CallAction, (float)Remains.TotalSeconds, this)
```

### Edge Cases

- **Background:** `DateTime.UtcNow` keeps ticking, `LateUpdate` stops. `Remains` is correct after return; callback fires on the first `CheckQueue`.
- **SetPause — operation order:** freezes `Remains` via property read before setting `IsPaused = true`. After `IsPaused = true`, the getter returns the cached value.

---

## Pattern: pausable deferred action

`Timer` is not just a "timer with progress" — it is a **pause-safe replacement for
`TimeController.Call`** for deferred gameplay and visual steps.

### Choosing the tool

| Scenario | Tool |
|---|---|
| Defer to end of frame / batching | `Call` / `Accumulate` |
| Deferred call indifferent to pause (app-level, analytics, non-gameplay audio) | `Call(action, delay, owner)` |
| Deferred **gameplay/visual** step that must freeze on pause (hit resolution, phase change, animation return, hiding a voice line) | `Timer` + pause by state |
| Need progress / remaining time (sliders, countdowns) | `Timer` (`Remains`, `GetTimePassed`) |

The root of the difference: project pause is a **state** (`MiniGameStates.Paused`),
not `Time.timeScale = 0`. The `TimeController` queue ticks on wall-clock and keeps firing
during pause. `Timer` supports `SetPause`/`Resume` with frozen remaining time — but **only
when explicitly driven from a state-change handler**. A created-and-forgotten `Timer`
behaves just like a plain `Call`.

### Canonical snippet

```csharp
private Timer _actionTimer;

// Launch a deferred step (instead of TimeController.Call(cb, delay, this))
_actionTimer?.SetPause();                    // cancel the previous one, if any
_actionTimer = new Timer(delay, Callback);   // the constructor starts counting immediately!

// Game state change handler (OnGameStateChanged / OnStateChanged)
private void OnStateChanged(MiniGameStates state)
{
    switch (state)
    {
        case MiniGameStates.Play:
            _actionTimer?.Resume();
            break;
        case MiniGameStates.Paused:
            _actionTimer?.SetPause();
            break;
    }
}

// Cleanup (DeInit / OnDisable / Unbind)
_actionTimer?.SetPause();   // no cancel method — pause without Resume IS the cancel
_actionTimer = null;
```

### Pitfalls

- **The `Timer` constructor starts counting immediately.** "Create while paused" is not
  possible — only create from code guaranteed to run in `Play` (gameplay event handlers),
  or pause it right away.
- **One timer — one action.** For sequential step chains a single field is enough
  (a new step cancels the previous one). For independent parallel deadlines —
  keep a list and check against game time (see below).
- **If there is "game time" (audio track, ticking counter) — it beats a timer.**
  A deadline bound to the track freezes with it for free: store the target
  (`targetTime`) and compare in the time-tick handler that is already guarded
  by the "game is running" state.

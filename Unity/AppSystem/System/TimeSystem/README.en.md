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
├── ReadyQueue       — List<Action>                       (buffer)
├── RemoveBuffer     — List<object>                       (buffer)
└── RemoveIndices    — List<int>                          (buffer)
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

### Edge Cases

- **StepTime (0.1s):** `CheckQueue` runs every ~100ms. When `stepSecs <= 0`, check is forced on the current `LateUpdate`.
- **Buffers:** `ReadyQueue`, `RemoveBuffer`, `RemoveIndices` — static, reusable, no GC pressure.
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

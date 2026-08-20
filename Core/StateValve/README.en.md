# StateValve (Core)

**Namespace:** `Vortex.Core.StateValve`
**Assembly:** `ru.vortex.core.statevalve`

## Purpose

A reactive state valve — a primitive that folds N named boolean gates ("keys") into a single boolean result via a fold mode + inversion. Domain-neutral: it deals in "open/closed"; the meaning ("closed = paused") is supplied by whoever applies it.

It is a **primitive, not a system**: instantiated with `new` (like `ReactiveValue`, `DateTimeTimer`), with no bus, driver, controller, or presets.

- N named "open/closed" gates
- Fold into a single result: `And` / `Or` / `Xor`
- Result inversion (NOT)
- Reactive output `State : BoolData` under an owner-lock
- fail-fast on an empty key

## Dependencies

- `Vortex.Core.Extensions.ReactiveValues` — `BoolData`

## Model

```
StateValve
├── State : BoolData                          ← reactive result (owner-lock), read/subscribe only
├── Keys  : IReadOnlyDictionary<string,bool>  ← gates, for debugging
├── Open(string) / Close(string)
└── ctor(ValveMode mode = And, bool invert = false)

ValveMode { And, Or, Xor }
```

## Fold (truth tables, when keys are present)

| Mode | Open if |
|---|---|
| `And` | ALL are open |
| `Or`  | AT LEAST ONE is open |
| `Xor` | EXACTLY ONE is open (zero or ≥2 open → closed) |

Empty set → **open** (the valve was never closed) — a valve convention, uniform across all modes, before inversion. Result: `final = invert ? !raw : raw`.

## Contract

- `State` is written only by the recompute after `Open/Close` (owner-lock; an external `Set` is impossible).
- An empty/`null` key → `ArgumentException` (fail-fast, caller bug).
- Releasing a gate = `Open(id)` — the key stays in the set, open; there is no key removal.

## Re-entrancy (single-threaded)

The valve adds **no guards, deliberately**.

- **Benign nesting** (a `State` subscriber calls `Open/Close` from its callback) resolves fine: `BoolData` dedups (the event fires only on a real change), last-write-wins, a converging chain settles.
- **Oscillating** re-entrancy (a subscriber toggles a key so the result flips back) is a **caller composition error**: it surfaces as a stack overflow — the correct fail-fast signal of a feedback loop. Contract: do not wire a self-sustaining loop.

## Usage

```csharp
var valve = new StateValve(ValveMode.And);        // "running" = all keys open
valve.State.OnUpdate += open => SetPaused(!open);

valve.Close("tutor");      // closed → State = false
valve.Close("countdown");  // another holds it
valve.Open("tutor");       // released, but countdown still holds → still closed
valve.Open("countdown");   // all open → State = true
```

The Unity wrapper (`StateValveHandler`, package `ru.vortex.unity.statevalve`) adds inspector configuration, a whitelist key filter, and loose binding via `IDataStorage`.

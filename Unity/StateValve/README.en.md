# StateValve (Unity)

**Namespace:** `Vortex.Unity.StateValve`
**Assembly:** `ru.vortex.unity.statevalve`

## Purpose

A Unity wrapper over the Core `StateValve`: a `MonoBehaviour` handler with inspector configuration, a whitelist filter for incoming keys, and loose binding via `IDataStorage`. The primitive itself (`StateValve`, `ValveMode`, folds, truth tables, re-entrancy) is documented in the Core README.

## Dependencies

- `ru.vortex.core.statevalve` — `StateValve`, `ValveMode`
- `ru.vortex.extensions` — `BoolData`
- `ru.vortex.system` — `IDataStorage` / `IDataSource`

## `StateValveHandler : MonoBehaviour, IStateValve, IDataStorage`

**Inspector fields:** `mode : ValveMode`, `invert : bool`, `whiteList : string[]`.

- **`Awake`** — creates `StateValve(mode, invert)`, raises `OnUpdateLink` (references ready).
- **whitelist filter** — a non-empty list: an incoming `Open/Close` with a key not in the list is rejected with `Debug.LogError` (a wiring error must not be hidden). An empty list — no filter. An empty/`null` key passes through to the core → fail-fast.
- **`GetWhiteList()`** → the whitelist — the dropdown source for locking views.
- **`IStateValve`** — `Open` / `Close` / `State` / `GetWhiteList`, the reference point for producers and consumers.
- **`IDataStorage`** — loose binding: `GetData<IStateValve>()` → the handler itself (for producers), `GetData<BoolData>()` → `State` (for consumers). `OnUpdateLink` — once in `Awake` (link-level: references ready; the `State` value is observed via its own `OnUpdate`).
- **Inspector debug** — the runtime `Keys` list (`[ShowInInspector, ReadOnly]`, Play Mode only).

## Application: a pause valve (example, outside the package)

The package is neutral; pause integration lives at the project level.

- One `StateValveHandler` per system, `And` mode: "running" = all keys open; `State == closed` → paused.
- **Holder producers** (tutorial, countdown component) call `Close(key)` on entry and `Open(key)` on exit.
- **The consumer** subscribes to `State` → stays paused while any key is closed.
- **A gapless "tutorial → countdown" handoff:** call order does not matter — while any key is closed, the result is closed.

# Vortex Core

**Namespace:** `Vortex.Core.*`
**Platform:** pure C# + UniTask
**Files:** ~89 (.cs), 16 systems

---

## What Core Is For

Core is the foundation of Vortex. Everything here knows nothing about Unity or any specific project. No `MonoBehaviour`, no `UnityEngine`. Pure C# that describes **how the application is structured** — without being tied to where it runs.

The idea is straightforward: if tomorrow the logic needs to move to a server, a console app, or a different engine — Core migrates unchanged. Everything platform-specific lives above, in the Unity layer.

In practice, Core solves three problems:

1. **Defines contracts.** Interfaces like `IProcess`, `ISaveable`, `ISystemDriver` are promises that the platform layer must fulfill. Core says "what is needed", Unity answers "how to do it".

2. **Implements domain-neutral logic.** The data bus (`Database`), loader (`Loader`), saves (`SaveController`), settings (`Settings`), UI provider (`UIProvider`), audio (`AudioController`), logic chains (`LogicChains`), parameter maps (`ParameterMaps`) — all of this works at the abstraction level, without binding to specific assets or an engine.

3. **Provides foundational primitives.** `Singleton<T>`, `SystemController<T, TD>`, `ReactiveValue<T>`, `ActionExt` — building blocks from which systems are assembled at every level.

---

## About UniTask

Core has exactly one external dependency beyond the .NET standard library — **UniTask** (`Cysharp.Threading.Tasks`).

It is used in four files:
- `IProcess.RunAsync()` — async loading contract
- `ISaveable.GetSaveData()` / `OnLoad()` — save/load contract
- `Loader.Run()` / `Loading()` — load orchestration
- `DatabaseExtSave` — record serialization with `await UniTask.Yield()` for batching

Why UniTask instead of `System.Threading.Tasks.Task`? A compromise. UniTask provides compatibility with Unity WebGL builds, where the standard `Task` doesn't work correctly due to the browser's single-threaded model. Yet the API is nearly identical: `async UniTask` instead of `async Task`, same `CancellationToken`, same `await`.

If a project doesn't need WebGL and requires a strictly .NET-dependent layer — the replacement is mechanical: `UniTask` → `Task`, `UniTask<T>` → `Task<T>`, `UniTask.Yield()` → `Task.Yield()`, `UniTask.CompletedTask` → `Task.CompletedTask`. Four files, find-and-replace. No architectural restructuring.

---

## Architectural Principles

### Bus Instead of DI

Vortex doesn't use DI containers. Instead — a static data bus `Database` that stores `Dictionary<GUID, Record>`. Any system retrieves data via `Database.GetRecord<T>(id)`, not through constructors or injection.

This is a deliberate choice: the bus is simpler to debug (single access point), requires no binding configuration, and naturally fits the Unity model where objects are created by the engine, not by the programmer.

### Controller Owns the Logic

Data lives in models. Decisions are made by controllers. UI only displays state and reports user actions — but never modifies data directly.

### SystemController + Singleton

`SystemController<T, TD>` inherits `Singleton<T>` and adds a driver contract: the platform layer registers an `ISystemDriver`, Core manages the lifecycle. `DriversGenericList.WhiteList` validates allowed driver types at registration time.

### ReactiveValue

`ReactiveValue<T>` — a wrapper around a value with an `OnUpdate` event. Specializations `IntData`, `BoolData`, `FloatData`, `StringData` provide implicit operators for transparent usage (`IntData count = 5;`). Models are built from reactive fields — subscribers receive notifications without polling.

### IProcess and Topological Loading

Each module implements `IProcess`: `RunAsync()` for loading and `WaitingFor()` for declaring dependencies. `Loader` automatically builds the load order — an analogue of topological sort. Cyclic dependencies are detected and result in `App.Exit()`.

---

## Contents

| System | What It Does | Key Types |
|--------|-------------|-----------|
| **System** | Base abstractions | `Singleton<T>`, `SystemController<T,TD>`, `ReactiveValue<T>`, `IProcess`, `DateTimeTimer` |
| **DatabaseSystem** | Data bus | `Database`, `Record` |
| **AppSystem** | Lifecycle | `App` (static), `AppModel`, `AppStates` |
| **LoaderSystem** | Loader | `Loader` — registration, topological sort, `async Run()` |
| **SaveSystem** | Saves | `SaveController`, `ISaveable`, `SaveData`, `SaveFolder` |
| **SettingsSystem** | Settings | `Settings`, `SettingsModel` (partial, extended by other systems) |
| **UIProviderSystem** | UI management | `UIProvider`, `UserInterfaceData`, `UserInterfaceCondition` |
| **AudioSystem** | Audio | `AudioController`, `AudioSample`, `MusicSample`, `SoundSample` |
| **LocalizationSystem** | Localization | `Localization`, `StringExt` |
| **LoggerSystem** | Logging | `Log`, `LogData`, `LogLevel` |
| **LogicChainsSystem** | Logic chains | `LogicChains`, `LogicChain`, `ChainStep`, `Connector` |
| **MappedParametersSystem** | Parameter maps | `ParameterMaps`, `IMappedModel`, `GenericParameter` |
| **ComplexModelSystem** | Composite models | `ComplexModel` |
| **DebugSystem** | Debugging | `SettingsModelExtDebug` — partial extension of `SettingsModel` |
| **Extensions** | Utilities | `ActionExt`, `SerializeController`, `Crypto`, `ListExt` |

---

## Partial Classes

Large systems in Core are split across files via `partial`:

- `Database` — main class + `DatabaseExtSave` (`ISaveable` implementation)
- `UIProvider` — main class + `UIProviderExtRegister` + `UIProviderExtEvents`
- `App` — main class + `AppExtEvents`
- `SettingsModel` — empty partial, extended by other systems (`SettingsModelExtDebug`, `SettingsModelExtUnity` in the Unity layer)

This is neither inheritance nor composition — it's splitting a single class by topic for readability.

---

## Layer Boundaries

Core **does not**:
- Load files from disk or know about the file system
- Create GameObjects or manage scenes
- Render UI or play audio
- Access the network
- Depend on Unity API (except UniTask, see above)

Core **defines**:
- How data is stored and retrieved (Database, Record, GUID)
- How systems initialize and in what order (Loader, IProcess)
- How state is saved and restored (SaveController, ISaveable)
- How UI decides what to show (UIProvider, Conditions)
- How settings reach the system (Settings, SettingsModel)
- How components react to changes (ReactiveValue, ActionExt)

Everything that requires a specific platform is delegated through driver interfaces (`ISystemDriver`, `IDriver`) to the Unity layer.

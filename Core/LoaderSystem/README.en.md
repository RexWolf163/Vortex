# LoaderSystem (Core)

**Namespace:** `Vortex.Core.LoaderSystem.Bus`
**Assembly:** `ru.vortex.apploader`
**Platform:** .NET Standard 2.1+

---

## Purpose

Centralized asynchronous orchestration of application component loading.

Capabilities:

- Component registration (`IProcess`) before loading starts
- Automatic ordering by dependencies via `WaitingFor()`
- Loading progress: current step, queue size, module data (`ProcessData`)
- Single module loading via `RunAlone()`
- Cancellation via `CancellationToken` on application exit
- Lifecycle integration: `AppStates.Starting` → `AppStates.Running`

Out of scope:

- Component initialization logic (orchestration only)
- Parallel loading of independent components
- State rollback on cancellation

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.AppSystem` | `App.SetState()`, `App.OnExit`, `AppStates` |
| `Vortex.Core.SettingsSystem` | `Settings.Data().DebugMode` — debug logging |
| `Vortex.Core.LoggerSystem` | `Log.Print()` — process logging |
| `Vortex.Core.System.ProcessInfo` | `IProcess`, `ProcessData` |
| `Vortex.Core.System.Abstractions` | `ISystemController` — `IsInit` check for external controllers |
| `Cysharp.Threading.Tasks` | `UniTask` |

---

## Architecture

Static class `Loader`. Single entry point for loading management.

### Loading flow

```
Register(IProcess) × N    ← before start
        ↓
      Run()                ← external trigger (LoaderStarter)
        ↓
App.SetState(Starting)
        ↓
   ┌─ Loading loop ─────────────────────────────┐
   │  1. Find process whose dependencies         │
   │     (WaitingFor) are already loaded          │
   │  2. Call RunAsync(token)                     │
   │  3. Add to loaded, update progress           │
   │  4. Repeat until queue is empty              │
   └─────────────────────────────────────────────┘
        ↓
OnComplete → App.SetState(Running)
```

### Order resolution

On each iteration the loader finds the first `IProcess` whose `WaitingFor()` types are all satisfied. Dependency check:

- Type is in `loaded` (already loaded in current session) → OK
- Type implements `ISystemController` and its `IsInit == true` → OK (external controller, initialized outside Loader)
- Otherwise → dependency not satisfied

If no process can be selected — cyclic or incorrect dependency. Error is logged, `App.Exit()` is called.

### IProcess

| Method | Description |
|--------|-------------|
| `WaitingFor()` | `Type[]` — types that must be loaded before this process. `null` or empty — no dependencies |
| `RunAsync(CancellationToken)` | Async initialization. Called after dependencies are satisfied |
| `GetProcessInfo()` | `ProcessData` for progress indication: name, progress, size. `null` → default values |

### ProcessData

Open fields without encapsulation (access optimization):

| Field | Type | Description |
|-------|------|-------------|
| `Name` | `string` | Process name (displayed in UI) |
| `Progress` | `int` | Current step within the process |
| `Size` | `int` | Total number of steps |

---

## Contract

### Input

- `Loader.Register(IProcess)` — before `Run()`
- `Loader.Run()` — called by external trigger (once)

### Output

- All registered `IProcess` instances called via `RunAsync()` in correct order
- `App.SetState(AppStates.Running)` after completion
- Events: `OnLoad` (start), `OnComplete` (finish)

### API

| Method | Description |
|--------|-------------|
| `Register(IProcess)` | Register module. Duplicate by type — `Warning`, skip |
| `UnRegister<T>()` / `UnRegister(Type)` / `UnRegister(IProcess)` | Remove from registration |
| `Run()` | Start full loading. Protected from repeated calls |
| `RunAlone(IProcess)` | Load a single module outside the main queue |
| `GetProgress()` | Current step number (1-based) |
| `GetSize()` | Total number of modules |
| `GetCurrentLoadingData()` | `ProcessData` of the currently loading module |

### Events

| Event | Timing |
|-------|--------|
| `OnLoad` | Before the loading loop starts |
| `OnComplete` | After all modules complete |

### Guarantees

- Loading executes once per session (`_isRunning`)
- Order is fully determined by `WaitingFor()`
- Cyclic dependencies are detected and terminate the application
- Each step is logged via `Log.Print`

### Limitations

| Limitation | Reason |
|------------|--------|
| Strictly sequential loading | No parallelism for independent modules |
| Cancellation does not roll back state | Already loaded modules remain in memory |
| `_isRunning` is never reset | Repeated `Run()` impossible without restart |
| `CancellationTokenSource` is readonly static | Single token for the application lifetime |
| `Queue` is `Dictionary<Type, IProcess>` | One instance per type |

---

## Usage

### Registration in a driver (Layer 2)

```csharp
[RuntimeInitializeOnLoadMethod]
private static void Init()
{
    if (MyService.SetDriver(Instance))
        Loader.Register(Instance);
}
```

### IProcess implementation

```csharp
public class AudioDriver : IProcess
{
    public Type[] WaitingFor() => new[] { typeof(DatabaseDriver), typeof(SettingsDriver) };

    public async UniTask RunAsync(CancellationToken token)
    {
        // Initialize after Database and Settings
        await LoadAudioBank(token);
    }

    public ProcessData GetProcessInfo() => _processData;
}
```

### Single module loading

```csharp
await Loader.RunAlone(hotReloadModule);
```

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| Cyclic dependency (A → B → A) | `LogLevel.Error`, `App.Exit()` |
| `WaitingFor()` contains unregistered type | `LogLevel.Error`; if type is `ISystemController`, `IsInit` is checked |
| `WaitingFor()` returns `null` | Treated as empty array — no dependencies |
| Duplicate registration of the same type | `LogLevel.Warning`, skip |
| `CancellationToken` cancelled | Loop breaks without rollback; `AppStates` remains `Starting` |
| Repeated `Run()` | Ignored |
| `GetProcessInfo()` returns `null` | Default `ProcessData` used |

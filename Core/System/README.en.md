# System (Core)

**Namespace:** `Vortex.Core.System.Abstractions`, `Vortex.Core.System.Abstractions.ReactiveValues`, `Vortex.Core.System.Abstractions.Timers`, `Vortex.Core.System.Abstractions.SystemControllers`, `Vortex.Core.System.ProcessInfo`, `Vortex.Core.System.Enums`, `Vortex.Core.System`
**Assembly:** `ru.vortex.system`
**Platform:** .NET Standard 2.1+

---

## Purpose

Foundational abstractions package of the framework. Defines base patterns used by all other systems: singleton, system controller with driver architecture, reactive values, process interface for async loading, and calendar-based timer.

Capabilities:

- `Singleton<T>` — generic singleton with lazy initialization
- `SystemController<T, TD>` — controller with pluggable driver and initialization queue
- `ISystemDriver` — interface for platform-dependent drivers
- `DriversGenericList` — white-list of valid controller → driver pairs
- `ReactiveValue<T>` — value wrapper with change events (`IntData`, `FloatData`, `BoolData`, `StringData`)
- `IProcess` / `ProcessData` — interface and data for async processes in `Loader`
- `DateTimeTimer` — `DateTime`-based timer, works offline
- `SystemModel` — base class for data models
- `IDataStorage` — data storage interface with update event
- `AppStates` — application state enumeration

Out of scope:

- Concrete driver implementations — Unity layer
- Process loader (`Loader`) — `LoaderSystem`
- Concrete system controllers — separate packages

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| UniTask | `UniTask`, `CancellationToken` (in `IProcess`) |

This package has no dependencies on other Vortex packages. It is the root dependency for the entire framework.

---

## Architecture

### Singleton\<T\>

```
Singleton<T> where T : Singleton<T>, new()
  ├── Instance → T                           ← lazy, new T() + OnInstantiate()
  ├── Dispose() → OnDispose(), _instance = null
  ├── OnInstantiate() (virtual)
  └── OnDispose() (virtual)
```

Generic singleton with lazy initialization. On first `Instance` access, creates an instance via `new T()` and calls `OnInstantiate()`. `Dispose()` is protected, calls `OnDispose()` and nullifies the instance.

### SystemController\<T, TD\>

```
SystemController<T, TD> : Singleton<T>, ISystemController
  where T : SystemController<T, TD>, new()
  where TD : ISystemDriver

  ├── IsInit: bool (static)
  ├── OnInit: Action (static event)          ← initialization wait queue
  ├── Driver: TD (static, protected)
  ├── SetDriver(TD) → bool
  │    ├── WhiteList validation
  │    ├── If driver exists → Disconnect → Destroy → Connect → Init
  │    └── If slot empty → Connect → Init
  ├── HasDriver() → bool
  ├── GetDriverType() → Type
  ├── OnDriverConnect() (abstract)
  ├── OnDriverDisconnect() (abstract)
  └── CallOnInit() → IsInit = true, flush InitQueue
```

Central framework pattern. Each system (Database, Settings, Log, etc.) inherits from `SystemController`. The driver is a platform-dependent implementation (`ISystemDriver`) connected externally.

#### Driver Connection Lifecycle

```
SetDriver(driver)
  ├── driver == null → false
  ├── WhiteList does not contain pair → false
  ├── Driver exists and != driver
  │    ├── Driver.OnInit -= CallOnInit
  │    ├── OnDriverDisconnect()
  │    ├── Driver.Destroy()
  │    ├── Driver = driver
  │    ├── OnDriverConnect()
  │    ├── Driver.OnInit += CallOnInit
  │    ├── Driver.Init()
  │    └── return false (replacement)
  └── Driver is null
       ├── Driver = driver
       ├── OnDriverConnect()
       ├── Driver.OnInit += CallOnInit
       ├── Driver.Init()
       └── return true (initial setup)
```

#### OnInit — Deferred Subscription

`OnInit` is a custom event accessor. Subscribing before initialization (`IsInit == false`) adds the callback to `InitQueue`. Subscribing after initialization invokes the callback immediately. `CallOnInit()` executes all accumulated callbacks and clears the queue.

#### DriversGenericList — WhiteList

Auto-generated file. Contains `Dictionary<string, string>` — pairs of `AssemblyQualifiedName` of system controller → `AssemblyQualifiedName` of allowed driver. `SetDriver` rejects drivers not in the list.

### ISystemDriver

```
ISystemDriver
  ├── OnInit: Action (event)
  ├── Init()                                 ← called after SetDriver
  └── Destroy()                              ← called on disconnect
```

### ReactiveValue\<T\>

```
ReactiveValue<T> : IReactiveData
  ├── Value: T { get; protected set; }
  ├── OnUpdate: Action<T>                    ← typed event
  ├── OnUpdateData: Action                   ← untyped event (IReactiveData)
  ├── Set(T value) → Value = value, fire events
  └── implicit operator T → Value
```

Value wrapper with two events: `OnUpdate` (typed) and `OnUpdateData` (generic). `Set()` always fires both events without checking for change. Implicit operator allows using `ReactiveValue<T>` as `T`.

#### Concrete Implementations

| Class | Type | Constructor |
|-------|------|-------------|
| `IntData` | `ReactiveValue<int>` | `IntData(int value)` |
| `FloatData` | `ReactiveValue<float>` | `FloatData(float value)` |
| `BoolData` | `ReactiveValue<bool>` | `BoolData(bool value)` |
| `StringData` | `ReactiveValue<string>` | `StringData(string value)` |

### IProcess / ProcessData

```
IProcess
  ├── GetProcessInfo() → ProcessData
  ├── RunAsync(CancellationToken) → UniTask
  └── WaitingFor() → Type[]                  ← dependencies (null = none)

ProcessData
  ├── Name: string
  ├── Progress: int
  └── Size: int
```

Interface for async processes registered with `Loader`. `WaitingFor()` returns an array of controller types that must complete loading first. `ProcessData` is mutable (no encapsulation, for performance).

### DateTimeTimer

```
DateTimeTimer
  ├── Start: DateTime
  ├── End: DateTime
  ├── Duration: TimeSpan
  ├── IsComplete() → End <= UtcNow
  ├── IsStarted() → Start <= UtcNow
  ├── GetTimeRemains() → TimeSpan            ← Zero if complete, Duration if not started
  └── GetTimeLeft() → TimeSpan               ← Duration if complete, Zero if not started
```

Timer based on `DateTime.UtcNow`. Works offline — independent of Update loop. Three constructors: `(DateTime end)`, `(TimeSpan duration)`, `(DateTime start, DateTime end)`.

### Auxiliary Types

| Type | Purpose |
|------|---------|
| `SystemModel` | Abstract base class for data models (empty, for typing) |
| `ISystemController` | Marker interface for system controllers |
| `IDataStorage` | Storage interface: `GetData<T>()`, `OnUpdateLink` |
| `AppStates` | Enum: `None`, `Unfocused`, `WaitSettings`, `Starting`, `Running`, `Loading`, `Saving`, `Stopping` |

---

## Contract

### Singleton\<T\>

| Guarantee | Description |
|-----------|-------------|
| Single instance | `_instance` created once |
| Lazy initialization | Instance does not exist until first `Instance` access |
| `Dispose` nullifies | Subsequent `Instance` access creates a new instance |

### SystemController\<T, TD\>

| Guarantee | Description |
|-----------|-------------|
| WhiteList validation | `SetDriver` rejects unregistered drivers |
| `OnInit` — safe subscribe | Subscribing after initialization triggers immediate invocation |
| Driver replacement | Old `Destroy()`, new `Init()` |
| `IsInit` set once | After `CallOnInit` — `true` permanently |

### ReactiveValue\<T\>

| Guarantee | Description |
|-----------|-------------|
| `Set()` always notifies | No change check — both events fire |
| Implicit operator | `ReactiveValue<int> x = ...; int y = x;` — valid |
| `Value` — protected set | Changed only via `Set()` or subclass |

### Constraints

| Constraint | Reason |
|------------|--------|
| `Singleton<T>` is not thread-safe | No `lock` / `volatile` |
| `Set()` without duplication check | May cause unnecessary UI updates |
| `DriversGenericList` is auto-generated | Manual edits will be overwritten |
| `ProcessData` has public fields | Optimization, programmer's responsibility |
| `DateTimeTimer` has no Pause/Resume | `freezePoint` declared but unused |
| `IProcess.WaitingFor()` returns types | Not instances, but `Type[]` for topological sorting |

---

## Usage

### Creating a system controller

```csharp
public interface IMyDriver : ISystemDriver
{
    string LoadConfig();
}

public class MySystem : SystemController<MySystem, IMyDriver>
{
    protected override void OnDriverConnect()
    {
        var config = Driver.LoadConfig();
    }

    protected override void OnDriverDisconnect() { }
}
```

### Using ReactiveValue

```csharp
var health = new IntData(100);
health.OnUpdate += value => Console.WriteLine($"Health: {value}");
health.Set(80);           // → "Health: 80"

int raw = health;          // implicit operator → 80
```

### Using DateTimeTimer

```csharp
var timer = new DateTimeTimer(TimeSpan.FromMinutes(30));

if (!timer.IsComplete())
{
    var remains = timer.GetTimeRemains();
    Console.WriteLine($"Remaining: {remains.Minutes}m {remains.Seconds}s");
}
```

### Deferred system subscription

```csharp
Database.OnInit += () =>
{
    // Called immediately if Database is already initialized
    // Or deferred until initialization
    var record = Database.GetRecord<MyRecord>("id");
};
```

### IProcess for Loader

```csharp
public class MyDriver : Singleton<MyDriver>, IMyDriver, IProcess
{
    public ProcessData GetProcessInfo() => _processData;

    public async UniTask RunAsync(CancellationToken ct)
    {
        _processData = new ProcessData("My System") { Size = 10, Progress = 0 };
        // loading ...
    }

    public Type[] WaitingFor() => new[] { typeof(Database) };
}
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `SetDriver(null)` | Returns `false`, driver unchanged |
| `SetDriver` with type not in WhiteList | Returns `false` |
| `SetDriver` again with same instance | `Driver.Equals(driver)` → skip replacement, `Init()` called |
| Subscribe to `OnInit` after initialization | Callback invoked immediately |
| `ReactiveValue.Set()` with same value | Both events fire (no check) |
| `DateTimeTimer` with `end < start` | Negative `Duration`, `IsComplete()` immediately `true` |
| `DateTimeTimer` — time before `Start` | `GetTimeRemains()` → `Duration`, `GetTimeLeft()` → `Zero` |
| `Dispose()` singleton + re-access `Instance` | New instance via `new T()` |
| `ProcessData.Progress > Size` | No validation, programmer's responsibility |
| `WaitingFor()` → `null` | Process has no dependencies |

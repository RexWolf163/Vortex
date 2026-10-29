# LoggerSystem (Core)

**Namespace:** `Vortex.Core.LoggerSystem.Bus`, `Vortex.Core.LoggerSystem.Model`
**Assembly:** `ru.vortex.logger`
**Platform:** .NET Standard 2.1+

---

## Purpose

Platform-independent logging bus. Routes messages with severity levels through a driver.

Capabilities:

- Three message levels: `Common`, `Warning`, `Error`
- Source binding (`Object source`) per message
- Pluggable driver — platform determines output method

Out of scope:

- Message formatting and output — driver responsibility
- Level-based filtering — driver responsibility
- File logging, server reporting — driver responsibility

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `ISystemDriver` |

---

## Usage

### Printing messages

```csharp
// Via parameters
Log.Print(LogLevel.Common, "Initialized", this);
Log.Print(LogLevel.Warning, "Config not found, using defaults", this);
Log.Print(LogLevel.Error, "Connection failed", "NetworkManager");

// Via struct
var data = new LogData(LogLevel.Error, "Critical failure", this);
Log.Print(data);
```

### Driver implementation

```csharp
public class MyDriver : IDriver
{
    public event Action OnInit;

    public void Print(LogData log)
    {
        Console.WriteLine($"[{log.Level}] [{log.Source}] {log.Message}");
    }

    public void Init() => OnInit?.Invoke();
    public void Destroy() { }
}
```

---

## Architecture

```
Log (SystemController<Log, IDriver>)
  ├── Print(LogData)                  ← delegates to driver
  └── Print(LogLevel, string, Object) ← constructs LogData and delegates

LogData (struct)
  ├── Level: LogLevel
  ├── Message: string
  └── Source: Object

LogLevel (enum)
  ├── Common
  ├── Warning
  └── Error

IDriver : ISystemDriver
  └── Print(LogData)
```

### Log

Static controller. Two `Print` overloads — direct `LogData` pass-through or construction from parameters. Contains no processing logic — pure routing to driver.

### LogData

Immutable struct. `Source` is `System.Object` — can be a string (source name) or an object (driver extracts `GetType().Name`).

### LogLevel

Three severity levels. The driver determines how to render each level.

---

## Contract

### Input

- Driver connects via `Log.SetDriver()`
- `Log.Print()` called with message data

### Output

- Driver receives `LogData` and performs output

### API

| Method | Description |
|--------|-------------|
| `Log.Print(LogData)` | Passes log struct to driver |
| `Log.Print(LogLevel, string, Object)` | Constructs `LogData` and passes to driver |

### IDriver

| Method | Description |
|--------|-------------|
| `Print(LogData)` | Outputs a message. Implementation defines format and channel |

### Constraints

| Constraint | Reason |
|------------|--------|
| Single driver at a time | `SystemController` allows one `IDriver` |
| No level filtering in core | Driver responsibility |
| `Source` is `System.Object` | Flexibility: string or typed object |

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| Driver not connected | `NullReferenceException` on `Print` call |
| `Source` is string | Driver uses string directly as source name |
| `Source` is object | Driver calls `GetType().Name` for source name |
| `Source` is `null` | `NullReferenceException` when driver accesses `Source` |

# LoggerSystem (Unity)

**Namespace:** `Vortex.Unity.LoggerSystem`
**Assembly:** `ru.vortex.unity.logger`
**Platform:** Unity 2021.3+

---

## Purpose

Unity driver for `Log`. Routes `LogData` to `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` with `[source] message` formatting.

Capabilities:

- Automatic driver registration (runtime and editor)
- `LogLevel` → `Debug.Log*` mapping
- Source name formatting in square brackets

Out of scope:

- Log level definitions — Core
- Logging API — Core (`Log.Print`)

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.LoggerSystem` | `Log`, `LogData`, `LogLevel`, `IDriver` |
| Unity Engine | `Debug.Log*`, `RuntimeInitializeOnLoadMethod` |
| Unity Editor | `InitializeOnLoad` (editor extension) |

---

## Architecture

```
LogDriver : IDriver (partial)
  ├── [RuntimeInitializeOnLoadMethod] Run()     ← runtime registration
  ├── Print(LogData)                             ← LogLevel → Debug.Log* mapping
  ├── Init() / Destroy()                         ← ISystemDriver lifecycle
  └── (Editor partial)
       └── [InitializeOnLoad] static LogDriver() ← editor registration
```

### Registration

| Context | Mechanism | Timing |
|---------|-----------|--------|
| Runtime | `[RuntimeInitializeOnLoadMethod]` | Before first frame |
| Editor | `[InitializeOnLoad]` static constructor | On domain load |

The editor extension prints a `Warning`-level confirmation message upon connection.

### Level Mapping

| LogLevel | Unity Method |
|----------|-------------|
| `Common` | `Debug.Log` |
| `Warning` | `Debug.LogWarning` |
| `Error` | `Debug.LogError` |

### Output Format

```
[SourceName] Message text
```

`Source` handling: if `string` — used directly, otherwise — `Source.GetType().Name`.

---

## Contract

### Input

- `LogData` from core via `IDriver.Print()`

### Output

- Message in Unity Console at corresponding level

### Constraints

| Constraint | Reason |
|------------|--------|
| No level filtering | All messages output; filtering via Unity Console |
| `Source = null` | `NullReferenceException` — driver accesses `Source` without null check |
| No file logging | `Debug.Log*` only; file logging requires a separate driver |

---

## Usage

The driver registers automatically. No additional setup required.

```csharp
// Call from anywhere in the project
Log.Print(LogLevel.Common, "Player spawned", this);
Log.Print(LogLevel.Error, "Save file corrupted", "SaveSystem");
```

Result in Unity Console:

```
[PlayerController] Player spawned
[SaveSystem] Save file corrupted    (red)
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| Editor mode | Driver registers via `[InitializeOnLoad]`, logging works |
| Play mode | Driver registers via `[RuntimeInitializeOnLoadMethod]` |
| Double registration | `Log.SetDriver()` returns `false`, no duplicate connection |
| `Source` is `null` | `NullReferenceException` in `Print` |
| `Source` is string `"MySystem"` | Output: `[MySystem] message` |
| `Source` is `MonoBehaviour` object | Output: `[ClassName] message` |

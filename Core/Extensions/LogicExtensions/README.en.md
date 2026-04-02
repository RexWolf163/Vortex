# LogicExtensions (Core)

General-purpose utility extensions for delegates, collections, strings, cryptography, and serialization.

## Purpose

- Safe delegate invocation (`Action`, `Func<bool>`) with null-checks and result aggregation
- Initialization valve (`InitValve`) — thread-safe deferred subscription
- Safe collection operations (`Dictionary`, `List`) with conflict logging
- `DateTime` ↔ Unix time conversion (milliseconds)
- SHA256 hashing and deterministic GUID generation
- String compression via ZipArchive + Base64
- Reflection-based public property copying between objects
- Property-based JSON serialization (experimental)

Out of scope: business logic validation, Unity-dependent utilities, thread-safe collections.

## Dependencies

- `Vortex.Core.LoggerSystem` — `Log`, `LogData`, `LogLevel` (error and warning logging)

## Components

```
LogicExtensions/
├── Actions/
│   ├── ActionExt.cs          → safe delegate invocation
│   └── InitValve.cs          → initialization valve
├── Crypto.cs                 → SHA256, GUID generation
├── DateTimeExtConvert.cs     → Unix time
├── DictionaryExtAdding.cs    → safe dictionary operations
├── IndexFabric.cs            → case-insensitive dictionary factory
├── ListExt.cs                → AddOnce, IndexOfItem
├── ObjectExtDeepClone.cs     → deep object cloning
├── SimpleTypeMarker.cs       → partial marker of platform primitives for DeepCopy
├── StringExtCompress.cs      → string compression
├── StringExtCommons.cs       → IsNullOrWhitespace
├── ObjectExtCopy.cs          → reflection-based property copy
└── SerializationSystem/
    └── SerializeController.cs → property-based JSON
```

---

## ActionExt

Extension methods for `Action` and `Func` delegates enabling safe invocation without null-checks.

### API

| Method | Signature | Description |
|--------|-----------|-------------|
| `Fire` | `Action.Fire()` ... `Action<T1..T5>.Fire(args)` | Null-safe delegate invocation (0–5 parameters) |
| `FireAnd` | `Func<.., bool>.FireAnd(args, returnOnZero)` | AND aggregation: `false` on first `false`. Short-circuits (0–5 parameters) |
| `FireOr` | `Func<.., bool>.FireOr(args, returnOnZero)` | OR aggregation: `true` if at least one returns `true`. All subscribers invoked (0–5 parameters) |
| `Accumulate<T>` | `Func<T[]>.Accumulate()`, `Func<T>.Accumulate()` | Collects results from all subscribers into a single array |
| `FirstNotNull<T>` | `Func<T>.FirstNotNull()`, `Func<T1,T2>.FirstNotNull(arg)` | First non-null result among subscribers |
| `AddSafe` | `Action.AddSafe(value)` | Subscribe with duplicate protection |

### Usage

```csharp
// Safe invocation
onComplete.Fire();
onDataChanged.Fire(newData);

// AND aggregation (short-circuits)
bool canProceed = canActivate.FireAnd();

// OR aggregation (all subscribers execute)
bool anyMatch = hasMatch.FireOr(inputData);

// Collect data from subscribers
string[] allTags = getTags.Accumulate();

// First non-null response
var handler = findHandler.FirstNotNull(request);

// Subscribe without duplicates
onUpdate = onUpdate.AddSafe(MyHandler);
```

### Edge Cases

- `FireAnd` / `FireOr` with no subscribers return `returnOnZero` (default `true`)
- `FireAnd` short-circuits on first `false`; `FireOr` invokes all subscribers
- Subscriber exceptions are caught and logged via `Log.Print`
- `AddSafe` logs a warning on duplicate subscription attempts

---

## InitValve

Thread-safe initialization gate. Accumulates subscriptions until opened; after opening, new subscriptions are invoked immediately.

### Lifecycle

```
Create(out openValve)  →  valve += handler  →  openValve()  →  valve += lateHandler (immediate call)
                                                                  │
                                                             valve.Dispose()
```

### API

| Method | Description |
|--------|-------------|
| `InitValve.Create(out Action openValve)` | Factory. Returns instance and `Action` to open the valve |
| `operator +` | Subscribe. Before opening — accumulates; after — immediate invocation |
| `operator -` | Unsubscribe (only before opening) |
| `Dispose()` | Blocks further operations |

### Usage

```csharp
public class MySystem
{
    public InitValve OnReady { get; } = InitValve.Create(out var open);
    private Action _open = open;

    public void Initialize()
    {
        // ... initialization ...
        _open(); // all accumulated subscriptions fire
    }
}

// Subscriber:
mySystem.OnReady += () => Debug.Log("System ready");
```

### Edge Cases

- Repeated opening — logs a warning
- Operations after `Dispose()` — logs an error, operation ignored
- Thread safety via `lock`; subscriber invocation occurs outside the lock

---

## Crypto

Hashing, identifier generation, data packing (compression + encryption).

### API

| Method | Description |
|--------|-------------|
| `GetHashSha256(string text)` | SHA256 hash of a string (hex, lowercase) |
| `GetNewGuid()` | Deterministic GUID: `SHA256(timestamp + counter + random + random)` |
| `SetCryptoPack(string data, string pass)` | Compression (GZip) + encryption (AES-256-CBC). Returns Base64 string |
| `GetCryptoPack(string cryptoData, string pass)` | Decryption + decompression. Reverse of `SetCryptoPack` |

### SetCryptoPack / GetCryptoPack format

```
Base64( salt[16] | iv[16] | ciphertext )
```

- Key derived from `pass` via PBKDF2-SHA256 (100,000 iterations)
- Salt (16 bytes) randomly generated on each `SetCryptoPack` call
- IV (16 bytes) generated by AES
- Wrong `pass` on decryption → `CryptographicException`

### Usage

```csharp
// Pack
string packed = Crypto.SetCryptoPack(jsonData, "my_secret");

// Unpack
string original = Crypto.GetCryptoPack(packed, "my_secret");
```

### Edge Cases

- `GetNewGuid()` uses a counter for uniqueness within a single tick
- Not cryptographically secure — uses `Random` instead of `RandomNumberGenerator`
- `SHA256Managed` is deprecated; functional but not recommended for new projects
- `SetCryptoPack` / `GetCryptoPack`: `null` or empty `data` → returned as-is
- `SetCryptoPack` / `GetCryptoPack`: `null` or empty `pass` → `ArgumentNullException`
- `GetCryptoPack`: data shorter than 33 bytes (salt + iv + 1) → `ArgumentException`

---

## DateTimeExtConvert

`DateTime` ↔ Unix time conversion in milliseconds.

### API

| Method | Description |
|--------|-------------|
| `date.ToUnixTime()` | `DateTime` → `long` (ms since epoch) |
| `date.FromUnixTime(ms)` | `long` → `DateTime` (UTC) |

### Edge Cases

- `FromUnixTime` is an extension method, but the `date` value is ignored (a new epoch is created)
- `FromUnixTime` result is always `DateTimeKind.Utc`

---

## DictionaryExtAdding

Safe `IDictionary` operations with conflict logging.

### API

| Method | Description |
|--------|-------------|
| `dict.AddNew(key, value)` | Add with key existence check. `Error` on value conflict, `Warning` on duplicate |
| `dict.Get(key)` | Get with `Warning` on missing key. Returns `default` |

---

## IndexFabric

Factory for creating `Dictionary<string, T>` with `StringComparer.InvariantCultureIgnoreCase`.

| Method | Description |
|--------|-------------|
| `IndexFabric.Create<T>()` | Empty case-insensitive dictionary |
| `IndexFabric.Create<T>(source)` | Case-insensitive dictionary populated from `IDictionary<string, T>` |

---

## ListExt

| Method | Description |
|--------|-------------|
| `list.AddOnce(data)` | Add element if not already present (`Contains` check) |
| `list.IndexOfItem(value)` | Index of element in `IReadOnlyList<T>` (returns `-1` if not found) |

---

## StringExtCompress

String compression and decompression via `ZipArchive` + Base64.

### API

| Method | Description |
|--------|-------------|
| `str.Compress(key)` | String → ZipArchive → Base64. `key` — entry name in archive (default `"data"`) |
| `str.Decompress(key)` | Base64 → ZipArchive → string. Entry lookup by `key` suffix |

### Edge Cases

- `Decompress` returns `null` if entry not found or archive is empty
- `key` during decompression is matched via `EndsWith`, not exact match

---

## StringExtCommons

| Method | Description |
|--------|-------------|
| `str.IsNullOrWhitespace()` | `string.IsNullOrEmpty(str?.Trim())` |

---

## ObjectExtDeepClone

Deep object cloning via reflection with cycle detection.

### API

| Method | Description |
|--------|-------------|
| `source.DeepCopy<T>(returnOriginalOnError)` | Deep copy of an object. With `returnOriginalOnError = true` — returns original instead of `null` on failure |

### Type Processing Order

| Priority | Type | Behavior |
|----------|------|----------|
| 1 | `null` | `default(T)` |
| 2 | Primitives, `string`, `decimal`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, `Uri`, `Version`, `enum` | Returned as-is |
| 3 | Platform primitives (`SimpleTypeMarker`) | Returned by reference (not cloned) |
| 4 | Object already in `visited` | Returns previously created copy (cycle protection) |
| 5 | `Array` | Element-wise recursive copying |
| 6 | `IDictionary` | Recursive value copying; keys passed as-is |
| 7 | `IList` | Recursive element copying |
| 8 | `ICloneable` | Calls `Clone()`. **Contract: implementation must perform deep copy** |
| 9 | Other objects | `Activator.CreateInstance` (fallback: `FormatterServices.GetUninitializedObject`) + all fields copied (including private and inherited) |

### SimpleTypeMarker — Platform Primitives

`SimpleTypeMarker` is a partial class. Empty in Core. Unity partial adds types (`Sprite`, `GameObject`) that should not be cloned — passed by reference. Check uses `IsAssignableFrom` — subclasses of marker types are also treated as platform primitives. Types are cached on first call via reflection over static fields of the class.

### Edge Cases

- Cyclic references detected via `ReferenceEqualityComparer` — revisited objects return the already-created copy
- `ICloneable`: if `Clone()` performs shallow copy — nested references will be shared with the original
- `returnOriginalOnError = true` mixes the original into the copy graph — mutations to the original will be visible through the "copy"
- Instance creation: `Activator.CreateInstance`, on failure falls back to `FormatterServices.GetUninitializedObject` (no constructor call); if both fail — error or original returned
- Dictionary keys passed as-is (not cloned) — protects `GetHashCode`/`Equals` contract
- `readonly` fields are copied via `SetValue` (reflection bypasses readonly)
- `FieldInfo[]` and platform type caches are static, never cleared
- `null` input → `default(T)`

---

## ObjectExtCopy

Reflection-based public property copying between objects.

### API

| Method | Description |
|--------|-------------|
| `target.CopyFrom(source)` | Copies property values from `source` to `target` by name match. Only `public` readable properties of `source` → `CanWrite` properties of `target` |

### Edge Cases

- `source` and `target` types may differ — only properties with matching names are copied
- Properties without a setter in `target` are skipped
- Exceptions are caught; returns `false` on error

---

## SerializeController (Experimental)

Property-based JSON serializer. Processes public properties with a `public` getter and any setter.

### API

| Method | Description |
|--------|-------------|
| `model.SerializeProperties()` | Object → JSON string with type markers (`"__": "AssemblyQualifiedName"`) |
| `data.DeserializeProperties<T>()` | JSON string → object of type `T` |

### Supported Types

- Primitives (`int`, `long`, `float`, `double`, `decimal`, `bool`, `byte`, `short` and unsigned variants)
- `string`, `char`, `DateTime`, `Guid`, `enum`, `Nullable<T>`
- Collections: `IList`, arrays
- Dictionaries: `IDictionary` with simple keys (`string`, `int`, `Type`)
- Nested objects (recursive)

### Limitations

- Fields are ignored — properties only
- Cyclic references cause an error (protected via `HashSet<object>`)
- Dictionary keys — simple types or `Type` only
- `VisitedObjects` is static, not thread-safe
- Status: experimental

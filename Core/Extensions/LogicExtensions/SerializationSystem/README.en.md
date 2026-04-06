# SerializeController

**Namespace:** `Vortex.Core.Extensions.LogicExtensions.SerializationSystem`
**Assembly:** `ru.vortex.extensions`
**Platform:** .NET Standard 2.1+

---

## Purpose

Object serialization and deserialization to JSON strings without external libraries.

Capabilities:

- Convert objects to formatted JSON strings and back
- Property-based (public getter + any setter)
- Serialization control via `[POCO]` attribute on types
- Exclusion of individual properties via `[NotPOCO]`
- Nested objects, collections, dictionaries
- Type-safe deserialization with `IsAssignableFrom` validation
- Cyclic reference protection
- `Nullable<T>`, `enum`, `DateTime`, `Guid` support

Out of scope:

- Field serialization (properties only)
- Dictionaries with complex keys (simple types and `Type` only)
- Thread safety
- Object graphs with multiple references to the same instance

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.LoggerSystem` | Error logging via `Log.Print()` |
| `System.Reflection` | Property and attribute discovery |
| `System.Collections` | `IDictionary`, `IList` |
| `System.Globalization` | `CultureInfo.InvariantCulture` for numbers and `DateTime` |

---

## Architecture

Partial class `SerializeController` split into 4 files:

| File | Purpose |
|------|---------|
| `SerializeController.cs` | Common infrastructure: caches, POCO validation, JSON parsing, simple types |
| `SerializeControllerExtSerialization.cs` | Object serialization to JSON string |
| `SerializeControllerExtDeserialization.cs` | JSON string deserialization into new objects |
| `SerializeControllerExtUploading.cs` | Loading data from JSON into existing objects |

Additionally — two attributes in `POCOAttribute.cs`.

### Attributes

| Attribute | Purpose | Target |
|-----------|---------|--------|
| `[POCO]` | Marks a type as serializable | Class, struct, interface |
| `[NotPOCO]` | Excludes a property from serialization | Property |

### Type filtering rules

| Category | Rule |
|----------|------|
| Simple types (primitives, string, enum, DateTime, Guid) | Always serialized |
| Collections (`IList`, arrays) | Serialized if element type is serializable |
| Dictionaries (`IDictionary`) | Key — simple type or `Type`; value must be serializable |
| Complex types | Only if the type is marked `[POCO]` |
| Properties with `[NotPOCO]` | Always skipped |

`[POCO]` on an interface applies to all its implementations.

### Serialization flow

```
SerializeProperties(object)
  SerializeClass(object, depth)
    IsSimpleType?   -> GetSimple()
    Cyclic?         -> Error, ""
    IDictionary?    -> SerializeDictionary()
    IList/Array?    -> SerializeArray()
    IsPOCO(type)?   -> GetReadablePropertiesList() -> recurse over properties
    !IsPOCO(type)?  -> Warning, "null"
```

### Deserialization flow

```
DeserializeProperties<T>(string)
  DeserializeClass(Type, string)
    IsSimpleType?  -> SetSimple()
    "null"?        -> null
    IDictionary?   -> DeserializeDictionary()
    Array?         -> DeserializeArray()
    IList?         -> DeserializeCollection()
    Object         -> SeparateText() -> read "__" marker
                      Type.GetType() -> IsPOCO? -> Activator.CreateInstance()
                      fallback -> FormatterServices.GetUninitializedObject()
                      -> recurse over properties (skip [NotPOCO])
```

### Upload flow

Unlike deserialization, does not create new instances — updates existing ones.
Dictionary keys not present in the data are preserved (merge).

```
UploadProperties<T>(string, T)
  IDictionary?    -> UploadDictionary()  (merge: existing updated, new added)
  Array?          -> UploadArray()       (recreates array)
  IList?          -> UploadCollection()  (clear + fill)
  Object          -> UploadClass()       (updates target properties)
    empty data    -> ClearClass()        (reset properties to default/null)
    property null -> fallback to DeserializeClass() (create new)
    property set  -> recursive Upload into existing object
```

### JSON format

Each complex object contains a type marker `"__"` with `AssemblyQualifiedName`:

```json
{
  "__" : "MyApp.Player, MyAssembly, Version=1.0.0.0, ...",
  "Name" : "Player1",
  "Level" : 5,
  "Items" : [
    {
      "__" : "MyApp.Item, MyAssembly, ...",
      "Id" : "sword_01",
      "Count" : 1
    }
  ]
}
```

| Type | JSON format |
|------|------------|
| `string` | `"text"` (with `\"`, `\\` escaping) |
| `int`, `long` and other integers | `42` |
| `float`, `double`, `decimal` | `3.14` (InvariantCulture) |
| `bool` | `true` / `false` |
| `enum` | `"Active"` (string name) |
| `DateTime` | `"yyyy-MM-dd HH:mm:ss"` (InvariantCulture, no timezone) |
| `Guid` | `"00000000-0000-0000-0000-000000000000"` |
| `null` | `null` |
| `Type` (dictionary key) | `"AssemblyQualifiedName"` |

### Caching

| Cache | Content |
|-------|---------|
| `CacheFields` | `Dictionary<Type, PropertyInfo[]>` — type properties (filtered by `[NotPOCO]` and `IsSerializableType`) |
| `CachePOCO` | `Dictionary<Type, bool>` — `[POCO]` check result for type and its interfaces |

Both caches are populated on first access and never cleared.

### Framework integration

**ComplexModel<T>** — uses `SerializeProperties()` / `DeserializeProperties<T>()` to persist composite models (`Dictionary<Type, T>`). The interface `T` can be marked `[POCO]`, making all its implementations automatically serializable.

**RecordScriptGenerator** — generates save code for Record subclasses:

```csharp
public override string GetDataForSave() => this.SerializeProperties();
public override void LoadFromSaveData(string data)
{
    var temp = data.DeserializeProperties<MyRecord>();
    this.CopyFrom(temp);
}
```

---

## Contract

### Input

An object whose type is marked `[POCO]` (or implements an interface with `[POCO]`), with public properties (getter + any setter).

### Output

- Serialization: formatted JSON string with type markers
- Deserialization: typed object instance or `default(T)` on error

### Supported property types

| Category | Types |
|----------|-------|
| Numbers | `int`, `long`, `short`, `byte`, `sbyte`, `uint`, `ulong`, `ushort`, `float`, `double`, `decimal` |
| Text | `string`, `char` |
| Boolean | `bool` |
| Date/Identifiers | `DateTime`, `Guid` |
| Enumerations | Any `enum` |
| Nullable | `Nullable<T>` for all simple types |
| Collections | `T[]`, `List<T>`, any `IList` (if element is serializable) |
| Dictionaries | `Dictionary<K,V>` — key: simple type or `Type`; value: serializable type |
| Nested objects | Recursively — only types with `[POCO]` |

### Guarantees

- Types without `[POCO]` are not serialized (warning logged, requires DebugMode enabled)
- Properties with `[NotPOCO]` are ignored in both directions
- Non-serializable property types are filtered out at `PropertyInfo[]` collection stage
- Deserialization validates type compatibility via `IsAssignableFrom`
- Cyclic references detected via `HashSet<object>`
- Floating-point numbers via `InvariantCulture`
- Unknown properties in JSON during deserialization are skipped with warning (don't break loading)
- Warning messages are only output when DebugMode is enabled (`SettingsModelExtDebug`)

### Limitations

| Limitation | Reason |
|------------|--------|
| Properties only, no fields | Controls the serializable surface, filters system fields |
| Complex types require `[POCO]` | Protects against pulling in UnityEngine.Object and other unintended types |
| No cyclic references | Detected via `HashSet`; cycle causes error |
| Dictionary keys — simple types or `Type` | Complex keys not supported |
| `Type.GetType()` on deserialization | Type must be available in current AppDomain |
| `DateTime` without timezone | Fixed format `yyyy-MM-dd HH:mm:ss` |
| `VisitedObjects` is static | Not thread-safe |

---

## Usage

### Type marking

```csharp
[POCO]
public class PlayerData
{
    public string Name { get; set; }
    public int Level { get; set; }
    public float Score { get; set; }
}
```

### Excluding properties

```csharp
[POCO]
public class QuestModel : Record
{
    public QuestState State { get; internal set; }
    public byte Step { get; internal set; }

    // Not serialized — immutable data from preset
    [NotPOCO] public bool Autorun { get; private set; }
    [NotPOCO] public bool UnFailable { get; internal set; }

    // Not serialized — QuestLogic is not marked [POCO]
    public QuestLogic[] Logics { get; private set; }
}
```

### Marking via interface

```csharp
[POCO]
public interface IGameData { }

// Automatically serializable — implements a [POCO] interface
public class InventoryData : IGameData
{
    public int Gold { get; set; }
    public int Gems { get; set; }
}
```

### Serialization

```csharp
var player = new PlayerData { Name = "Test", Level = 42, Score = 3.14f };
string json = player.SerializeProperties();
```

### Deserialization

```csharp
PlayerData restored = json.DeserializeProperties<PlayerData>();
```

### Polymorphism

Deserialization restores the concrete type from the `"__"` marker. Target type `T` can be a base class or interface:

```csharp
IGameData data = json.DeserializeProperties<IGameData>();
// data will be the concrete type stored in "__"
```

### SaveSystem integration via Record

```csharp
[POCO]
public class PlayerRecord : Record
{
    public string Name { get; set; }
    public int Level { get; private set; }

    // Reference to ScriptableObject — won't be pulled in (type lacks [POCO])
    public WeaponConfig Weapon { get; set; }

    public override string GetDataForSave() => this.SerializeProperties();
    public override void LoadFromSaveData(string data)
        => this.CopyFrom(data.DeserializeProperties<PlayerRecord>());
}
```

### ComplexModel integration

```csharp
[POCO]
public interface IGameData { }

public class GameModel : ComplexModel<IGameData>
{
    // All IGameData implementations are automatically serializable
}
```

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| `null` input to `SerializeProperties` | `string.Empty` |
| Type without `[POCO]` | `LogLevel.Warning`, `"null"` (serialization) / `null` (deserialization) |
| Property with `[NotPOCO]` | Skipped in both directions |
| Property with non-serializable type | Filtered from `PropertyInfo[]`, not included in JSON |
| Cyclic reference (A.B = obj, A.C = obj) | `LogLevel.Error`, `""` |
| Incompatible type on deserialization | `LogLevel.Error`, `default(T)` |
| Missing `"__"` marker in JSON | `LogLevel.Error`, `null` |
| Property without setter | Filtered during property collection |
| Property in JSON but removed from type | `LogLevel.Warning`, skip (doesn't break loading) |
| Property in JSON but without setter | `LogLevel.Warning`, skip |
| Empty string on deserialization | `null` |
| `null` element in collection | Skipped on deserialization |
| `[POCO]` on interface | All implementations are considered serializable |
| Type without default constructor | Fallback to `FormatterServices.GetUninitializedObject()` — fields are not initialized by constructor |

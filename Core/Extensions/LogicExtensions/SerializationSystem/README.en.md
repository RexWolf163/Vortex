# SerializeController

**Namespace:** `Vortex.Core.Extensions.LogicExtensions.SerializationSystem`
**Assembly:** `ru.vortex.extensions`
**Platform:** .NET Standard 2.1+
**Status:** experimental

---

## Purpose

Object serialization and deserialization to JSON strings without external libraries.

Capabilities:

- Convert objects to formatted JSON strings and back
- Property-based (public getter + any setter)
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
| `System.Reflection` | Property discovery |
| `System.Collections` | `IDictionary`, `IList` |
| `System.Globalization` | `CultureInfo.InvariantCulture` for numbers and `DateTime` |

---

## Architecture

Static class with two extension methods. Single file, no interfaces or inheritance.

### Serialization flow

```
SerializeProperties(object)
  SerializeClass(object, depth)
    IsSimpleType? -> GetSimple()
    Cyclic?       -> Error, ""
    IDictionary?  -> SerializeDictionary()
    IList/Array?  -> SerializeArray()
    Object        -> GetReadablePropertiesList() -> recurse over properties
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
                      Type.GetType() -> Activator.CreateInstance()
                      -> recurse over properties
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

`Dictionary<Type, PropertyInfo[]>` — property cache per type. Populated on first access, never cleared.

### Framework integration

**ComplexModel<T>** — uses `SerializeProperties()` / `DeserializeProperties<T>()` to persist composite models (`Dictionary<Type, T>`).

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

Any object with public properties that have a getter and any setter (`public`, `protected`, `private`).

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
| Collections | `T[]`, `List<T>`, any `IList` |
| Dictionaries | `Dictionary<K,V>` — key: simple type or `Type` |
| Nested objects | Recursively via properties |

### Guarantees

- Deserialization validates type compatibility via `IsAssignableFrom`
- Cyclic references detected via `HashSet<object>`, serialization aborted
- Floating-point numbers serialized/deserialized via `InvariantCulture`

### Limitations

| Limitation | Reason |
|------------|--------|
| Properties only, no fields | Developer controls the serializable surface |
| No cyclic references | Detected via `HashSet`; cycle causes error |
| Dictionary keys — simple types or `Type` | Complex keys not supported |
| `Type.GetType()` on deserialization | Type must be available in current AppDomain |
| `DateTime` without timezone | Fixed format `yyyy-MM-dd HH:mm:ss` |
| `VisitedObjects` is static | Not thread-safe |
| `PropertyInfo[]` cache never cleared | Potential leak with many types |

---

## Usage

### Serialization

```csharp
var player = new Player { Name = "Test", Level = 42, Score = 3.14f };
string json = player.SerializeProperties();
```

### Deserialization

```csharp
Player restored = json.DeserializeProperties<Player>();
```

### Polymorphism

Deserialization restores the concrete type from the `"__"` marker. Target type `T` can be a base class or interface:

```csharp
IUnit unit = json.DeserializeProperties<IUnit>();
// unit will be the concrete type stored in "__"
```

### SaveSystem integration via Record

Record properties to be saved are declared as public properties with a setter:

```csharp
public class PlayerRecord : Record
{
    public string Name { get; set; }
    public int Level { get; private set; }  // private set — serialized
    public float Score { get; set; }
}
```

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| `null` input to `SerializeProperties` | `string.Empty` |
| Cyclic reference (A.B = obj, A.C = obj) | `LogLevel.Error`, `""` |
| Incompatible type on deserialization | `LogLevel.Error`, `default(T)` |
| Missing `"__"` marker in JSON | `LogLevel.Error`, `null` |
| Property without setter | Skipped |
| Property in JSON but removed from type | `LogLevel.Error`, `null` |
| Empty string on deserialization | `null` |
| `null` element in collection | Skipped on deserialization (`continue`) |

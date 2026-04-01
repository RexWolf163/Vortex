# Vortex Framework

A modular framework for Unity application development with clear layer separation and a data bus pattern.

## Philosophy

A minimal, protected, predictable architecture where every line of code is justified by practical benefit.

Programming comes down to three tasks:
1. **Data retrieval** — data is accessible from any point through static buses
2. **Data processing** — continuous processing without external corrections of intermediate results
3. **Data display** — display components work with the model through the bus, not directly

Any technology should simplify or improve working with code — otherwise it is redundant. The main threat is architectural degradation from uncontrolled changes, layer mixing, and lack of protection for key components.

## Layer Architecture

Upper layers may reference lower ones. The reverse is forbidden. Layers characterize the degree of code universality and neutrality, not its execution conditions. The layer concept applies only to scripts and does not extend to assets.

```
┌─────────────────────────────────────────────────────────────────┐
│  Layer 4: AppLocale                                             │
│  Project-specific scripts                                       │
├─────────────────────────────────────────────────────────────────┤
│  Layer 3: AppSDK                                                │
│  Universal mechanics for an application family                  │
├─────────────────────────────────────────────────────────────────┤
│  Layer 2: Framework Adaptation (Unity)                          │
│  Drivers, presets, platform-dependent implementations           │
├─────────────────────────────────────────────────────────────────┤
│  Layer 1: Framework Core                                        │
│  Neutral models, buses, abstractions (no Unity API)             │
└─────────────────────────────────────────────────────────────────┘
```

### Layer 1: Framework Core

Platform-independent logic: pure C#, .NET Standard, no Unity. Abstract data models, static access buses, driver interfaces, loading and saving systems.

Namespace: `Vortex.Core`

### Layer 2: Framework Adaptation

Platform-dependent but domain-neutral logic. Implements universal patterns requiring Unity (`ScriptableObject`, `Resources`, Addressables, Odin). Can be used in any Unity project.

Namespace: `Vortex.Unity`

### Layer 3: AppSDK

Domain-specific but reusable within a product family. Not tied to a single project.

### Layer 4: AppLocale

Unique project-specific logic. Even if it uses the data bus — it remains local.

### Classification Criteria

- **Level 1 ↔ 2**: if code depends on Unity — it cannot be level 1. If code does not depend on the subject domain — it can be level 2, even if it implements complex logic.
- **Level 2 ↔ 3**: level 2 is a pattern without a domain. Level 3 is a domain-specific pattern, but not tied to a single project.
- **Level 3 ↔ 4**: level 3 can be copied to another project of the same family without changes. Level 4 requires modification even for a similar project.

The ideal: everything gradually migrates upward toward level 1, provided it demonstrates sufficient neutrality and universality.

---

## Data Bus

The central architectural pattern of the framework. A static singleton with `Dictionary<GUID, Record>` for O(1) access to shared data from any point in the project.

```csharp
// Singleton — one instance for the entire lifecycle (saved)
var profile = Database.GetRecord<UserProfile>("user-profile-guid");

// MultiInstance — a fresh copy on each request (not saved)
var template = Database.GetNewRecord<DocumentTemplate>("template-guid");
```

**Bus criteria:**
1. Data is readable from any point in the project
2. The requesting component knows exactly what it is looking for (by GUID)
3. Selection by an unambiguous identifier
4. Maximum performance (Dictionary O(1))

**Data types:**
- **Shared** → through the bus (`GetRecord<T>(id)`, `GetNewRecord<T>(id)`)
- **Private** → only within the component

DI containers (Zenject, VContainer) are not used — they are redundant when data is shared and accessible through the bus.

---

## Working with Data

### Preset → Model

The database is a set of immutable presets (`ScriptableObject`). Models are mutable instances created from presets. GUID is mandatory for all typed units.

### Processing Continuity

External correction of intermediate results must not be allowed:

```csharp
// ❌ Bad: event fires on every change
model.HP = 50;  // → OnChange → external correction
model.MP = 30;  // → OnChange → recursion

// ✓ Good: manual call after all changes
model.HP = 50;
model.MP = 30;
model.NotifyChanged();
```

### Call Accumulation

When multiple changes occur in a single frame — process once at the end:

```csharp
// Multiple changes in a frame
soldier.SetTarget(enemy);
soldier.SetPosture(Crouching);
soldier.TakeDamage(10);

// Visual update — once at the end of the frame
```

### Data Modification Logic — Only in the Controller

No recursive triggers without explicit control. UI does not make decisions — it only reports user actions.

---

## Display

View ≠ model. The view may have its own additional parameters that are not transferable to the model.

UI components:
- Subscribe to bus or UI node events
- Independently retrieve data by identifiers
- Work with maximally atomic and neutral data

```csharp
// ❌ Bad: direct data passing
interface.SetData(model);

// ✓ Good: component retrieves data from the bus itself
public class HeroPanel : MonoBehaviour
{
    void OnEnable()
    {
        var hero = HeroBus.GetCurrent();
        UpdateView(hero);
        HeroBus.OnChanged += UpdateView;
    }
}
```

---

## Package Structure

### Single Mono-Model

| Component | Layer | Description |
|-----------|-------|-------------|
| `{System}` | Core | Bus: cache, change/output logic, events |
| `{System}Model` | Core | Model: public properties (`get; private set;`) |
| `{System}Preset` | Adaptation | Preset: public properties (`get;`), ScriptableObject |

### Multiple Models (Database)

| Component | Layer | Description |
|-----------|-------|-------------|
| `{System}` | Core | Bus: index-registry, retrieval methods |
| `Record` | Core | Model: properties, events, `CopyFrom()` |
| `RecordPreset<T>` | Adaptation | Preset: Record creation method from data |

### Key Patterns

- **Singleton + SystemController**: `SystemController<T, TD>` inherits from `Singleton<T>`, drivers validated via `DriversGenericList.WhiteList`
- **ReactiveValue\<T\>**: `IntData`, `BoolData`, `FloatData` with implicit operators and `OnUpdate` event
- **IProcess + WaitingFor()**: async loading with topological dependency sorting
- **Partial classes**: large systems split by topic (`App`, `Database`, `UIProvider`, `SettingsModel`)
- **ActionExt**: safe delegate invocation — `Fire()`, `FireAnd/Or()`, `Accumulate()`, `FirstNotNull()`
- **MonoBehaviour singletons** — only when `Update`/`Coroutine` is needed; otherwise — pure C#

---

## Dependencies

- Unity 2021.3+
- UniTask
- Odin Inspector (optional, for editor tools)
- Addressables (optional, for `AddressablesDriver`)
- protobuf-net (for `ComplexModel` serialization)

---

## License

Proprietary. All rights reserved.

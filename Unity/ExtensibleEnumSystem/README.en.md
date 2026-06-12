# ExtensibleEnumSystem (Unity)

**Namespace:** `Vortex.Unity.ExtensibleEnumSystem.*`
**Assembly:** `ru.vortex.unity.extenums` (Editor-only logic guarded by `#if UNITY_EDITOR` inside the same asmdef)

---

## Purpose

Thin Unity-side wrapper around the `ExtensibleEnum` abstraction (Layer 1):

- Inspector attribute `[ExtEnumKey(typeof(MoveState))]` with a dropdown of valid keys.
- `ExtensibleEnumValueSwitcherHandler` — reflection-based bridge from `ExtEnumData<T>` to `UIStateSwitcher`.

The eager registry initialization (`[RuntimeInitializeOnLoadMethod]` + `[InitializeOnLoadMethod]`) is performed in L1 — no separate Unity initializer is needed.

Out of scope:

- Codegen, paired SO assets, validators — **intentionally not provided**. Concrete `ExtensibleEnum` subclasses are written by hand (see Layer 1 README).
- UI integration (overload `UIStateSwitcher.Set(ExtensibleEnum)` and `StateSwitcherAttribute` extension) lives in the `Vortex.Unity.UI.StateSwitcher` package.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.ExtensibleEnumSystem` | `ExtensibleEnum`, `ExtEnumData<T>` |
| `Vortex.Core.Extensions.ReactiveValues` | `IReactiveData` for subscription in `ExtensibleEnumValueSwitcherHandler` |
| `Vortex.Unity.UI.StateSwitcher` | `UIStateSwitcher` for the bridge |

---

## Architecture

```
Attributes/
  ExtEnumKeyAttribute               — [ExtEnumKey(typeof(TEnum))] for string fields in the Inspector;
                                      stores ExtEnumType, validates inheritance from ExtensibleEnum

Handlers/
  ExtensibleEnumValueSwitcherHandler — MonoBehaviour: source + property name + UIStateSwitcher;
                                       subscribes to IReactiveData.OnUpdateData,
                                       calls switcher.Set(extEnumData.Index) on change

Editor/                              (under #if UNITY_EDITOR, in the same runtime asmdef)
  ExtensibleEnumKeyAttributeDrawer  — popup with axis keys for [ExtEnumKey].
                                      The ExtensibleEnum registry is already populated by the
                                      L1 eager initializer — the drawer just reads GetAll(Type).
```

---

## `[ExtEnumKey]` Inspector attribute

```csharp
public class CharacterPreset : ScriptableObject
{
    [ExtEnumKey(typeof(MoveState))]
    [SerializeField] private string defaultMoveState;

    public MoveState GetDefault() => ExtensibleEnum.GetByKey<MoveState>(defaultMoveState);
}
```

The drawer shows a popup of keys read from `ExtensibleEnum.GetAll(typeof(MoveState))`. The registry is already populated by the L1 initializer at domain-load / scene-start — no additional triggers on the drawer side.

The attribute constructor verifies that the supplied `Type` inherits from `ExtensibleEnum`; otherwise it throws `ArgumentException`.

---

## `ExtensibleEnumValueSwitcherHandler` — bridge

Late-binding bridge `ExtEnumData<T>` → `UIStateSwitcher`. Configured in the inspector:

- **source** — MonoBehaviour exposing a property of type `ExtEnumData<TEnum>`.
- **property** — name of that property.
- **switcher** — target `UIStateSwitcher`.

Lifecycle:
- `Awake` — resolves the `PropertyInfo` for `property`, reads the value into `_extEnumData`, caches the `Index` property reference via reflection, subscribes to `IReactiveData.OnUpdateData`. On any failure — `Debug.LogError` + `enabled = false`.
- `Start` — first `OnDataChanged()` call (after every Awake in the scene; consumers have subscribed by then).
- `OnDataChanged` — reads `Index` via reflection; if `index >= 0`, calls `switcher.Set(index)`.
- `OnDestroy` — unsubscribe + cache nullification.

The link between key and switcher slot is the value's `Index`: the handler reads `ExtEnumData.Index` and, when `index >= 0`, calls `switcher.Set(index)`. The switcher's slots must be in the same order as the values in the type.

The editor-helper `GetExtEnumDataProperties()` (under `#if UNITY_EDITOR`) collects the list of `source`'s properties whose type derives from `ExtEnumData<>`. Ready to be wired into a future drawer with a dropdown (currently the `property` field is a plain string input).

---

## Edge cases

| Scenario | Behavior |
|----------|----------|
| `[ExtEnumKey]` for a non-`ExtensibleEnum` type | `ArgumentException` in the attribute ctor |
| `[ExtEnumKey]` on a non-string field | Drawer prints `"[ExtEnumKey] only on string fields"` |
| `[ExtEnumKey]` for a type with no values | Popup empty, warning HelpBox is drawn |
| `ExtensibleEnumValueSwitcherHandler` with source / property unset | Awake logs error, `enabled = false` |
| `property` not found / returns null / not `ExtEnumData<>` | Awake logs error, `enabled = false` |
| `ExtEnumData.Index == -1` (value not registered) | Switcher does not switch, no-op |

---

## Public API

```csharp
namespace Vortex.Unity.ExtensibleEnumSystem.Attributes
{
    public class ExtEnumKeyAttribute : PropertyAttribute
    {
        public Type ExtEnumType { get; }
        public ExtEnumKeyAttribute(Type extEnumType);
    }
}

namespace Vortex.Unity.ExtensibleEnumSystem.Handlers
{
    public class ExtensibleEnumValueSwitcherHandler : MonoBehaviour { }
}
```

Editor classes (`ExtensibleEnumKeyAttributeDrawer`) are internal, guarded by `#if UNITY_EDITOR` in the same asmdef.

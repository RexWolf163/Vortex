# ItemsSystem

## Purpose

An item model whose set of properties is defined at authoring time rather than fixed in code.

- An item is a multi-instance database record with a polymorphic set of properties.
- Property classes are declared outside the package; the package knows nothing about concrete properties.
- Property access goes through a purpose interface in O(1).
- Authoring data and gameplay state are separated, so balance patches reach items the player already owns.
- A single `OnChanged` event gives consumers reactivity without polling.
- The set of properties can change at runtime and survives saving.

Out of scope: inventory, showcase, stacks, mass, volume, quantity. The package describes an item and its properties — not their placement and not their aggregation.

## Dependencies

The package compiles when the `itemsSdk` toggle is enabled in `SdkSettings` (define symbol `USING_VORTEX_ITEMS`).

Assembly: `ru.vortex.sdk.itemssystem`. Odin Inspector required.

## Architecture

| Unit | Role |
|------|------|
| `ItemPreset` | Authoring asset: property set, category key. The single authoring surface |
| `ItemModel` | Item instance. Two property containers, `OnChanged` event, resolved category |
| `ItemProperty` | Property base. The storage type of the set |
| `IItemProperty` | Root marker for purpose interfaces. The query type is anything derived from it |
| `ItemCategory` | Extensible enum of categories. Domain values are declared outside the package; only `Unknown` ships with it |
| `ItemsBus` | Static controller: item construction, property-set changes |

### Two property containers

Item properties live in two non-public dictionaries.

| Container | Key | Saved | Purpose |
|-----------|-----|-------|---------|
| Set | concrete property class | yes | merge on load, each property stored exactly once |
| Index | purpose interface | no | O(1) lookup, rebuilt on construction and on set change |

This storage makes the central invariant structural: **at most one property per purpose interface**. Two properties on the same interface physically do not fit into the index — the conflict surfaces on insertion.

The cost is memory. At 10,000 items the subsystem takes roughly 16–20 MB. The trade is deliberate: against a build where textures alone are measured in gigabytes, this is a fraction of a percent of the memory budget.

### Reactivity

An item has one event — `OnChanged` — raised when a property value or the property set changes. Whoever changes a value (a controller of count, durability, decaying weight) calls, through the property, the protected `ItemProperty.NotifyChanged(owner)`, which raises `ItemModel.NotifyChanged()` (internal). So a change reaches the consumer without polling and without a global counter: an inventory subscribes to its items' `OnChanged` and re-emits it as its own event.

Derived values are recomputed on demand — a consumer reading total mass always sees the current value, including decay. A cache is added only where a profile shows a problem.

## Contract

**Input.** A preset identifier, optionally saved state.

**Guarantees.**

| # | Invariant |
|---|-----------|
| Inv-1 | At most one property per purpose interface within an item |
| Inv-2 | The set changes only through the bus |
| Inv-3 | Authoring data is not saved and is taken anew on every construction |

**Limitations.**

- Raising `OnChanged` on a property value change is the responsibility of the owning controller (it calls `NotifyChanged(owner)` after the mutation). The package neither guarantees nor can verify it.
- `OnItemCreated` fires on every construction. Loading a large collection means thousands of consecutive invocations — handlers must be cheap.
- An exception in a handler is not isolated and aborts loading. That is preferable to a silently half-built item.
- Property class identity in a save is the assembly-qualified type name. Renaming or moving a property class is a breaking change for saves.

## Separating authoring data from saved data

The serialization default is "saved". A missing exclusion mark on an authoring field means the value goes into the save, overrides the authoring value on load, and the balance patch **silently** stops reaching existing items. The failure is invisible and surfaces late.

Property fields fall into two categories:

| Category | Example | Mark |
|----------|---------|------|
| Authoring | base mass, maximum durability | `[NotPOCO]` |
| Gameplay state | current durability | none |

A private setter does **not** exclude a field from the save: selection looks at getter visibility and at the presence of a setter of any access level. These are two independent mechanisms.

A property meant to be added at runtime must have no authoring fields: it has no source preset, and on recreation from a save such fields arrive as zeros.

## Usage

### 1. Declare a purpose interface

```csharp
public interface IMassProperty : IItemProperty
{
    float Mass { get; }
}
```

Deriving from `IItemProperty` is mandatory: the index is built from it, and through it the interface carries the serializability mark — property classes need no annotation of their own.

### 2. Declare a property

```csharp
[Serializable]
public class FixedMass : ItemProperty, IMassProperty
{
    [SerializeField] private float baseMass;
    [SerializeField] private float bonus;

    [NotPOCO] public float BaseMass { get => baseMass; private set => baseMass = value; }

    public float Bonus { get => bonus; set => bonus = value; }

    public float Mass => BaseMass + Bonus;
}
```

`[Serializable]` is required by Unity for `[SerializeReference]`.

The inspector works with **fields**, saving works with **properties**. Hence the pair "serialized field + property over it": the field supplies the authoring value in the preset, the property decides the value's fate in the save. `BaseMass` comes from authoring and is excluded; `Bonus` is gameplay state and is saved. `Mass` has no setter and never reaches the save.

`[NotPOCO]` is allowed on properties only — it will not compile on a field.

### 3. Declare categories

```csharp
public partial class ItemCategory
{
    public static readonly ItemCategory Weapon     = new(nameof(Weapon), 100);
    public static readonly ItemCategory Consumable = new(nameof(Consumable), 110);
}
```

### 4. Author a preset

`Create → Database → Item`. The inspector exposes the property set and the category. The record type is pinned to multi-instance automatically.

### 5. Build an item

```csharp
var item = ItemsBus.Create(presetGuid);                 // new
var restored = ItemsBus.Create(presetGuid, saveData);   // from a save
```

The order is mandatory and encapsulated in the bus: shape from preset → state overlay → index → event. The owner takes an instance by preset identifier and only then passes saved state.

### 6. Read a property

```csharp
var mass = item.GetProperty<IMassProperty>();
if (mass != null)
    total += mass.Mass;
```

### 7. React to an item change

```csharp
item.OnChanged += it => Refresh(it);   // a property value or the set changed
```

Compute a derived value on demand — it is always current:

```csharp
public float Total(IReadOnlyList<ItemModel> items)
{
    var total = 0f;
    foreach (var item in items)
        if (item.GetProperty<IMassProperty>() is { } mass)
            total += mass.Mass;
    return total;
}
```

A controller that changes a property value raises the signal through the protected `ItemProperty.NotifyChanged(owner)`, so the change reaches `OnChanged` subscribers without polling.

### 8. Change the set

```csharp
ItemsBus.AddProperty(item, new Enchantment());
ItemsBus.RemoveProperty<IEnchantment>(item);
```

### 9. Post-construction assembly

```csharp
ItemsBus.OnItemCreated += item =>
{
    if (item.Category == ItemCategory.Weapon && !item.HasProperty<IDurability>())
        ItemsBus.AddProperty(item, new Durability());
};
```

The event fires both on creation from a preset and on restoration from a save, so domain assembly is written once.

## API

### `ItemsBus`

| Member | Description |
|--------|-------------|
| `Create(presetGuid, saveData = null)` | Build an item (with the event) |
| `BuildProbe(presetGuid, saveData = null)` | Silent build for inspection: no event |
| `AddProperty(item, property)` | Add a property. `false` on conflict |
| `RemoveProperty<T>(item)` | Remove a property by purpose or by class |
| `RemoveProperty(item, property)` | Remove a specific property |
| `OnItemCreated` | The item is built and consistent |

### `ItemModel`

| Member | Description |
|--------|-------------|
| `GetProperty<T>()` | Property by purpose interface or by class. `null` if absent |
| `HasProperty<T>()` | Whether the property is present |
| `AllProperties` | All properties for enumeration |
| `Category` | Resolved category. Never `null`: an empty or unresolvable key yields `ItemCategory.Unknown` |
| `OnChanged` | A property value or the set changed |
| `GetDataForSave()` / `LoadFromSaveData()` | Saving and state overlay |

### `ItemProperty`

| Member | Description |
|--------|-------------|
| `NotifyChanged(owner)` | `protected` — a subclass calls it after changing its value; raises `owner.OnChanged` |

## Editor tools

Set validation in the preset inspector — an error message above the property list. It catches:

- an empty slot in the list;
- a repeated property class;
- an occupied purpose, naming both claimants;
- a property with no purpose interface at all — it cannot be found and is inert.

The result is cached for one second so validation does not degrade inspector rendering.

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| Unresolvable preset identifier, no save | An empty stub: identifier kept, set empty, no name or icon, category `Unknown`. Any property query returns `null`. The collection holder decides the item's fate |
| Unresolvable preset identifier, with a save | The set is restored from the save in full, down to the concrete property classes: player data is not lost because a preset went missing. There is no authoring data — no name, no icon, category `Unknown` — and authoring fields of properties arrive as zeros |
| Query for a missing property | `null`, no exception |
| Purpose conflict during construction | The offending property is rejected entirely — from both the index and the set. Error logged |
| Conflict on runtime addition | The operation is rejected before any mutation, so the item is never left half-changed. Error logged, `false` returned |
| Property in preset, absent from save | Preset instance in its initial state — this is how patch-added properties reach existing items |
| Property in save, absent from preset | Recreated from the save. Covers both runtime additions and patch removals: the two are indistinguishable |
| Property removed by a patch | Stays on old items as a dead passenger. Cleanup is the job of the `OnItemCreated` subscriber that owns the property |
| Category not set | `Category` returns `ItemCategory.Unknown` silently |
| Unresolvable category key | `Category` returns `ItemCategory.Unknown` and logs an error once |
| Property with no purpose interfaces | Not indexed, reachable only by a concrete-class query. Highlighted in the editor |
| Property class renamed or moved | "Type not found" error on load. A breaking change for saves; no migration tables exist |

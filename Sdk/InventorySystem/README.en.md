# InventorySystem

## Purpose

An embeddable inventory constrained by a set of verifying rules, plus capacity properties (mass, volume, stack, container) declared for the items of the `ItemsSystem` package.

- The inventory is not a database record but a field in the host's authoring asset (chest, character, merchant, corpse). It lives inside the host model and shares its lifecycle.
- Constraints are a set of verifiers, not fixed fields. A rule absent from the set constrains nothing.
- Capacity is measured by item properties. Mass and volume are properties; the inventory sums them.
- Homogeneous items merge into stacks by matching type and the presence of a stack property.
- A container is also an item: a bag carries an inventory as a property, and its mass and volume are counted together with the contents.
- The save holds not the item but a "preset id + state" pair; restoration goes through the items bus.

Out of scope: slots and positioning, drag-and-drop UI, trade, crafting, equipment.

## Dependencies

Compiles when the `itemsSdk` toggle is enabled in `SdkSettings` — under the same define key `USING_VORTEX_ITEMS` as the items package; there is no separate toggle. Assembly: `ru.vortex.sdk.inventorysystem`, references `ru.vortex.sdk.itemssystem`. There is no reverse dependency — the items package knows nothing of inventories. Odin Inspector required.

## Architecture

| Unit | Role |
|------|------|
| `Inventory` | Embeddable model: verifier set, stack policy, startup fill, composition, packed save |
| `InventoryVerifier` | The "whether to place" rule. Polymorphic, `[SerializeReference]` |
| `InventoryController` | Composition operations: add, take, split, destroy, move |
| `StackController` | The only mutator of stack count; holds the owner lock key |
| `InventoryBus` | Inventory-destroyed event |
| `InventoryRegistry` | Debug scan for duplicate item instances across inventories |
| Properties | `Stack`, `Mass`, `Volume`, `Container`, `ContainerMass`, `Rigid/SoftVolume` |

### Verifiers

Constraints are a set of rules in the inventory's authoring. The base rule declares three methods, all taking the inventory as a parameter (they hold no reference to it):

| Method | Returns |
|--------|---------|
| `CanPlace` | whether to place the item |
| `GetMax` | limit of the measured quantity (filters return 0) |
| `GetCurrent` | current value over the composition (filters return 0) |

The generic `QuantityVerifier` implements "sum of a measured quantity against a limit" with a cache keyed on the version axis; the mass and volume rules add only how to measure a single item. Filters (`CategoryFilter`, `Require/ForbidProperty<T>`) derive from the base directly and return zero on both read methods — they measure nothing.

Limit and sum are a wide integer (`long`): ten thousand items with large unit values exceed 32 bits, and overflow would mean negative occupancy with unlimited capacity.

Shipped rules:

| Rule | Authoring |
|------|-----------|
| `MassVerifier` | total mass limit |
| `VolumeVerifier` | total volume limit |
| `CategoryFilterVerifier` | a set of categories and a whitelist/blacklist flag |
| `RequirePropertyVerifier<T>` | generic: property purpose `T` required |
| `ForbidPropertyVerifier<T>` | generic: property purpose `T` forbidden |

The last two have no authoring: a purpose interface is a type and cannot be picked in the inspector, whereas polymorphic selection works by class. A project declares a subclass in one line:

```csharp
[Serializable] public class RequireQuestTag : RequirePropertyVerifier<IQuestTag> { }
```

### Capacity properties

They live in this package; for `ItemsSystem` these are "properties from outside." Access goes by purpose interface, and within one item a purpose is held by exactly one property.

| Property | Purpose | Value |
|----------|---------|-------|
| `StackProperty` | `IStackProperty` | stack max (authoring), current count (reactive, saved) |
| `MassProperty` | `IMassProperty` | unit mass × stack count |
| `VolumeProperty` | `IVolumeProperty` | unit volume × stack count |
| `ContainerProperty` | `IContainerProperty` | nested inventory |
| `ContainerMassProperty` | `IMassProperty` | own weight + contents mass |
| `RigidVolumeProperty` | `IVolumeProperty` | own volume, independent of contents |
| `SoftVolumeProperty` | `IVolumeProperty` | own volume + contents volume |

Container mass is a separate class on the same `IMassProperty` purpose as ordinary mass: two properties on one purpose within an item are impossible, so a bag carries either plain mass or container mass. Rigid vs. soft volume is a designer's choice of class.

### Version axis and cache

The inventory and verifiers share the version axis from the items package. The occupancy cache in `QuantityVerifier` is keyed on it. The axis is advanced by: a change to the inventory's composition (`Inventory.Version`), a change to a stack count (`StackController` → `ItemProperty.Touch`), a change to any item's composition (`ItemsBus`), and recursively by changes inside nested containers. Keying on the global axis is necessary: the inventory's own mark does not change when a nested container's contents change, yet the total mass does.

## Contract

**Input.** The inventory is authored in the host's preset. The composition changes only through `InventoryController`.

**Guarantees.**

| # | Invariant |
|---|-----------|
| Inv-1 | Items in an inventory are built only by the items bus |
| Inv-2 | Occupancy is consistent with composition — on-demand recompute cached by the axis |
| Inv-3 | Stack count is changed only by `StackController`, which also advances the mark |
| Inv-4 | Composition changes only through the controller |
| Inv-5 | A move loses no item — "check the receiver, then take from the source" |
| Inv-6 | A container cannot end up inside itself or its descendant |
| Inv-7 | Adding cannot cause an overflow; an existing overflow is legal and not resolved by the package |
| Inv-8 | Stack count never exceeds `Max`: startup is clamped, `MergeIn` and split respect the limit |

**Ownership transfer.** `Add` and `Move` take ownership of the item. On a full merge into existing stacks the incoming instance is absorbed and zeroed — its reference must not be reused.

**Limitations.**

- Item homogeneity on merge is decided by matching type and the presence of a stack property; the composition and state of other properties are not compared — the correct set of stackable types is the designer's responsibility.
- Verifiers gate placement only. There are no take-out restrictions in this version.
- Property class identity in a save is the assembly-qualified name; renaming the assembly breaks item saves.

## Usage

### 1. Declare the inventory in the host preset

```csharp
public class ChestPreset : RecordPreset<ChestModel>
{
    [SerializeField] private Inventory inventory;
    public Inventory Inventory => inventory;
}

public class ChestModel : Record
{
    public Inventory Inventory { get; private set; }
    // ...
}
```

The inventory moves into the model via `CopyFrom` on record construction — like an ordinary property. Verifiers, policy, and startup fill arrive from authoring.

### 2. Author in the inspector

The verifier set (polymorphic class selection), stack policy, startup fill (pairs of "item preset + count").

### 3. Operations

```csharp
if (chest.Inventory.Add(item)) { /* placed */ }

var taken = chest.Inventory.Take(item);          // take the whole item
var part  = chest.Inventory.TakeAmount(item, 5); // split 5 off a stack
chest.Inventory.Drop(item);                      // take out and destroy

item.Move(from: chest.Inventory, to: bag.Inventory);
```

### 4. Read composition and occupancy

```csharp
foreach (var it in chest.Inventory.Items) { /* ... */ }

foreach (var v in chest.Inventory.Verifiers)
    ShowBar(v.GetCurrent(chest.Inventory), v.GetMax(chest.Inventory)); // filters return 0/0
```

### 5. Destruction

```csharp
chest.Inventory.Dispose(); // contents removed recursively, then the OnInventoryDestroyed event
```

The event fires after the contents are removed — it is for cleanup, not for salvage. Whoever needs the contents (to spill them on the ground) takes them before `Dispose`.

## API

### `InventoryController` (extension methods)

| Member | Description |
|--------|-------------|
| `CanPlace(item)` | Structural cycle check + verifier set poll |
| `Add(item)` | Add honouring the stack policy |
| `Take(item)` | Take the whole item, return it |
| `TakeAmount(item, n)` | Split `n` off a stack into a new instance |
| `Drop(item)` | Take out and destroy |
| `Move(from, to)` | Move between inventories |
| `CountOf(presetGuid)` | Total units of a given item type, across stacks |
| `CanAdd(presetGuid, n)` | Whether `n` units fit — via a silent probe (no item creation, event or axis shift) against the verifiers |
| `RemoveCount(presetGuid, n)` | Remove `n` units across stacks; all-or-nothing |
| `AddCount(presetGuid, n)` | Add `n` units, chunked into stacks no larger than `Max`; all-or-nothing (rollback on shortfall) |

### `Inventory`

| Member | Description |
|--------|-------------|
| `Items` | Composition for reading; the first access materializes the inventory |
| `Verifiers` | The rule set |
| `Policy` | The stack policy |
| `Version` | Mark of the last composition change |
| `Dispose()` | Destroy with contents |

### `InventoryBus`

| Member | Description |
|--------|-------------|
| `OnInventoryDestroyed` | The inventory was destroyed together with its contents |

## Debugging

The `inventoryDebugMode` flag in `DebugSettings`, subordinate to the global debug mode. Both checks are noticeably expensive:

- logging a merge of items with divergent composition or state (compared via serialization to string);
- a duplicate scan after a new game and after a load — all live inventories yield their contents into a common set, and each repeated instance is logged;
- a destruction protocol — how many of which items were removed with the inventory.

The duplicate scan sees only materialized inventories, but only they hold live item references, so a by-reference duplicate is possible only among them.

A guard against an item ending up in two inventories cannot be placed in the item — it would reverse the assembly dependency. Hence the check is diagnostic, in the debug circuit.

## Accepted discipline

- **Homogeneity on merge is not checked.** A full-durability sword and a broken one merge into a stack, and the latter's state is lost. A deliberate trade: comparing on every add is costly, and the correct set of stackable types is an authoring matter. The debug flag helps find such cases.
- **Authoring data is not saved.** The serialization default is "saved." A missing exclusion mark on an authoring field sends the value into the save, overrides the authoring on load, and a balance patch silently stops reaching existing inventories. The inventory's authoring fields (verifiers, policy, fill) are stored as fields, not properties, so the serializer does not see them; new item properties split their fields into authoring and gameplay per the `ItemsSystem` rules.

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| Empty verifier set | Everything fits |
| Adding to an overflowed inventory | Rejected: any addition fails the check. Unloading brings it back to normal |
| Loading beyond current limits | Passes in full, the inventory stays overflowed; the capacity check is not applied on the restore path |
| Startup fill beyond `Max` | Count is clamped to `Max` with an error log; a multi-stack set is several entries |
| Startup count on a non-stackable item | One instance, error log |
| Full merge of the incoming | The incoming is absorbed and zeroed; ownership taken |
| Splitting a stack | A new instance from the preset; state beyond count is not restored |
| Container into itself or a descendant | Rejected with an error log (Inv-6) |
| Corrupted packed save | The inventory is empty, error logged; the original string is not overwritten — a chance to recover remains |
| Unresolvable item preset in a save | A stub per the `ItemsSystem` rules; game logic decides its fate |
| Reading container mass | Materializes the nested inventory as a side effect: `OnItemCreated` for the contents may fire earlier, including during a rejected `CanPlace`. The sums are still correct — a consequence of the lazy model |
| Destroying an inventory | Contents removed recursively, then the event; a second `Dispose` is a no-op |

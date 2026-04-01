# PoolSystem

**Namespace:** `Vortex.Unity.UI.PoolSystem`
**Assembly:** `ru.vortex.unity.ui.misc`

## Purpose

Object pool with data keys and reuse. `Pool` manages `PoolItem` instances, providing creation, reuse, and deactivation of elements.

Capabilities:
- Element creation/reuse by data key
- Deactivation with return to free queue
- Typed data access via `IDataStorage`
- `OnUpdateLink` event on data change

Out of scope:
- Element display logic (implemented in `PoolItem` subclasses)
- Pool preloading

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `IDataStorage` — data storage interface |
| Odin Inspector | `[ShowInInspector]` — data display in Editor |

---

## Architecture

```
PoolSystem/
├── Pool.cs         # Pool manager (MonoBehaviour)
└── PoolItem.cs     # Element container (MonoBehaviour, IDataStorage)
```

### Pool

MonoBehaviour manager. Stores:
- `PoolItem preset` — template for instantiation
- `Dictionary<object, PoolItem> _index` — active elements by data key
- `Queue<PoolItem> _freeItems` — free element queue

On `Awake`, collects existing child `PoolItem` instances into the free queue.

### PoolItem

MonoBehaviour container linking data to visuals. Implements `IDataStorage`. Stores `List<object> _data` with FIFO type-based search.

---

## API

### Pool

```csharp
pool.AddItem(model, intData);               // params object[] — multiple data objects
pool.AddItem(data);                          // create/reuse
pool.AddItemBefore(data);                    // insert at beginning (SetAsFirstSibling)
var item = pool.AddItem<MyView>(data);       // with MonoBehaviour cast

var existing = pool.GetItem<MyView>(data);   // get by key (or null)

pool.RemoveItem(data);                       // deactivate, return to queue
pool.RemoveByCallback(key => key is MyType); // remove all elements matching condition
pool.Clear();                                // deactivate all, return to queue
```

When passing `params object[]`, the array is used as the key in `_index`, while `PoolItem.MakeLink` unpacks it: each array element is added to `_data` separately, enabling `GetData<T>()` lookup by any of the provided types.

### PoolItem

```csharp
var model = poolItem.GetData<MyModel>();     // FIFO search by type
poolItem.OnUpdateLink += OnDataChanged;      // data update event
```

---

## Contract

### Input
- `PoolItem preset` — template in Inspector
- `object data` — element key and data

### Output
- Active `PoolItem` with bound data
- `OnUpdateLink` — event on data change

### Guarantees
- `AddItem` with existing key returns existing element
- `RemoveItem` returns element to queue, does not destroy
- `RemoveByCallback` iterates over a key snapshot — safe during `_index` modification
- `Clear` deactivates all elements and returns them to the free queue

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `AddItem()` with existing key | Returns existing element |
| `RemoveItem()` with non-existent key | Silent skip |
| `GetItem()` with non-existent key | Returns `null` |
| Free queue empty | New instance created from `preset` |
| `Clear()` | All elements deactivated and returned to free queue, index cleared |
| `RemoveByCallback()` — callback matched no keys | Nothing removed |
| `PoolItem.GetData<T>()` — type not found | Returns `null` |

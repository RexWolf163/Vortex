# AssetCacheSystem

**Namespace:** `Vortex.Unity.AssetCacheSystem.*`
**Assembly:** `ru.vortex.unity.assetcachesystem` (defineConstraints: `ENABLE_ADDRESSABLES`)

---

## Purpose

Managed cache of Addressables assets with five core mechanics:

- **Owner-tracking** — which consumer holds which `AssetReference`, and with how many holds (one per `Load`).
- **Inflight deduplication** — parallel requests for the same ref resolve via a single `Addressables.LoadAssetAsync` call.
- **LRU survivors** — released but not yet unloaded assets sit in a buffer; a repeated request becomes an instant HIT.
- **Sweep of destroyed Unity owners** — every `Release` call scans for `UnityEngine.Object` owners (`MonoBehaviour` etc.) destroyed without an explicit Release.
- **Groups** — isolated registries with their own survivor capacity: a stream of assets in one group (e.g. voice lines) does not evict another group's survivors.

Consumer API — `Load` and `Release`, plus the synchronous `LoadSync` (the group is optional; without it, calls work with the default group):
```csharp
UniTask<T> AssetCache.Load<T>(object owner, AssetReference reference, CancellationToken ct = default,
                              string group = null);
T          AssetCache.LoadSync<T>(object owner, AssetReference reference, string group = null);
void       AssetCache.Release(object owner, string group = null);
```

On top of the cache the package ships two ready consumers: `AddressableAssetHandle<T>` (lazy implementation of `AssetHandle<T>`) and `DataStorageTransport` (prefab loaded on enable).

Out of scope:
- Implicit conversion of `AssetReference` to a concrete T (`Load<T>` expects the caller to know the actual asset type).
- Sweep of POCO owners (Unity objects only; POCOs require explicit Release discipline).
- Streaming, Addressables labels, scene loading — that's the Addressables API layer.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `Unity.Addressables` + `Unity.ResourceManager` | Actual asset load/unload |
| `UniTask` + `UniTask.Addressables` | Async API |
| `Vortex.Core.System` | `Singleton<T>` |
| `Vortex.Core.AppSystem` | `App.OnExit` for Dispose |
| `Vortex.Core.SettingsSystem` + `Vortex.Unity.SettingsSystem` | Reading `SurvivorCapacity`, groups, debug flag; `SettingsPreset` |
| `Vortex.Core.Extensions` | `InitValve.Create`, `IsNullOrWhitespace` |
| `Vortex.Core.AssetHandleSystem` | `AssetHandle<T>` — base of `AddressableAssetHandle<T>` |
| `Vortex.Core.LoggerSystem` | `Log.Print` — repeated-load warning in `AddressableAssetHandle<T>` |
| `Vortex.Unity.UI.Misc` | `DataStorage` — base of `DataStorageTransport` |
| `Vortex.Unity.Extensions` | `AssetDatabaseExt.GetSingletonAsset` (Editor menu, group drawer) |
| `Vortex.Unity.EditorTools` | `DrawingUtility.DrawSelector` — the `[AssetCacheGroup]` dropdown |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` SO marker |
| Odin Inspector (`Sirenix`) | Inspector attributes of the settings and `DataStorageTransport`, `[AssetCacheGroup]` drawer |

`defineConstraints: ["ENABLE_ADDRESSABLES"]` in asmdef — the package compiles only in projects with Addressables enabled.

---

## Architecture

```
Bus/
  AssetCache (static)               — public API: Load / LoadSync / Release / OnReady / OnRelease
                                      bootstrap via [RuntimeInitializeOnLoadMethod]
                                      subscribes to Settings.OnInit → CreateController → Controller.Init

Controllers/
  AssetCacheController : Singleton  — IAssetCacheController implementation, partial split by topic
    ├── .cs            — Init / Cleanup / IsInitialized / Model / OnInitialized / OnReleased / ResolveGroup
    ├── .Loading.cs    — Load<T> / LoadSync<T> / StartLoad / RegisterOwner / IsSameRecord /
    │                    UnregisterOwner / ReviveFromSurvivor
    └── .Releasing.cs  — Release / SweepDeadOwners / ReleaseOwner / IsHeldByAnyOwner / PushSurvivor /
                         EvictIfOverflow

Abstractions/
  IAssetCacheController             — consumer API + lifecycle contract

Models/
  AssetCacheModel                   — runtime state: set of groups (Default + named), debug aggregates
  AssetCacheGroupModel              — group registry + capacity:
                                      Locks     — owner → (ref → number of holds)
                                      Handles   — ref → Addressables handle
                                      Inflight  — ref → in-flight load
                                      Survivors — LRU queue (LinkedList) of assets without holds
  InflightLoad                      — DTO for in-flight load: Handle + UniTaskCompletionSource

AddressableAssetHandle<T>           — AssetHandle<T> on top of AssetCache (owner = the handle itself)

Handler/
  DataStorageTransport : DataStorage — loads a prefab through the cache on OnEnable, instantiates it and
                                       hands over its Data; releases on OnDisable

Attributes/
  AssetCacheGroupAttribute          — string field = group key, dropdown in the inspector

Config/
  AssetCacheSettings : SettingsPreset, ICoreAsset
                                    — SO in Resources/Settings/: default group SurvivorCapacity
                                      (default 32) + group list (key, SurvivorCapacity)
  SettingsExt/                      — SettingsModel partial extension via .asmref
    AssetCacheConfig                — immutable runtime config (+ AssetCacheGroupConfig)
    SettingsModelExtAssetCache      — AssetCache + AssetCacheDebugLogs properties
  DebugExt/                         — DebugSettings partial extension via .asmref
    DebugSettingsExtAssetsCache     — AssetCacheDebugLogs toggle (respects DebugMode)

Editor/
  MenuController                    — package menu items (AssetsCache/Runtime Index, AssetsCache/Config,
                                      Configs/AssetCache Settings)
  AssetCacheIndexWindow             — EditorWindow: runtime index inspector (read-only)
  AssetCacheGroupAttributeDrawer    — Odin drawer for the group dropdown
```

### Bootstrap lifecycle

```
[RuntimeInitializeOnLoadMethod]
    ↓
AssetCache.Bootstrap()
    ├── Settings.OnInit += CreateController  (if Settings is already initialized — invoked immediately)
    └── App.OnExit += Dispose
    ↓
Settings.OnInit (through InitValve)
    ↓
CreateController()
    ├── Controller already created → exit
    ├── Settings.Data() == null → exit
    ├── Settings.Data().AssetCache → Config; null → Debug.LogError, exit (Controller stays null)
    ├── Controller = AssetCacheController.Instance
    ├── Controller.OnInitialized += NotifyReady     ← opens InitValve OnReady
    ├── Controller.OnReleased    += NotifyReleased
    └── Controller.Init()
    ↓
Controller.Init()
    ├── reads SurvivorCapacity, groups + debugLogging from Settings.Data()
    ├── creates AssetCacheModel: default group + named groups
    │   (empty key / duplicate key → Debug.LogError, group skipped)
    ├── IsInitialized = true
    └── OnInitialized?.Invoke() → AssetCache.OnReady opens
    ↓
Consumer:
    AssetCache.OnReady.Subscribe(handler) — runs immediately if already Ready

App.OnExit → Dispose()
    ├── Settings.OnInit -= CreateController; App.OnExit -= Dispose
    └── Controller.Cleanup() → OnReleased → NotifyReleased: unsubscribes from the controller, AssetCache.OnRelease
        (OnReady stays open, IsReady == false; the bus doesn't initialize again on its own)
```

### `Load<T>(owner, reference, ct, group)` flow

All structures below belong to the registry of the group that `group` resolves to.

```
1. ArgNullCheck: owner / reference
2. ResolveGroup(group)                  ← empty → Default; unknown key → LogError + Default
3. RegisterOwner(owner, reference)      ← Locks[owner][reference] += 1 (one hold per Load);
                                           remembers the owner record (generation)
4. Lookup in Handles
   ├── HIT  → ReviveFromSurvivor, result = (T)handle.Result   (instant)
   └── miss → continue
5. Lookup in Inflight
   ├── JOIN → result = await slot.Completion (with ct support)
   └── miss → continue
6. StartLoad<T>:
   ├── handle = Addressables.LoadAssetAsync<T>(reference)
   ├── Inflight[reference] = { Handle, Completion }
   ├── await handle.ToUniTask()             ← waiter's ct does NOT abort the load
   ├── Cleanup happened (model replaced)? → OCE
   ├── Handles[reference] = handle
   ├── slot.Completion.TrySetResult(loaded) ← broadcast to all JOIN-waiters
   ├── if (!IsHeldByAnyOwner(ref))          ← all waiters cancelled before completion
   │     PushSurvivor(ref)
   ├── ct.ThrowIfCancellationRequested()    ← waiter's OCE after successful load
   ├── load failure → Completion.TrySetException (to all waiters), Addressables.Release(handle),
   │                  Handles.Remove(ref); rethrow
   └── finally: Inflight.Remove(ref)
7. Before returning: is the owner record still the same?
   └── no (Release(owner) during the load) → OperationCanceledException: an asset without a hold is not returned
8. catch (any exception, incl. waiter's OCE and a T cast mismatch) → UnregisterOwner(owner, ref, record)
   ├── the record was replaced (Release, then new Loads of the owner) → skip: its holds belong to other Loads
   ├── minus one hold; the ref is dropped from the record at zero, the owner from Locks with an empty record
   └── no holds of the ref left and the asset is loaded → PushSurvivor(ref); rethrow
```

### `LoadSync<T>(owner, reference, group)` flow

The synchronous variant for spots where `await` isn't possible. Blocks the main thread until the load completes.

```
1. ArgNullCheck: owner / reference
2. ResolveGroup(group); RegisterOwner(owner, reference) ← one hold, remembers the record
3. HIT      → ReviveFromSurvivor, return (T)handle.Result
4. Inflight → slot.Handle.WaitForCompletion()
   ├── failure → UnregisterOwner(owner, ref, record), return null
   └── success → return (T)result
5. New load → Addressables.LoadAssetAsync<T>(reference).WaitForCompletion()   ← without an Inflight slot
   ├── failure → Addressables.Release(handle), UnregisterOwner(owner, ref, record), return null
   └── success → Handles[reference] = handle, return result
```

Differences from `Load`: failure is returned as `null`, not an exception; there's no generation check on the success path (the call is synchronous); a T cast mismatch on HIT/Inflight throws `InvalidCastException` without rollback — the hold stays until `Release(owner)`.

### `Release(owner, group)` flow

```
1. ArgNullCheck: owner
2. Model == null (before Init / after Cleanup) → exit
3. Sweep destroyed Unity owners — across ALL groups:
   ├── Scan the group's Locks for `o is Object uo && uo == null`
   └── For each — ReleaseOwner(group, dead, isSweep: true)
4. ResolveGroup(group) → ReleaseOwner(owner, isSweep: false) in that group:
   ├── owner not found → warning in debug mode, exit
   ├── Locks.Remove(owner)          ← the record with all holds of the owner at once
   └── For each ref:
       ├── IsHeldByAnyOwner(ref)? → skip (still held)
       └── Handles.Contains(ref)? → PushSurvivor(ref)

PushSurvivor (into the group's survivors):
   ├── Survivors.Remove(ref)        ← refresh position
   ├── Survivors.AddLast(ref)
   └── EvictIfOverflow:
       while (Survivors.Count > group's SurvivorCapacity):
         head = Survivors.First
         Survivors.RemoveFirst
         if (IsHeldByAnyOwner(head)) continue  ← someone revived, handle stays
         Addressables.Release(handle)
         Handles.Remove(head)
```

`AssetCache.Release` before bootstrap (`Controller == null`) is a no-op.

---

## Contract

### Guarantees

- **Inflight deduplication**: N parallel `Load(refX)` calls from different owners → **one** `Addressables.LoadAssetAsync` call, all wait on the same `UniTaskCompletionSource`.
- **Instant HIT**: a repeated `Load` for an active or survivor asset returns synchronously via `await`, with no network/disk IO.
- **Survivor revive**: an asset that landed in survivors is returned instantly on a repeat request — no `Addressables.Release` is called.
- **Cancellation semantics**: waiter's ct cancellation **does not abort** the actual load — it's needed for other waiters. A cancelled waiter gets `OperationCanceledException`, the load continues.
- **Hold counting**: every `Load` of a ref adds one hold to its owner's record; a failed/cancelled `Load` removes only its own hold, `Release(owner)` removes the record with all holds at once. The rollback touches only the record the `Load` registered in: if `Release(owner)` happened in between and the owner loaded again, the new holds stay. So a cancelled load still in flight does not drop the hold of a live `Load` of the same ref by the same owner — neither on a repeated `Load` without `Release`, nor on `Release` + a repeated `Load` (disable/enable during loading). The owner stays one key in `Locks` regardless of the number of holds.
- **Release during loading**: if `Release(owner)` happened while that owner's `Load` was in flight, the `Load` ends with `OperationCanceledException` instead of returning an asset without a hold; the asset goes to survivors unless someone else holds it.
- **Cleanup is idempotent**: a repeated call is a no-op.
- **One handle per ref within a group**: there is always at most one entry per `AssetReference` in a group's `Handles`.
- **Groups do not break each other**: one ref loaded in two groups holds one handle in each. The shared reference count is kept by Addressables itself (a repeated `LoadAssetAsync` of the same key calls `IncrementReferenceCount` on the cached operation), so eviction/Release in one group only decrements the count — the asset is physically unloaded once every group has released it.

### Groups

- **Default group** — the group without an identifier. Calls without `group` (or with an empty string) work with it; its capacity is `AssetCacheSettings.SurvivorCapacity`.
- **Named groups** — the `groups` list in `AssetCacheSettings`: key + its own `SurvivorCapacity`. Empty keys and duplicates are dropped at `Init` with `Debug.LogError`.
- **Isolation**: a group has its own `Locks`/`Handles`/`Inflight`/`Survivors`. HIT, JOIN, and eviction happen only within the group.
- **The loader releases**: `Release(owner, group)` drops ownership only in the given group. An owner that loaded in several groups releases each one separately.
- **Unknown key** (typo, group removed from settings) — `Debug.LogError` and handled as the default group. The call isn't lost; the misconfiguration shows up in the log.
- **Sweep** of destroyed Unity owners runs across all groups on every `Release`.

### Discipline

- A single `AssetCacheSettings` instance in `Resources/Settings/` is required.
- **Owner must call `Release`** for every Load-ownership, otherwise the asset is not unloaded (see Sweep below).
- **`Release` with the same group as `Load`.** A Release in another group won't find the owner (warning in debug mode); the lock stays until the sweep (Unity owner) or until `Cleanup` (POCO).
- Sweep only handles `UnityEngine.Object` owners destroyed without Release. **POCO owners** (plain C# classes) require manual Release discipline; otherwise their Locks entry and held handles live until `Cleanup`.
- An `AssetReference` must be requested **with the same T** across different `Load<T>` calls. The cast to T happens on the waiter side; using one ref with different T values causes `InvalidCastException` on the second request.
- **`Load`/`LoadSync` only after `OnReady`.** Before bootstrap `Controller == null` and the call fails with NRE. `[RuntimeInitializeOnLoadMethod]` runs after the first scene loads, so `OnEnable` of objects in that scene can come earlier.

### Limitations

- `IsHeldByAnyOwner` lookup is O(L) by number of owners. Fine for typical projects with tens to hundreds of owners. For thousands you'd want a reverse index (not implemented in v1 — memory priority).
- `ReviveFromSurvivor` is O(n) via `LinkedList.Remove(value)`. For capacities of 32-128 it's negligible.
- Not thread-safe — all operations are Unity main-thread.

---

## Usage

### Register settings

The `AssetCacheSettings` asset in `Resources/Settings/` is created by the settings system in the editor when missing (`Assets/Resources/Settings/AssetCacheSettings.asset`); `Tools/Vortex/AssetsCache/Config` pings it in the Project window.
- Fill in the default group's `SurvivorCapacity` (default 32).
- If needed, add named groups to `groups`: key + the group's `SurvivorCapacity`.
- `SettingsDriver` picks it up automatically.

### Basic pattern in a MonoBehaviour

```csharp
public class WeaponView : MonoBehaviour
{
    [SerializeField] private AssetReference muzzleFlashRef;
    private GameObject _muzzleFlashPrefab;

    private async void OnEnable()
    {
        // owner = this (Unity object — sweep will pick it up on destroy)
        _muzzleFlashPrefab = await AssetCache.Load<GameObject>(this, muzzleFlashRef, destroyCancellationToken);
    }

    private void OnDestroy()
    {
        // Explicit Release is not strictly required (sweep handles destruction),
        // but it's useful for immediate release / moving the asset into survivors.
        AssetCache.Release(this);
    }
}
```

### Loading into a group

```csharp
public class VoiceLine : MonoBehaviour
{
    [SerializeField] private AssetReference clipRef;

    // Dropdown with the groups from AssetCacheSettings. Empty — default group.
    [SerializeField, AssetCacheGroup] private string cacheGroup;

    private async UniTask PlayAsync(CancellationToken ct)
    {
        // Release(this) during the load ends it with OperationCanceledException.
        var clip = await AssetCache.Load<AudioClip>(this, clipRef, ct, cacheGroup);
        // ...
    }

    private void OnDisable()
    {
        // The loader releases — in the same group it loaded into.
        AssetCache.Release(this, cacheGroup);
    }
}
```

`DataStorageTransport` (`cacheGroup`) and `AddressableAssetHandle<T>` (`group`) already have a group field.

### POCO owner

```csharp
public class AudioCue
{
    private readonly object _ownerKey = new();   // owner = indestructible key
    private AudioClip _clip;

    public async UniTask PreloadAsync(AssetReference clipRef, CancellationToken ct)
    {
        _clip = await AssetCache.Load<AudioClip>(_ownerKey, clipRef, ct);
    }

    public void Dispose()
    {
        // Required — sweep doesn't kick in for POCO owners.
        AssetCache.Release(_ownerKey);
    }
}
```

### `AddressableAssetHandle<T>`

The `AssetHandle<T>` implementation on top of the cache: fields `reference` (`AssetReference`) and `group` (`[AssetCacheGroup]`). The owner in the cache is the handle itself (`owner = this`), so a handle always has exactly one key in `Locks`; overlapping loads of one handle are counted as holds of that key. `LoadAsync` → `AssetCache.Load<T>(this, reference, ct, group)`; a repeated `LoadAsync` without `Release` returns the same instance with a warning; `Release` → `AssetCache.Release(this, group)` (no-op while nothing is loaded). The full contract is in the `AssetHandleSystem` README.

### `DataStorageTransport`

A `DataStorage` descendant for swapping a heavy prefab in and out with the GameObject:
- `OnEnable` → `AssetCache.Load<GameObject>(this, prefabReference, token, cacheGroup)` → `Instantiate` under `targetForInstance` (empty — its own `Transform`) → if `Data` isn't empty, `SetData` into the instance's `DataStorage` (if it has one).
- `OnDisable` → cancel the token, `Destroy` the instance, `AssetCache.Release(this, cacheGroup)`. `OperationCanceledException` of an interrupted load is swallowed.
- In the editor: `Connect` / `Disconnect` buttons — instantiate the prefab via `editorAsset` / remove the target's children for layout work. An error InfoBox is shown while the target has children.

### Subscribing to bus readiness

```csharp
AssetCache.OnReady.Subscribe(() =>
{
    // Runs immediately if bootstrap is already complete.
    Debug.Log($"AssetCache ready. Survivor capacity = {AssetCache.Config.SurvivorCapacity}");
});
```

### Parallel requests for one ref

```csharp
// All 100 simultaneous requests resolve through ONE Addressables call.
var tasks = Enumerable.Range(0, 100).Select(i =>
    AssetCache.Load<Sprite>(owners[i], iconRef, ct));

var sprites = await UniTask.WhenAll(tasks);
```

### Cancelling a waiter

```csharp
var cts = new CancellationTokenSource();

var task = AssetCache.Load<GameObject>(owner, heavyRef, cts.Token);
cts.CancelAfter(100);   // give 100ms

try { var go = await task; }
catch (OperationCanceledException) { /* waiter cancelled */ }

// The actual load CONTINUES — once it completes, the asset goes into survivors
// (if no other active owners) or becomes an instant HIT for another requester.
```

---

## Edge Cases

| Scenario | Behavior |
|---|---|
| `Load(null, ref)` or `Load(owner, null)` | `ArgumentNullException` |
| `Release(null)` | `ArgumentNullException` |
| `Load`/`LoadSync` before Settings bootstrap | NRE on `Controller.Load` (Controller == null). Subscribe to `OnReady` first |
| `Release` before Settings bootstrap | No-op (`Controller?.Release`) |
| Parallel `Load(refX)` from 100 owners | One `Addressables.LoadAssetAsync`, all wait on the same `Completion`. HIT for the next ones |
| `Load<T1>(...)` then `Load<T2>(...)` for the same ref with T1 ≠ T2 | First cast works, second one fails with `InvalidCastException`; the second `Load`'s hold is rolled back |
| `ct` cancelled before load completes | OCE for that waiter; the actual load continues; result goes to other waiters or to survivors |
| `ct` cancelled for the only waiter | Load completes, asset goes to survivors. `Load` removes the waiter's hold itself (`UnregisterOwner`), no explicit `Release(owner)` needed |
| One owner: `Load(ref)` cancelled while still in flight, then a repeated `Load(ref)` | Two holds of one owner; the cancelled one removes only its own, the asset stays held by the live `Load` |
| One owner: `Load(ref)` in flight, `Release(owner)`, then a new `Load(ref)` (disable/enable during loading) | The first `Load`'s rollback doesn't touch the new record; the asset stays held by the second `Load` |
| `Release(owner)` during that owner's `Load` with the token not cancelled | `Load` ends with `OperationCanceledException`; the asset goes to survivors unless someone else holds it |
| One owner, several successful `Load(ref)` | Several holds; one `Release(owner)` removes all |
| Addressables load failure | All waiters of the ref get the exception; the handle is released, every waiter's hold is rolled back |
| `LoadSync` failure | `null`, the hold is rolled back |
| `LoadSync` with a T mismatch on HIT/Inflight | `InvalidCastException`; the hold stays until `Release(owner)` |
| `Load(this, ref)` on an MB; MB destroyed without `Release` | Sweep on next `Release` of any owner detects `o is Object && o == null` and releases |
| POCO owner without `Release` | Leak: Locks/Handles live until `Cleanup` |
| Survivor capacity = 0 | An asset left without holds (`Release` or rollback of the last hold) is unloaded via `Addressables.Release` right away |
| `Release(owner)` for an owner with no active Locks | Warning in debug mode, no-op in release |
| `Settings.Data().AssetCache == null` (no SO) | `Debug.LogError` in `CreateController`, controller is not created: `Load` fails with NRE, `Dispose` on `App.OnExit` too |
| `App.OnExit` | `Dispose` → `Controller.Cleanup` → all Handles + Inflight released, `OnRelease`; `OnReady` stays open with `IsReady == false` |
| Repeated `Init` after `Cleanup` | At the controller level: a new `Model`, registries from scratch. The bus doesn't call it: after `Dispose` it's unsubscribed from `Settings.OnInit` |
| `Cleanup` (+ repeated `Init`) during a load | The load ends with OCE; the handle was released in `Cleanup` and doesn't reach the new model |
| `Load`/`Release` with an unknown group | `Debug.LogError`, the call works with the default group |
| Empty or duplicate key in `groups` | `Debug.LogError` at `Init`, group skipped (calls with that key behave as unknown) |
| One ref in two groups | Two handles; HIT/eviction are independent; physical unload happens after both release (Addressables refcount) |
| `Release(owner, B)` for an owner that loaded in group A | Owner not found in B (warning in debug mode); the lock in A stays until its own `Release`/sweep |
| Owner loaded in A and B | `Release` is needed for each group; the sweep of a destroyed Unity owner clears both |

---

## Editor

`Tools/Vortex/AssetsCache/Config` and `Tools/Vortex/Configs/AssetCache Settings` — ping the settings asset in the Project window. Two entry points into one command: the first sits next to the package window, the second in the project-wide config list. Uses `AssetDatabaseExt.GetSingletonAsset<AssetCacheSettings>()` — expects a single instance in the project; logs an error on multiplicity.

### Runtime Index

`Tools/Vortex/AssetsCache/Runtime Index` — a window showing the current state of the registry. Data exists in Play Mode only: before bootstrap the model isn't created, and outside play the window shows a hint.

Summary: loaded (active / survivors) and inflight across all groups, unique owner count, and separately how many owners were destroyed without `Release` (their locks live until the next sweep). One progress bar per group shows survivor fill against that group's `SurvivorCapacity`.

The table holds one row per (group, `AssetReference`) pair — one ref in two groups gives two rows (column headers in the window are in Russian):

| Column | Meaning |
|---|---|
| Asset (`Ассет`) | Name of the loaded object, otherwise the file name resolved from the GUID |
| Group (`Группа`) | Group key; `<default>` is the default group |
| State (`Состояние`) | `LOAD` (inflight) / `ACTIVE` / `SURVIVOR` |
| Refs (`Ссылок`) | How many owners in the group hold the ref; several holds of one owner count once. `IsHeldByAnyOwner` decides on whether it's above zero |
| Type (`Тип`) | Actual type of the loaded object |
| LRU | Position in the group's survivor queue; `1/N` is the head, evicted first |

Expanding a row reveals the GUID, the named owner list (destroyed ones are marked), and a button that pings the asset in the Project window.

Sorting: `LOAD` → `ACTIVE` → `SURVIVOR`; within active, by descending refs; within survivors, by group and eviction order. An `ACTIVE` row with zero refs is highlighted yellow: by contract such a ref must sit in survivors, otherwise it never reaches eviction and lingers until `Cleanup` — a leak indicator.

The snapshot is rebuilt in `OnInspectorUpdate` (~10 times/sec, only for a visible window); the "Авто" toggle disables it, next to a manual "Обновить" button, a "Конфиг" button (pings `AssetCacheSettings` through the same command as the menu item), and a name/group/GUID filter. The window is read-only: it never calls `Release` or triggers eviction.

It lives in `Editor/` inside the package's runtime assembly under `#if UNITY_EDITOR`, so it reads the `internal` `AssetCacheGroupModel` dictionaries directly, without widening the model's public API.

### `[AssetCacheGroup]` attribute

Put it on a `string` field holding a group key. The Odin drawer replaces the field with a dropdown of group keys from `AssetCacheSettings` (the list is cached for 1 s, so config edits show up almost immediately). The look matches `ValueSelector`:

- an empty value (default group) is shown as a red `——[NULL]——`;
- a key missing from the settings is shown in red, tagged `[нет в AssetCacheSettings]`;
- the `×` button on the right clears the selection — the field goes back to the default group.

### Debug logs

Debug trace: in `DebugSettings` → `Log Settings` → `AssetCacheDebugLogs` toggle. Only effective if global `DebugMode` is on. Output: `HIT / JOIN / LOAD / REL / SWEEP / EVICT-skip / EVICT`, each entry tagged `group=<key>` (`<default>` for the default group).

---

## Public API

```csharp
namespace Vortex.Unity.AssetCacheSystem.Bus
{
    public static class AssetCache
    {
        public static IAssetCacheController Controller { get; }
        public static AssetCacheConfig      Config     { get; }
        public static AssetCacheModel       Data       { get; }
        public static bool                  IsReady    { get; }
        public static InitValve             OnReady    { get; }
        public static event Action          OnRelease;

        public static UniTask<T> Load<T>(object owner, AssetReference reference,
            CancellationToken ct = default, string group = null) where T : UnityEngine.Object;
        public static T          LoadSync<T>(object owner, AssetReference reference,
            string group = null) where T : UnityEngine.Object;
        public static void       Release(object owner, string group = null);
    }
}

namespace Vortex.Unity.AssetCacheSystem.Abstractions
{
    public interface IAssetCacheController
    {
        bool             IsInitialized  { get; }
        AssetCacheModel  Model          { get; }
        event Action     OnInitialized;
        event Action     OnReleased;

        void Init();
        void Cleanup();
        UniTask<T> Load<T>(object owner, AssetReference reference, CancellationToken ct = default,
            string group = null) where T : UnityEngine.Object;   // OCE: ct cancelled or owner released
        T LoadSync<T>(object owner, AssetReference reference, string group = null)
            where T : UnityEngine.Object;                         // null on failure
        void Release(object owner, string group = null);
    }
}

namespace Vortex.Unity.AssetCacheSystem.Models
{
    public sealed class AssetCacheModel
    {
        public AssetCacheGroupModel                Default { get; }
        public IReadOnlyList<AssetCacheGroupModel> GetGroups();          // default first
        public AssetCacheGroupModel                GetGroup(string key); // empty → Default, unknown → null

        // Aggregates across all groups
        public bool IsLoaded(AssetReference reference);
        public int  LoadedCount    { get; }
        public int  InflightCount  { get; }
        public int  SurvivorsCount { get; }
        public int  OwnersCount    { get; }   // unique owners
    }

    public sealed class AssetCacheGroupModel
    {
        public string Key              { get; }   // null — default group
        public int    SurvivorCapacity { get; }
        public bool   IsDefault        { get; }

        public bool IsLoaded(AssetReference reference);
        public int  LoadedCount    { get; }
        public int  InflightCount  { get; }
        public int  SurvivorsCount { get; }
        public int  OwnersCount    { get; }
    }
}

namespace Vortex.Unity.AssetCacheSystem
{
    [Serializable]
    public sealed class AddressableAssetHandle<T> : AssetHandle<T> where T : UnityEngine.Object
    {
        public override bool       IsLoaded { get; }
        public override T          Asset    { get; }
        public override UniTask<T> LoadAsync(CancellationToken ct);
        public override void       Release();
    }
}

namespace Vortex.Unity.AssetCacheSystem.Handler
{
    public class DataStorageTransport : DataStorage { }   // configured in the inspector
}

namespace Vortex.Unity.AssetCacheSystem.Attributes
{
    public class AssetCacheGroupAttribute : PropertyAttribute { }
}

namespace Vortex.Core.SettingsSystem.Model
{
    public class AssetCacheConfig
    {
        public AssetCacheConfig(int survivorCapacity, IReadOnlyList<AssetCacheGroupConfig> groups = null);
        public int                                  SurvivorCapacity { get; }   // default group
        public IReadOnlyList<AssetCacheGroupConfig> Groups           { get; }
    }

    public class AssetCacheGroupConfig
    {
        public AssetCacheGroupConfig(string key, int survivorCapacity);
        public string Key              { get; }
        public int    SurvivorCapacity { get; }
    }
}
```

`AssetCacheController` (Singleton), `InflightLoad`, `AssetCacheSettings` (SO), partial extensions of `SettingsModel`/`DebugSettings` — internal/config classes, not intended for direct consumer use.

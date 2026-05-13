# AssetCacheSystem

**Namespace:** `Vortex.Unity.AssetCacheSystem.*`
**Assembly:** `ru.vortex.unity.assetcachesystem` (defineConstraints: `ENABLE_ADDRESSABLES`)

---

## Purpose

Managed cache of Addressables assets with four core mechanics:

- **Owner-tracking** — which consumer holds which `AssetReference`.
- **Inflight deduplication** — parallel requests for the same ref resolve via a single `Addressables.LoadAssetAsync` call.
- **LRU survivors** — released but not yet unloaded assets sit in a buffer; a repeated request becomes an instant HIT.
- **Sweep of destroyed Unity owners** — every `Release` call scans for `MonoBehaviour`s destroyed without an explicit Release.

Consumer API is exactly two methods:
```csharp
UniTask<T> AssetCache.Load<T>(object owner, AssetReference reference, CancellationToken ct = default);
void       AssetCache.Release(object owner);
```

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
| `Vortex.Core.SettingsSystem` + `Vortex.Unity.SettingsSystem` | Reading `SurvivorCapacity`, debug flag |
| `Vortex.Core.Extensions` | `InitValve.Create` |
| `Vortex.Unity.Extensions` | `AssetDatabaseExt.GetSingletonAsset` (Editor menu) |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` SO marker |

`defineConstraints: ["ENABLE_ADDRESSABLES"]` in asmdef — the package compiles only in projects with Addressables enabled.

---

## Architecture

```
Bus/
  AssetCache (static)               — public API: Load / Release / OnReady / OnRelease
                                      bootstrap via [RuntimeInitializeOnLoadMethod]
                                      subscribes to Settings.OnInit → CreateController → Controller.Init

Controllers/
  AssetCacheController : Singleton  — IAssetCacheController implementation, partial split by topic
    ├── .cs            — Init / Cleanup / IsInitialized / Model / OnInitialized / OnReleased
    ├── .Loading.cs    — Load<T> / StartLoad / RegisterOwner / ReviveFromSurvivor
    └── .Releasing.cs  — Release / ReleaseOwner / IsHeldByAnyOwner / PushSurvivor / EvictIfOverflow

Abstractions/
  IAssetCacheController             — consumer API + lifecycle contract

Models/
  AssetCacheModel                   — runtime state: 4 dictionaries (Locks/Handles/Inflight/Survivors)
  InflightLoad                      — DTO for in-flight load: Handle + UniTaskCompletionSource

Config/
  AssetCacheSettings : SettingsPreset, ICoreAsset
                                    — SO in Resources/Settings/: SurvivorCapacity (default 32)
  SettingsExt/                      — SettingsModel partial extension via .asmref
    AssetCacheConfig                — immutable runtime config
    SettingsModelExtAssetCache      — AssetCache + AssetCacheDebugLogs properties
  DebugExt/                         — DebugSettings partial extension via .asmref
    DebugSettingsExtAssetsCache     — AssetCacheDebugLogs toggle (respects DebugMode)

Editor/
  MenuController                    — Vortex/Configs/AssetCache Settings → ping the asset
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
    ├── Settings.Data().AssetCache → Config
    ├── Controller = AssetCacheController.Instance
    ├── Controller.OnInitialized += NotifyReady     ← opens InitValve OnReady
    ├── Controller.OnReleased    += NotifyReleased
    └── Controller.Init()
    ↓
Controller.Init()
    ├── reads SurvivorCapacity + debugLogging from Settings.Data()
    ├── creates empty AssetCacheModel
    ├── IsInitialized = true
    └── OnInitialized?.Invoke() → AssetCache.OnReady opens
    ↓
Consumer:
    AssetCache.OnReady.Subscribe(handler) — runs immediately if already Ready
```

### `Load<T>(owner, reference, ct)` flow

```
1. ArgNullCheck: owner / reference
2. RegisterOwner(owner, reference)      ← Locks[owner] += reference
3. Lookup in Model.Handles
   ├── HIT  → ReviveFromSurvivor + return (T)result   (instant)
   └── miss → continue
4. Lookup in Model.Inflight
   ├── JOIN → await slot.Completion (with ct support)
   └── miss → continue
5. StartLoad<T>:
   ├── handle = Addressables.LoadAssetAsync<Object>(reference)
   ├── Inflight[reference] = { Handle, Completion }
   ├── await handle.ToUniTask()             ← waiter's ct does NOT abort the load
   ├── Handles[reference] = handle
   ├── slot.Completion.TrySetResult(loaded) ← broadcast to all JOIN-waiters
   ├── if (!IsHeldByAnyOwner(ref))          ← all waiters cancelled before completion
   │     PushSurvivor(ref)
   ├── ct.ThrowIfCancellationRequested()    ← waiter's OCE after successful load
   └── finally: Inflight.Remove(ref)
```

### `Release(owner)` flow

```
1. ArgNullCheck: owner
2. Sweep destroyed Unity owners:
   ├── Scan Locks for `o is Object uo && uo == null`
   └── For each — ReleaseOwner(dead, isSweep: true)
3. ReleaseOwner(owner, isSweep: false):
   ├── Locks.Remove(owner)
   └── For each ref:
       ├── IsHeldByAnyOwner(ref)? → skip (still held)
       └── Handles.Contains(ref)? → PushSurvivor(ref)

PushSurvivor:
   ├── Survivors.Remove(ref)        ← refresh position
   ├── Survivors.AddLast(ref)
   └── EvictIfOverflow:
       while (Survivors.Count > SurvivorCapacity):
         head = Survivors.First
         Survivors.RemoveFirst
         if (IsHeldByAnyOwner(head)) continue  ← someone revived, handle stays
         Addressables.Release(handle)
         Handles.Remove(head)
```

---

## Contract

### Guarantees

- **Inflight deduplication**: N parallel `Load(refX)` calls from different owners → **one** `Addressables.LoadAssetAsync` call, all wait on the same `UniTaskCompletionSource`.
- **Instant HIT**: a repeated `Load` for an active or survivor asset returns synchronously via `await`, with no network/disk IO.
- **Survivor revive**: an asset that landed in survivors is returned instantly on a repeat request — no `Addressables.Release` is called.
- **Cancellation semantics**: waiter's ct cancellation **does not abort** the actual load — it's needed for other waiters. A cancelled waiter gets `OperationCanceledException`, the load continues.
- **Cleanup is idempotent**: a repeated call is a no-op.
- **One handle per ref**: there is always at most one entry per `AssetReference` in `Handles`.

### Discipline

- A single `AssetCacheSettings` instance in `Resources/Settings/` is required.
- **Owner must call `Release`** for every Load-ownership, otherwise the asset is not unloaded (see Sweep below).
- Sweep only handles `UnityEngine.Object` owners destroyed without Release. **POCO owners** (plain C# classes) require manual Release discipline; otherwise their Locks entry and held handles live until `Cleanup`.
- An `AssetReference` must be requested **with the same T** across different `Load<T>` calls. The cast to T happens on the waiter side; using one ref with different T values causes `InvalidCastException` on the second request.

### Limitations

- `IsHeldByAnyOwner` lookup is O(L) by number of owners. Fine for typical projects with tens to hundreds of owners. For thousands you'd want a reverse index (not implemented in v1 — memory priority).
- `ReviveFromSurvivor` is O(n) via `LinkedList.Remove(value)`. For capacities of 32-128 it's negligible.
- Not thread-safe — all operations are Unity main-thread.

---

## Usage

### Register settings

In `Resources/Settings/` create an `AssetCacheSettings` asset:
- Project window → Create → Vortex → Settings Preset → AssetCacheSettings.
- Fill in `SurvivorCapacity` (default 32).
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
| `Load` before Settings bootstrap | NRE on `Controller.Load` (Controller == null). Subscribe to `OnReady` first |
| Parallel `Load(refX)` from 100 owners | One `Addressables.LoadAssetAsync`, all wait on the same `Completion`. HIT for the next ones |
| `Load<T1>(...)` then `Load<T2>(...)` for the same ref with T1 ≠ T2 | First cast works, second one fails with `InvalidCastException` |
| `ct` cancelled before load completes | OCE for that waiter; the actual load continues; result goes to other waiters or to survivors |
| `ct` cancelled for the only waiter | Load completes, asset goes to survivors, owner remains in Locks (call `Release(owner)` explicitly) |
| `Load(this, ref)` on an MB; MB destroyed without `Release` | Sweep on next `Release` of any owner detects `o is Object && o == null` and releases |
| POCO owner without `Release` | Leak: Locks/Handles live until `Cleanup` |
| Survivor capacity = 0 | Every `Release` immediately calls `Addressables.Release` |
| `Release(owner)` for an owner with no active Locks | Warning in debug mode, no-op in release |
| `Settings.Data().AssetCache == null` (no SO) | `Debug.LogError` in `CreateController`/`Init`, controller is not created, `Load` will NRE |
| `App.OnExit` | `Dispose` → `Controller.Cleanup` → all Handles + Inflight released |
| Repeated `Init` after `Cleanup` | Fine: a new `Model` is created, registries from scratch |

---

## Editor

`Tools/Vortex/Configs/AssetCache Settings` — pings the settings asset in the Project window. Uses `AssetDatabaseExt.GetSingletonAsset<AssetCacheSettings>()` — expects a single instance in the project; logs an error on multiplicity.

Debug trace: in `DebugSettings` → `Log Settings` → `AssetCacheDebugLogs` toggle. Only effective if global `DebugMode` is on. Output: `HIT / JOIN / LOAD / REL / SWEEP / EVICT-skip / EVICT`.

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
            CancellationToken ct = default) where T : UnityEngine.Object;
        public static void       Release(object owner);
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
        UniTask<T> Load<T>(object owner, AssetReference reference, CancellationToken ct = default)
            where T : UnityEngine.Object;
        void Release(object owner);
    }
}

namespace Vortex.Unity.AssetCacheSystem.Models
{
    public sealed class AssetCacheModel
    {
        public bool IsLoaded(AssetReference reference);
        public int  LoadedCount    { get; }
        public int  InflightCount  { get; }
        public int  SurvivorsCount { get; }
        public int  OwnersCount    { get; }
    }
}

namespace Vortex.Core.SettingsSystem.Model
{
    public class AssetCacheConfig
    {
        public AssetCacheConfig(int survivorCapacity);
        public int SurvivorCapacity { get; }
    }
}
```

`AssetCacheController` (Singleton), `InflightLoad`, `AssetCacheSettings` (SO), partial extensions of `SettingsModel`/`DebugSettings` — internal/config classes, not intended for direct consumer use.

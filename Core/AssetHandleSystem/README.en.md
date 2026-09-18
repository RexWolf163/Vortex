# AssetHandleSystem

**Package:** driver-neutral wrapper over a Unity asset with a unified async contract

| Part | Namespace | Assembly | Layer |
|---|---|---|---|
| Contract + Direct | `Vortex.Core.AssetHandleSystem.*` | `ru.vortex.core.assethandle` | Core (Layer 3) |
| Addressable impl | `Vortex.Unity.AssetCacheSystem.*` | `ru.vortex.unity.assetcachesystem` (`ENABLE_ADDRESSABLES`) | Unity (Layer 2) |

Physical layout: `Assets/Vortex/Core/AssetHandleSystem/` (contract + `DirectAssetHandle`), `Assets/Vortex/Unity/AssetCacheSystem/AddressableAssetHandle.cs` (a thin adapter lives next to `AssetCache` — doesn't warrant its own package).

---

## Purpose

One type for "direct reference to an asset" and "lazy loading of an asset" with identical consumer code. Consumers never write `if (Addressables enabled) ... else ...` — the switch happens at the preset level via a polymorphic `[SerializeReference]` field.

### When to use

The core use case is **reusable systems** shipped as packages and used in different projects with different asset infrastructures. Such a system must not force a particular loading mechanism on its consumers, but also can't assume Addressables are available. `AssetHandle<T>` lets the package **defer that decision to the consumer preset**.

**Example.** The `GallerySpritesSystem` package declares a preset with `[SerializeReference] AssetHandle<Sprite> fullscreen`. The same package ships to:

- **Project A** (Addressables enabled): the designer picks `AddressableAssetHandle<Sprite>` in the inspector — the heavy fullscreen sprite loads lazily.
- **Project B** (no Addressables): the designer picks `DirectAssetHandle<Sprite>` — the sprite is held as a direct reference.

The `GallerySpritesSystem` code (model, viewer, all Show/Hide logic) **is identical across both projects**. The same `await preset.Fullscreen.LoadAsync()` works in either case. When project B adds Addressables tomorrow, some presets can be flipped to lazy without touching the gallery package itself.

If the package targets a single project where the asset infrastructure is fixed (e.g. Addressables only), the abstraction is overkill — use `AssetReference` / `AssetCache` directly.

Features:

- **Unified async contract** — `UniTask<T> LoadAsync(...)` for both implementations.
- **`DirectAssetHandle<T>`** — direct reference to a Unity asset, works in any project (including without Addressables).
- **`AddressableAssetHandle<T>`** — lazy via Addressables, delegates to `Vortex.Unity.AssetCacheSystem.AssetCache` (dedup, LRU survivors, destroyed-owner sweep — for free).
- **`[SerializeReference]` polymorphism** — the designer picks the implementation for each field via the standard Odin picker in the inspector.
- **Driver neutrality** — the Core contract doesn't reference `UnityEngine.AddressableAssets`. `AssetReference` lives only inside the Unity implementation; the domain preset holds only the abstract `AssetHandle<T>`.

Out of scope:

- **Refcount across different handles** — not at the contract level. `AddressableAssetHandle` delegates to `AssetCache`, which has its own owner-based refcount.
- **Automatic handling of missing Addressables** — if the project disables Addressables, `AddressableAssetHandle` in the preset deserializes as `null`. The consumer will hit an NRE on access — this is intentional (a fallback would mask a config bug).
- **Managing the handle container's lifetime** — that's the consumer's job (preset / model cache). The handle owns only its underlying resource, not its own instance.

---

## Dependencies

| Dependency | Part | Purpose |
|-------------|---|-----------|
| `UniTask` | Core | Async contract |
| Unity Engine | Core | `UnityEngine.Object`, `Sprite` (example), `SerializeField` |
| `Vortex.Unity.AssetCacheSystem` | Unity | `AssetCache.Load` / `AssetCache.Release` — underlying mechanism |
| `Vortex.Core.LoggerSystem` | Unity | Warning on `LoadAsync` after `Release` |
| Unity Addressables | Unity | `AssetReference`, only inside `AddressableAssetHandle` |

---

## Contract

```csharp
[Serializable]
public abstract class AssetHandle<T> where T : UnityEngine.Object
{
    public abstract bool IsLoaded { get; }
    public abstract T Asset { get; }
    public abstract UniTask<T> LoadAsync(CancellationToken ct);
    public UniTask<T> LoadAsync();                    // overload without ct
    public abstract void Release();
}
```

**Invariants:**

- **I1.** `Asset != null && IsLoaded == true` — equivalent.
- **I2.** Before `Release`: a repeated `LoadAsync` returns the same `T` instance.
- **I3.** `Release` is idempotent.
- **I4.** `DirectAssetHandle.Release()` — guaranteed no-op.
- **I5.** The Core assembly contains no references to `UnityEngine.AddressableAssets`.
- **I6.** `AddressableAssetHandle` is the sole owner of its Addressables handle through `AssetCache` (`owner = this`).

---

## Implementations

### `DirectAssetHandle<T>`

Single field `[SerializeField] T asset`. `LoadAsync` returns `UniTask.FromResult(asset)`. `Release` is empty.

### `AddressableAssetHandle<T>`

- `[SerializeField] AssetReference reference` — Addressables asset link.
- `[NonSerialized] T _cached` — cached loaded asset.
- `[NonSerialized] bool _released` — terminal flag.

`LoadAsync(ct)` flow:
1. If `_released` → warning + `default(T)`.
2. If `_cached != null` → return immediately.
3. Otherwise — `await AssetCache.Load<T>(this, reference, ct)` → save to `_cached`.

`Release()` flow:
1. If `_released` — return.
2. `AssetCache.Release(this)` → release in the cache.
3. `_cached = null`, `_released = true`.

---

## Usage

### Preset

```csharp
public class GallerySpritePreset : RecordPreset<GallerySpriteModel>
{
    [SerializeReference] private AssetHandle<Sprite> fullscreen;
    public AssetHandle<Sprite> Fullscreen => fullscreen;
}
```

In the inspector, the Odin picker for `fullscreen` lets you choose between `DirectAssetHandle<Sprite>` (field — direct ref) and `AddressableAssetHandle<Sprite>` (field — `AssetReference`).

### Consumer

```csharp
async UniTask ShowFullscreen(GallerySpritePreset preset)
{
    var sprite = await preset.Fullscreen.LoadAsync();  // works for both Direct and Lazy
    _imageComponent.sprite = sprite;
}

void CloseFullscreen(GallerySpritePreset preset)
{
    preset.Fullscreen.Release();                        // Direct — no-op, Lazy — releases
}
```

### With CancellationToken

```csharp
async UniTask ShowFullscreen(GallerySpritePreset preset, CancellationToken ct)
{
    var sprite = await preset.Fullscreen.LoadAsync(ct);
    _imageComponent.sprite = sprite;
}
```

---

## Edge cases

| Situation | Behaviour |
|---|---|
| `LoadAsync` before `Release` — repeated call | Returns the same `T` instance; for Lazy — without touching `AssetCache` |
| `LoadAsync` after `Release` | Warning to logger, returns `default(T)` |
| `Release` before `LoadAsync` (Lazy) | No-op — nothing to release; the `_released` flag is set |
| Repeated `Release` | Idempotent no-op |
| `DirectAssetHandle` with `asset == null` | `IsLoaded == false`, `LoadAsync` returns `null`. Valid as "empty by design" |
| `AddressableAssetHandle.reference == null` | `AssetCache.Load` throws `ArgumentNullException` — fail-fast on a config bug |
| Project without Addressables: scene contains `AddressableAssetHandle` | Field deserializes as `null`. Consumer must explicitly null-check or catch NRE. No automatic fallback |
| Cancellation via `ct` during load (Lazy) | `AssetCache.Load` throws `OperationCanceledException`. Handle stays not-loaded (retry possible) |

---

## Editor

Standard Odin `[SerializeReference]` picker in the inspector. No custom drawer is written.

# AssetHandleSystem (Unity)

**Package:** Unity implementations of the `AssetHandle<T>` contract from `Vortex.Core.AssetHandleSystem`

| Part | Namespace | Assembly |
|---|---|---|
| Direct reference | `Vortex.Unity.AssetHandleSystem.*` | `ru.vortex.unity.assethandle` |
| Lazy loading via Addressables | `Vortex.Unity.AssetCacheSystem.*` | `ru.vortex.unity.assetcachesystem` (`ENABLE_ADDRESSABLES`) |

Physical layout: `Assets/Vortex/Unity/AssetHandleSystem/` (`DirectAssetHandle`), `Assets/Vortex/Unity/AssetCacheSystem/AddressableAssetHandle.cs` (a thin adapter lives next to `AssetCache` and does not deserve its own package).

The contract, invariants and handle lifecycle rules are in the [Core README](../../Core/AssetHandleSystem/README.en.md).

---

## Purpose

The designer picks the `AssetHandle<T>` implementation for each preset field in the inspector — consumer code does not change.

**Example.** The `GallerySpritesSystem` package declares a preset with `[SerializeReference] AssetHandle<Sprite> fullscreen`. The same package ships to:

- **Project A** (Addressables enabled): the designer picks `AddressableAssetHandle<Sprite>` — the heavy fullscreen sprite loads lazily.
- **Project B** (no Addressables): the designer picks `DirectAssetHandle<Sprite>` — the sprite is held as a direct reference.

The `GallerySpritesSystem` code (model, viewer, Show/Hide logic) **is identical in both projects**: the same `await preset.Fullscreen.LoadAsync()`. If project B enables Addressables, some presets can switch to lazy loading without touching the package.

If the project uses Addressables only, the abstraction is overkill — use `AssetReference` / `AssetCache` directly.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.AssetHandleSystem` | `AssetHandle<T>` contract |
| `UniTask` | Async contract |
| Unity Engine | `UnityEngine.Object`, `SerializeField` |
| `Vortex.Unity.AssetCacheSystem` (for `AddressableAssetHandle`) | `AssetCache.Load` / `AssetCache.Release` — underlying mechanism |
| Unity Addressables (for `AddressableAssetHandle`) | `AssetReference` |

---

## Implementations

Both narrow the contract constraint to `where T : UnityEngine.Object`.

### `DirectAssetHandle<T>`

Direct reference to an asset. Single field `[SerializeField] T asset`. `LoadAsync` returns `UniTask.FromResult(asset)`, `Release` is empty. The asset stays in memory while the preset lives. Works in any project, including without Addressables; suits light and always-needed assets.

### `AddressableAssetHandle<T>`

Lazy loading via Addressables on top of `AssetCache` (dedup, LRU survivors, destroyed-owner sweep — for free). Compiled only with `ENABLE_ADDRESSABLES`.

- `[SerializeField] AssetReference reference` — Addressables asset link.
- `[NonSerialized] T _cached` — cache of the loaded asset; the only state (the handle is reusable).
- The handle is the sole owner of its Addressables handle through `AssetCache` (`owner = this`).

`LoadAsync(ct)` flow:
1. If `_cached != null` → warning (repeated Load without Release) + the same instance, without touching `AssetCache`.
2. Otherwise — `await AssetCache.Load<T>(this, reference, ct)` → store in `_cached`.

`Release()` flow:
1. If `_cached == null` — no-op.
2. `AssetCache.Release(this)`; `_cached = null`. The handle is ready for `LoadAsync` again.

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

In the inspector, the Odin picker for `fullscreen` lets you choose between `DirectAssetHandle<Sprite>` (field — direct reference) and `AddressableAssetHandle<Sprite>` (field — `AssetReference`). No custom drawer is written.

### Consumer

```csharp
async UniTask ShowFullscreen(GallerySpritePreset preset, CancellationToken ct)
{
    var sprite = await preset.Fullscreen.LoadAsync(ct);  // works for both Direct and Addressable
    _imageComponent.sprite = sprite;
}

void CloseFullscreen(GallerySpritePreset preset)
{
    preset.Fullscreen.Release();                          // Direct — no-op, Addressable — releases
}
```

---

## Edge cases

| Situation | Behavior |
|---|---|
| `DirectAssetHandle` with `asset == null` | `IsLoaded == false`, `LoadAsync` returns `null` — valid as "empty by design" |
| Repeated `LoadAsync` on `AddressableAssetHandle` without `Release` | Same instance + warning, without touching `AssetCache` |
| `AddressableAssetHandle.reference == null` | `AssetCache.Load` throws `ArgumentNullException` — fail-fast on a config bug |
| Project without Addressables, the preset contained `AddressableAssetHandle` | The field deserializes as `null`. The consumer must check for a null handle. No automatic fallback — masking a config bug would be worse |
| Cancellation via `ct` during loading (Addressable) | `AssetCache.Load` throws `OperationCanceledException`; the handle stays not loaded (retry possible) |

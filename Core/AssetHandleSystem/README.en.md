# AssetHandleSystem (Core)

**Package:** driver-neutral contract of a pointer to a platform asset with a unified async contract

| Part | Namespace | Assembly |
|---|---|---|
| Contract | `Vortex.Core.AssetHandleSystem.*` | `ru.vortex.core.assethandle` |

Implementations (direct reference, lazy loading) live in the platform layer.

---

## Purpose

One type for "direct reference to an asset" and "lazy loading of an asset" with identical consumer code. Consumers never write `if (lazy loading available) ... else ...` — the implementation is chosen in the holder's data via a polymorphic field.

### When to use

The core use case is **reusable systems** shipped as packages and used in different projects with different asset infrastructures. Such a system must not force a particular loading mechanism on consumers and cannot know in advance which loading mechanism the project has. `AssetHandle<T>` lets you **defer that decision to the holder's data**: the system code is the same, the implementation is picked in the preset.

If a package targets a single project with a fixed asset infrastructure, the abstraction is overkill — use the platform loading mechanism directly.

Capabilities:

- **Unified async contract** — `UniTask<T> LoadAsync(...)` for any implementation.
- **Implementation polymorphism** — the holder keeps an abstract `AssetHandle<T>`, the concrete implementation is chosen in data.
- **Driver neutrality** — the contract depends neither on the platform nor on the loading mechanism; they live only in platform-layer implementations.

Out of scope:

- **Implementations** — the platform layer.
- **Refcount across different handles** — not at the contract level; a lazy implementation relies on its loading mechanism's refcount.
- **Handle lifecycle is the holder's, not the consumers'.** `Load` and `Release` are called by the handle's **owner** (the preset / model cache that opened a screen) — once per cycle. Consumers that need the asset only read via `LoadAsync` and **do not call `Release`**: otherwise they pull the asset out from under other readers of the same handle. A handle is the single owner of its underlying resource; the holder manages its own instance.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `UniTask` | Async contract |

---

## Usage

### Holder

```csharp
public class MyCardPreset
{
    // Polymorphic field: the concrete implementation is chosen by data.
    private AssetHandle<MyAsset> fullscreen;
    public AssetHandle<MyAsset> Fullscreen => fullscreen;
}
```

### Consumer

```csharp
async UniTask ShowFullscreen(MyCardPreset preset, CancellationToken ct)
{
    var asset = await preset.Fullscreen.LoadAsync(ct);  // same for any implementation
    Show(asset);
}

void CloseFullscreen(MyCardPreset preset)
{
    preset.Fullscreen.Release();                         // direct — no-op, lazy — releases
}
```

---

## Contract

```csharp
[Serializable]
public abstract class AssetHandle<T> where T : class
{
    public abstract bool IsLoaded { get; }
    public abstract T Asset { get; }
    public abstract UniTask<T> LoadAsync(CancellationToken ct);
    public UniTask<T> LoadAsync();                    // overload without ct
    public abstract void Release();
}
```

The `where T : class` constraint is minimal. Platform-layer implementations may narrow it to their platform's asset base type.

**Invariants:**

- **I1.** `Asset != null` and `IsLoaded == true` are equivalent.
- **I2.** Before `Release`: a repeated `LoadAsync` returns the same `T` instance.
- **I3.** `Release` is idempotent.
- **I4.** For a direct reference `Release()` is guaranteed to be a no-op.
- **I5.** The contract contains no references to the platform or loading mechanisms.
- **I6.** `Release` is not terminal: the handle is reusable — the next `LoadAsync` loads the asset again.

---

## Edge cases

| Situation | Behavior |
|---|---|
| `LoadAsync` before `Release` — repeated call | Same `T` instance |
| `LoadAsync` after `Release` | The handle is reusable — a lazy implementation loads the asset again |
| `Release` before `LoadAsync` | No-op — nothing to release |
| Repeated `Release` | Idempotent, no-op |
| Direct reference without an asset | `IsLoaded == false`, `LoadAsync` returns `null` — valid as "empty by design" |
| Cancellation via `ct` during loading | `OperationCanceledException`; the handle stays not loaded (retry possible) |
| Implementation unavailable in the project (loading mechanism disabled) | The holder's field is empty (`null`); the consumer must check for a null handle. No automatic fallback |

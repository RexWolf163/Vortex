# GallerySpritesSystem

**Namespace:** `Vortex.Sdk.GallerySpritesSystem.*`
**Assembly:** `ru.vortex.sdk.gallery.sprites`
**Layer:** SDK
**Package type:** consumer for [GallerySystem](../GallerySystem/README.en.md) — art sprite gallery cards

---

## Purpose

One of three gallery content types — **art sprite cards**: a fullscreen illustration the player opens from the gallery and views. Classic CG content in JRPGs / visual novels: preview tile in the list, click → fullscreen with the large image.

Features:

- **`GallerySpriteModel`** implements `IGalleryEntry` from GallerySystem — enters the gallery pool automatically; `GalleryView` picks it up via `Database.GetRecords(typeof(IGalleryEntry))`.
- **`GallerySpritePreset`** holds the standard `RecordPreset<T>` fields (Guid, Name, Description, Icon-preview) plus a polymorphic `AssetHandle<Sprite>` for the fullscreen asset.
- **Designer chooses per preset**: `DirectAssetHandle<Sprite>` (fullscreen always in memory) or `AddressableAssetHandle<Sprite>` (Addressables lazy load) via the Odin `[SerializeReference]` picker.
- **Show through a static bus** — the model knows nothing about the viewer, it just fires `RequestShow`/`RequestHide`. One registered viewer per project.
- **Unlocking is orthogonal** — "unlocked" status lives in `RecordMarksSystem`; this package doesn't write there.

Out of scope:

- **The fullscreen-viewer UI component** — not in the package; lives in the app's UI layer (art direction and animation are game-specific).
- **Card unlocking** — the job of story mechanics (Nani command, drop trigger); they write directly to `RecordMarksBus`.
- **Asset loading** — delegated to `AssetHandle<T>`, which for the Addressable implementation delegates to `AssetCache`.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Sdk.GallerySystem` | `IGalleryEntry` contract |
| `Vortex.Core.AssetHandleSystem` | Fullscreen asset abstraction |
| `Vortex.Core.DatabaseSystem` | `Record`, entry into the gallery pool |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>` |
| `Vortex.Core.Extensions.LogicExtensions` | `ObjectExtCopy.CopyFrom`, `ObjectExtDeepClone.DeepCopy` |
| `Vortex.Core.LoggerSystem` | Warning when no viewer, LogError on re-registration |
| UniTask | Async contract for `LoadFullscreenAsync` |

---

## Architecture

```
GallerySpritePreset (SO)
    ├── [SerializeField] Icon (preview)         — from RecordPreset<T> base
    └── [SerializeReference] fullscreen         — DirectAssetHandle<Sprite> OR AddressableAssetHandle<Sprite>
                    │
                    │   Database.GetRecords → RecordPreset<T>.GetData()
                    │       ↓
                    ▼
GallerySpriteModel (Record, IGalleryEntry)
    ├── GuidPreset, Icon, Name, Description     — public (from Record + CopyFrom)
    ├── internal Fullscreen                     — deep-clone of preset's FullscreenTemplate
    ├── Show()  → GallerySpritesBus.RequestShow(this, ct)
    └── Hide()  → GallerySpritesBus.RequestHide(this)
                    │
                    ▼
GallerySpritesBus (static, registry)
    ├── Register(showHandler, hideHandler)      — one viewer per project
    ├── Unregister()
    ├── RequestShow(model, ct)  → showHandler   — Warning if no viewer registered
    └── RequestHide(model)      → hideHandler
                    │
                    ▼
Viewer (MonoBehaviour in the app UI layer — not in this package)
    ├── Awake:    GallerySpritesBus.Register(HandleShow, HandleHide)
    ├── OnDestroy: GallerySpritesBus.Unregister()
    ├── HandleShow(model, ct): await model.LoadFullscreenAsync(ct); show UI
    ├── HandleHide(model): if (_current == model) InternalClose()
    ├── OnDisable: InternalClose()
    └── InternalClose(): _current?.ReleaseFullscreen(); _current = null; hide UI
```

---

## Contract

### `GallerySpriteModel : Record, IGalleryEntry`

```csharp
public class GallerySpriteModel : Record, IGalleryEntry
{
    public Sprite Icon { get; protected set; }
    internal AssetHandle<Sprite> Fullscreen { get; set; }

    public bool CopyFrom(SoData source);       // override, deep-clone for Fullscreen
    public void Show();
    public void Show(CancellationToken ct);
    public void Hide();
}
```

### `GallerySpritePreset : RecordPreset<GallerySpriteModel>`

```csharp
public class GallerySpritePreset : RecordPreset<GallerySpriteModel>
{
    [SerializeReference] private AssetHandle<Sprite> fullscreen;
    public AssetHandle<Sprite> FullscreenTemplate => fullscreen;
}
```

### `GallerySpritesBus` (static)

```csharp
public static class GallerySpritesBus
{
    public static bool HasViewer { get; }

    public static void Register(Action<GallerySpriteModel, CancellationToken> showHandler,
                                Action<GallerySpriteModel> hideHandler);
    public static void Unregister();

    public static void RequestShow(GallerySpriteModel model, CancellationToken ct);
    public static void RequestHide(GallerySpriteModel model);
}
```

### `GallerySpriteController` (extension)

```csharp
public static class GallerySpriteController
{
    public static UniTask<Sprite> LoadFullscreenAsync(this GallerySpriteModel model, CancellationToken ct);
    public static UniTask<Sprite> LoadFullscreenAsync(this GallerySpriteModel model);
    public static void            ReleaseFullscreen(this GallerySpriteModel model);
}
```

### Invariants

- **I1.** `Fullscreen` is internal; from outside the assembly it can neither be read nor mutated.
- **I2.** The model's handle is an independent deep-clone of the preset's handle: `_cached`/`_released` are not shared.
- **I3.** Only one viewer is active at a time (registry).
- **I4.** `Show`/`Hide` are synchronous (as `IGalleryEntry` requires); inside — only a bus call.

---

## Creating a card (for the designer)

1. **Create a preset:** Project → Create → Vortex → Presets → Gallery → Sprite. You get a `GallerySpritePreset`.
2. **Fill the base fields:** Name, Description, Icon (preview for the gallery tile).
3. **Pick an `AssetHandle<Sprite>` implementation:** click the `fullscreen` field in the inspector; the Odin picker offers:
   - **`DirectAssetHandle<Sprite>`** — direct reference. Fullscreen always in memory. Good for light / always-needed sprites.
   - **`AddressableAssetHandle<Sprite>`** — Addressables lazy-load. Loads on demand, unloads on `Release`.
4. **Assign the asset** to the chosen implementation.
5. **Unlock mark:** `RecordMarksSettings` must contain a mark that story mechanics will apply to the card's `GuidPreset` for "unlocked". The gallery scene designer specifies that mark in `GalleryView.marks`.

The card enters `GalleryView` automatically when its type is allowed (`allowedTypes` empty or contains it, `deniedTypes` doesn't) and its `GuidPreset` is tagged with a listed mark.

---

## Viewer implementation guide

The package **does not ship a UI component** — the fullscreen viewer implementation lives in the app's UI layer. Template:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Sdk.GallerySpritesSystem.Bus;
using Vortex.Sdk.GallerySpritesSystem.Controllers;
using Vortex.Sdk.GallerySpritesSystem.Models;

public class FullscreenSpriteViewer : MonoBehaviour
{
    [SerializeField] private GameObject panel;   // UI panel root
    [SerializeField] private Image image;        // component to display the sprite
    [SerializeField] private Button closeButton;

    private GallerySpriteModel _current;

    private void Awake()
    {
        GallerySpritesBus.Register(HandleShow, HandleHide);
        closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDestroy()
    {
        GallerySpritesBus.Unregister();
        closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnDisable()
    {
        InternalClose();
    }

    private async void HandleShow(GallerySpriteModel model, CancellationToken ct)
    {
        // Transitioning to a new entry: close the old one without calling its Hide (avoid a bus loop).
        if (_current != null && _current != model)
            InternalClose();

        _current = model;
        var sprite = await model.LoadFullscreenAsync(ct);
        if (_current != model) return;   // another Show came in during await — this one is cancelled

        image.sprite = sprite;
        panel.SetActive(true);
    }

    private void HandleHide(GallerySpriteModel model)
    {
        if (_current != model) return;
        InternalClose();
    }

    private void OnCloseClicked()
    {
        // Via the model — the single public "close" path; it comes back into HandleHide.
        // This lets loggers/analytics see the close event.
        _current?.Hide();
    }

    private void InternalClose()
    {
        if (_current == null) return;
        _current.ReleaseFullscreen();
        _current = null;
        panel.SetActive(false);
    }
}
```

**Discipline (important):**

- **`Register` exactly once** in the project. A second viewer will be rejected by the bus with a LogError.
- **`InternalClose` does not call `model.Hide()`** — that would create a bus loop. When the viewer closes on its own initiative (OnDisable, transition to a new entry) — only direct `ReleaseFullscreen` + hide UI.
- **`OnCloseClicked` calls `_current.Hide()`**, not `InternalClose()` directly. This routes the close through model → bus → HandleHide → InternalClose, and any listener (analytics) sees the event.
- **Race on rapid switching**: while `await LoadFullscreenAsync` is running, a new `Show` may come in. Check `if (_current != model) return` after the await.

---

## Edge cases

| Situation | Behavior |
|---|---|
| `Show()` with no registered viewer | Warning to logger, command lost |
| `Hide()` with no viewer | Warning to logger |
| Repeated `Register` while a viewer is active | LogError, the second is rejected; the first keeps working |
| `Unregister` without a prior `Register` | Idempotent no-op |
| Viewer destroyed without `Unregister` | The handler delegates keep a reference to the destroyed MonoBehaviour → NRE on the next `RequestShow`. **Always `Unregister` in OnDestroy** |
| A new entry arrives while loading the previous one | The viewer checks `_current` after the await; if it changed, the previous Show is cancelled |
| `AddressableAssetHandle` in a project without Addressables | On deserialization the field is null; `LoadFullscreenAsync` throws NRE. Decide up-front in the project (see AssetHandle README) |
| `Fullscreen` handle instance in model vs preset | Different objects (deep-clone in CopyFrom). Release on the model doesn't affect the preset's handle |
| `LoadFullscreenAsync` called outside the package assembly | Compile error — the extension method is visible, but it requires internal `Fullscreen`, which is inaccessible from outside. Always go through the model as the handle holder |

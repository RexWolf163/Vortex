# GallerySystem

**Package:** SDK gallery contract + Unity pool widget

| Part | Namespace | Assembly | Layer |
|---|---|---|---|
| Contract | `Vortex.Sdk.GallerySystem.*` | `ru.vortex.sdk.gallery` | SDK (Layer 3) |
| Widget | `Vortex.Sdk.GallerySystem.View.*` | `ru.vortex.unity.gallery.view` | Unity/UI (Layer 2) |

Both live under `Assets/Vortex/Sdk/GallerySystem/` — the contract and comparers at the root, the widget under `View/`.

---

## Purpose

Universal gallery pool: the SDK part defines the `IGalleryEntry` contract and the `IGalleryEntryComparer` sorter extension point; the Unity part provides the `GalleryView` widget, which collects records from `Database`, filters and sorts them, and populates a `Pool` from `Vortex.Unity.UI.PoolSystem`. Unlocking is out of scope — mark status is managed externally via `RecordMarksBus`; the package only reads.

Features:

- Thin contract with five members: `GuidPreset`, `Icon`, `Name`, `Show()`, `Hide()`.
- Synchronous `Show`/`Hide` — async work (asset loading, script playback) is the implementation's responsibility.
- Inspector-configurable "single gallery scene": mark set, allowed/denied types, denied guids, icon overrides, sorter, locked mode.
- Focus and Show are decoupled: Focus updates the selected-guid reactive and the static "last viewed" memory; Show is invoked by the card itself.
- External positioner `Open(guid)` — for search/navigation mechanics.
- Empty-pool stub via `TweenerHub` (Vortex-native, optional).
- All inspector dropdowns use Vortex-native `[ValueSelector]`, no Odin.

Out of scope:

- Storing "unlocked" facts — handled by `RecordMarksSystem`. This package never touches marks.
- Displaying the actual card content (fullscreen viewer, nani script) — that's the `Show()` implementation in the supplier package's model.
- Card search mechanics — consumers use `Open(guid)` and `LastViewedGuid`, but the semantics are up to them.
- Heavy asset loading — on consumers (sprite cards via `IAssetHandle<Sprite>`, stories via `Naninovel.Script`).

---

## Dependencies

| Dependency | Part | Purpose |
|-------------|---|-----------|
| `Vortex.Core.LocalizationSystem` (`StringExt.TryTranslate`) | SDK | Used by `GalleryEntrySortByLocalizedName` |
| `Vortex.Core.DatabaseSystem` (`Database.GetRecords`) | View | Model source |
| `Vortex.Sdk.RecordMarksSystem` (`RecordMarksBus`, `RecordMarksSettings`) | View | Reading mark status + editor dropdown of available marks |
| `Vortex.Core.Extensions.ReactiveValues` (`StringData`, `BoolData`) | View | Reactives for highlight and locked state |
| `Vortex.Unity.DatabaseSystem.Attributes` (`DbRecord`) | View | Dropdown for `deniedGuids` |
| `Vortex.Unity.EditorTools.Attributes` (`ValueSelector`, `AutoLink`) | View | Inspector dropdowns and auto-binding |
| `Vortex.Unity.UI.PoolSystem` (`Pool`, `PoolItem`) | View | Pool machinery |
| `Vortex.Unity.UI.TweenerSystem` (`TweenerHub`) | View | Empty-pool stub |
| Unity Engine | both | `Sprite`, `MonoBehaviour` |

---

## Architecture

```
[SDK part]
    IGalleryEntry            — single-item contract
    IGalleryEntryComparer    — sorter marker
    Comparers/               — built-in comparers

[View part]
    GalleryView (MonoBehaviour)
        │
        ├── OnEnable → RefreshPool
        │       │
        │       ├─→ Database.GetRecords(typeof(IGalleryEntry))
        │       │
        │       ├─→ filter (allowedTypes ∩ ¬deniedTypes ∩ ¬deniedGuids ∩ marks ∩ lockedMode)
        │       │
        │       ├─→ sort (IGalleryEntryComparer or none)
        │       │
        │       ├─→ if empty: stubTweener.Forward()
        │       │
        │       └─→ pool.AddItem(preview, isLocked, selectedGuid, entry, callbacks) × N
        │
        ├── HandleFocus(guid) — updates selectedGuid + LastViewedGuid
        │
        ├── HandleShow(guid)  — extension point for subclasses (default no-op)
        │
        └── Open(guid)        — external positioner
```

---

## Contract (SDK)

### `IGalleryEntry`

```csharp
public interface IGalleryEntry
{
    string GuidPreset { get; }   // from RecordPreset<T>.GuidPreset
    Sprite Icon { get; }         // from RecordPreset<T>.Icon
    string Name { get; }         // from RecordPreset<T>.Name (raw, pre-localization)
    void Show();
    void Hide();
}
```

- Implement **on the Record model**, not on the ScriptableObject preset: `Database.GetRecords(typeof(IGalleryEntry))` returns models.
- Model must have `RecordType == Singleton` — `Database.GetRecords` only enumerates singletons.
- `Show`/`Hide` are synchronous. If the implementation kicks off async work, it owns its lifecycle.
- Implementations **must not** touch `RecordMarksBus` — mark status is managed externally.

### `IGalleryEntryComparer`

```csharp
public interface IGalleryEntryComparer : IComparer<IGalleryEntry> { }
```

Empty marker over `IComparer<IGalleryEntry>`. Any class implementing it automatically appears in the `sorter` dropdown of `GalleryView`. Without the marker, the reflection scan would have to inspect generic type arguments and risk pulling in system types.

### Built-in comparers

- **`GalleryEntrySortByRawName`** — `string.Compare(x.Name, y.Name, InvariantCultureIgnoreCase)`. Order stays stable across user locales.
- **`GalleryEntrySortByLocalizedName`** — `x.Name.TryTranslate()` vs `y.Name.TryTranslate()`, `CurrentCultureIgnoreCase`. Degrades to raw order when a key has no translation.

Consumer packages can add their own comparers — they show up in the dropdown automatically.

---

## Widget API

### Public

```csharp
public void Open(string guid);
public static string LastViewedGuid { get; }
```

- `Open(guid)` — sets focus on the given card if it is in the current pool. Warning + no-op if missing.
- `LastViewedGuid` — session-static, shared across all instances. Written on Focus and Open, persists until app restart (not serialized).

### Subclassing

`HandleShow(string guid)` is `protected virtual`. Override it to react to a card's Show event on the surrounding UI, after the card has invoked its own `entry.Show()` and `callbacks.OnShow()`.

---

## Card prefab contract

The card prefab goes into `pool.itemPrefab`. Widgets inside it read data via `PoolItem.GetData<T>()`:

| Type | Purpose |
|---|---|
| `Sprite` | preview icon |
| `BoolData` | isLocked → visual state of a locked card (silhouette/blur/grayscale — on the prefab) |
| `StringData` | selectedGuid — shared reactive across all PoolItems; compare with `GetData<IGalleryEntry>().GuidPreset` for highlight |
| `IGalleryEntry` | entry — used for both `GuidPreset` (highlight) and `Show()` (the "Show" button handler) |
| `GalleryPoolCallbacks` | `OnFocus` / `OnShow` — card invokes them on its own gestures |

Data array: `{ Sprite, BoolData, StringData, IGalleryEntry, GalleryPoolCallbacks }`.

### What the prefab does

- On hover (or Focus gesture) — calls `callbacks.OnFocus()`.
- On the "Show" click — reads `entry`, calls `entry.Show()` and `callbacks.OnShow()`.
- Visual treatment of `isLocked` — entirely on the prefab.

---

## Configuration

### `GalleryView` inspector

| Field | Description |
|---|---|
| `marks` | One or more mark names from `RecordMarksSettings`. A card enters the pool if at least one of these marks tags its guid. |
| `allowedTypes` | Whitelist of FullNames of `IGalleryEntry` implementations. Empty — all allowed. |
| `deniedTypes` | Blacklist of FullNames. |
| `deniedGuids` | Explicit guid exclusions. |
| `lockedMode` | `Hide` — locked cards absent from pool; `ShowAsLocked` — in pool with `isLocked=true`. |
| `sorter` | FullName of an `IGalleryEntryComparer` implementation. Empty — no sorting (Database order, not guaranteed stable). |
| `iconOverrides` | List of `(guid, Sprite)` — replace the model's icon for a specific scene. |
| `pool` | `Pool` from `Vortex.Unity.UI.PoolSystem`. |
| `stubTweener` | Optional `TweenerHub`. Forward on empty pool, Back on non-empty. |

Before use — add the required mark names to `RecordMarksSettings` (`Tools → Vortex → Record Marks → Settings`) in `slotMarks[]` (per-slot) or `globalMarks[]` (per-account).

### Consumer model (example)

```csharp
public class MyCardModel : Record, IGalleryEntry
{
    // GuidPreset / Name / Icon come from RecordPreset<MyCardModel>.CopyFrom.
    public string GuidPreset => guid;
    public Sprite Icon => icon;
    public string Name => name;

    public void Show() { /* your viewer */ }
    public void Hide() { /* your closer */ }
}

public class MyCardPreset : RecordPreset<MyCardModel> { /* content fields */ }
```

`GalleryView` in the scene picks up the model through Database — no manual registration.

---

## Edge cases

| Situation | Behaviour |
|---|---|
| Model without `RecordType == Singleton` | `Database.GetRecords` skips it — the card never appears in the gallery. |
| `Icon == null` | If no override is set, PoolItem gets `null`-Sprite — visual reaction is up to the prefab. |
| Empty `Name` | Raw comparer puts the record first; localized comparer behaves as `TryTranslate("")` does (usually empty string). |
| `Show` throws | `GalleryView` does not wrap the call — the exception propagates; the implementation must be safe. |
| Two models sharing `GuidPreset` | Database prevents it (guid is unique). |
| `RecordMarksBus.IsReady == false` at OnEnable | Subscribe to `OnReady`, defer `RefreshPool`. On OnDisable subscription is cleaned. |
| `Database.GetRecords` is empty / all records filtered out | Empty pool → `stubTweener.Forward()`. |
| `sorter` type not found in domain / does not implement `IGalleryEntryComparer` | Warning + Database order. |
| `LastViewedGuid` absent from current pool | Start position stays unset — `selectedGuid = null`. |
| `Open(guid)` — guid not in pool | Warning, no state change. |
| `pool == null` | LogError at OnEnable, no further work. |
| Fast Enter Play | Static `_lastViewedGuid` persists (per project convention); subscriptions are cleared on OnDisable. |

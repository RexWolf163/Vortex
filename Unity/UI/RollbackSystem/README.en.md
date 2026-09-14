# RollbackSystem

**Namespace:** `Vortex.Unity.UI.RollbackSystem`, `Vortex.Unity.UI.RollbackSystem.Sources`
**Assembly:** `ru.vortex.unity.ui.rollback`

## Purpose

Rolling back a screen's unsaved changes. `RollbackHandler` combines rollback sources, shows an "unsaved changes" flag and drives two paths — "Save" and "Rollback". Disabling the screen means "Rollback".

Features:
- Any rollback area is a `RollbackSource` descendant; the handler does not know what exactly it rolls back
- Reactive: sources report changes themselves, no per-frame checks
- Exact comparison: "changed and changed back" — no changes
- Exit request via `TweenerHub`, exit without changes — `UnityEvent`
- A ready source for dropdowns and sliders with a `RollbackControl` marker on the control

Out of scope:
- Collapsing the exit request and closing the window after the player's choice — layout
- Data storage: a source restores values through the same API the player changes them with
- Comparison with factory settings — each system has its own (e.g. `ChangesStateHandler` in Rebind)

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `ru.vortex.unity.ui.misc` | `UIStateSwitcher`, `TweenerHub`, `DropDownComponent` |
| `ru.vortex.unity.app` | `TimeController` — end-of-frame checkpoint and control registration |
| `ru.vortex.extensions` | `SwitcherState` |
| `ru.vortex.extenums`, `ru.vortex.unity.extenums` | `UIStateSwitcher.Set` overloads |
| `ru.vortex.unity.editortools` | `[AutoLink]` in `RollbackControl` |
| `UnityEngine.UI` | `Slider` |
| Odin Inspector | `[SerializeReference]` source list, source type row |

---

## Architecture

```
RollbackSystem/
├── RollbackSource.cs              # source abstraction
├── RollbackHandler.cs             # screen handler
└── Sources/
    ├── UIControlsRollback.cs      # dropdowns and sliders
    └── RollbackControl.cs         # control marker
```

```
RollbackHandler (screen root)
  ├── [SerializeReference] RollbackSource[]   ← UIControlsRollback, RebindRollback, custom sources
  ├── UIStateSwitcher (SwitcherState)         ← Off — no changes, On — changes
  ├── TweenerHub                              ← exit request
  └── UnityEvent onExit                       ← exit without changes
```

### Lifecycle

| Event | Handler | Sources |
|-------|---------|---------|
| `OnEnable` | `TweenerHub.Back(true)`, switcher `Off`, checkpoint at the end of the frame | Subscribe to their events |
| Data change | Recompute the overall flag, switcher | Recompute their flag on the event; notify only if the flag changed |
| `Save()` | New checkpoint in all sources, switcher `Off` | Remember the current state |
| `Rollback()` | Rollback in all, then a new checkpoint, switcher `Off` | Restore the checkpoint state |
| `CallExit()` | Changes — `TweenerHub.Forward()`, none — `onExit` | — |
| `OnDisable` | With changes — `Rollback()` | Unsubscribe from events |

The checkpoint is taken at the end of the enabling frame rather than right in `OnEnable`: by then controls have received their values. After a rollback the checkpoint is taken again — the post-rollback state counts as the baseline even if the rollback did not restore data exactly.

`Save()` and `Rollback()` do not touch the tweener: collapsing the request and closing the window is up to the layout.

---

## API

### RollbackHandler

```csharp
handler.Save();                                  // the current state becomes the new checkpoint
handler.Rollback();                              // rollback to the checkpoint, then a new checkpoint
handler.CallExit();                              // changes — exit request, none — onExit
bool changed = handler.HasChanges;               // there are unsaved changes
var source = handler.GetSource<UIControlsRollback>();   // first source of the type; null — none
```

| Inspector field | Type | Description |
|-----------------|------|-------------|
| `sources` | `RollbackSource[]` | Rollback sources (`[SerializeReference]`) |
| `switcher` | `UIStateSwitcher` (`SwitcherState`) | `Off` — no changes, `On` — changes |
| `exitRequest` | `TweenerHub` | Exit request |
| `onExit` | `UnityEvent` | Exit without changes |

### Custom source

```csharp
[Serializable]
public class VolumeRollback : RollbackSource
{
    private float _checkpoint;

    protected override void Subscribe() => Audio.OnVolumeChanged += OnChanged;
    protected override void Unsubscribe() => Audio.OnVolumeChanged -= OnChanged;

    protected override void MakeCheckpoint() => _checkpoint = Audio.Volume;
    protected override bool DiffersFromCheckpoint() => !Mathf.Approximately(Audio.Volume, _checkpoint);
    protected override void Restore() => Audio.SetVolume(_checkpoint);

    private void OnChanged(float value) => Refresh();   // recompute the flag only on an event
}
```

| Member | Called by | What it does |
|--------|-----------|--------------|
| `Subscribe()` / `Unsubscribe()` | handler (enable / disable) | Subscribe to its data events; call `Refresh()` in handlers |
| `MakeCheckpoint()` | handler (enable, "Save", after rollback) | Remember the current state |
| `DiffersFromCheckpoint()` | `Refresh()` | Compare with the checkpoint; called only on an event |
| `Restore()` | handler ("Rollback") | Restore the checkpoint state |
| `Refresh()` | implementation | Recompute the flag and notify the handler if it changed |
| `IsInitialized` | implementation | The handler is enabled and the source subscribed — for registering data on the fly |
| `HasChanges` | handler | Cached change flag |

Before a checkpoint exists `Refresh()` does not raise the flag and `Rollback()` does not call `Restore()`. In the handler's list each source shows a row with its type name — otherwise a source without serialized fields would be an empty list element.

---

## UI controls source

`UIControlsRollback` rolls back dropdowns (`DropDownComponent`) and sliders (`Slider`). The checkpoint is dropdown indices and slider values; rollback sets values back into the controls, and the controls write them to the settings themselves. Changes are detected via `DropDownComponent.OnValueSelected` (player selection or `SetValue`) and `Slider.onValueChanged`.

Controls are not listed — each registers itself with a `RollbackControl` marker on the control:

| Field | Description |
|-------|-------------|
| `handler` | `RollbackHandler`; the nearest one among parents is filled in (`OnValidate`) |
| `dropdown` | `DropDownComponent` on the same object (`[AutoLink]`) |
| `slider` | `Slider` on the same object (`[AutoLink]`) |

The marker travels with the control when copied — a parameter cannot drop out of rollback. Registration happens at the end of the enabling frame, and the control's value is remembered at that moment; the handler's checkpoint overwrites the values of all registered controls. Registration is removed only when the object is destroyed: the `OnDisable` order of parent and children is not guaranteed, and removing it on disable could precede the handler's rollback.

### Screen setup

1. On the screen root — `RollbackHandler`: indicator switcher, exit request tweener, `onExit`.
2. In the source list — `UIControlsRollback`.
3. On every dropdown and slider — `RollbackControl`.
4. Buttons: exit — `CallExit()`; in the request — `Save()` or `Rollback()` plus closing the window with their own events.

### Other sources

| Source | Package | Rolls back |
|--------|---------|------------|
| `UIControlsRollback` | RollbackSystem | Dropdowns and sliders |
| `RebindRollback` | RebindSystem (`ru.vortex.sdk.rebind.views` assembly) | Key rebinds and group activity: the checkpoint is an `Export()` snapshot, rollback — `Import()` |

### Simplified alternative

`RollbackSettings` and `RollbackSlaveObserver` in `Vortex/Sdk/Core/RollbackSettingsHandlers` — the previous mechanism for dropdowns and sliders only, with per-frame polling and three switcher states. Kept for screens where the new system is overkill.

---

## Contract

### Guarantees
- Right after a checkpoint a source sees no changes
- After "Rollback" no source sees changes
- The switcher is `On` if and only if at least one source has changes
- The flag is recomputed only on source events; no per-frame work
- While the handler is disabled, sources are not subscribed to events
- Closing a screen without changes rolls nothing back
- A rollback error in one source is logged and does not affect the others

### Limitations
- Data changes that bypass the events a source subscribes to (e.g. a control value set in code without `OnValueSelected` / `onValueChanged`) are not seen
- Two handlers over the same data have their own checkpoints
- The checkpoint is taken at the end of the enabling frame: a control that receives its value later shows changes right after opening

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| Empty source list | Warning in the editor; the handler is always `Off` |
| Empty element in the list | Error in the editor; the element is skipped |
| `CallExit()` without changes | `onExit` |
| `Save()` / `Rollback()` with the request open | Executed; the tweener is not touched |
| Disabling the screen with changes | `Rollback()`, then sources unsubscribe |
| `RollbackControl` without `UIControlsRollback` on the handler | Error in the log, the control is not registered |
| Registering the same control again | No-op |
| Dropdown was empty at the checkpoint | Rollback skips it |
| Destroyed control | Unregistered; skipped in comparison and rollback |

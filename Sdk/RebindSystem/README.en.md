# RebindSystem

Vortex framework Sdk package: key rebinding on top of the project-wide Input System asset. A command can hold several keys per device group; "one key on several commands" conflicts are checked within an action map. Player changes are stored as a diff snapshot and applied over the factory layout at startup. The core is the model, operations, press capture and events; ready-made controls-menu views come as a separate assembly.

## Purpose

- Key slots for every command (Input System action) — separately per device group
- Conflict checks: within a map — rejection with a list, across maps — allowed with highlighting
- Operations: assign, take a key, clear, swap, reset a command / map / everything
- "Press a key" capture valve with Shift/Ctrl/Alt modifiers
- Switchable layouts: mutually exclusive groups of the same device
- Diff snapshot loaded under "loaded is loaded" rules, with unreadable parts isolated and the source backed up
- Snapshot export and import — rollback point and bulk remap
- Developer rules in the config: allowed pairs, protected, skipped and forbidden keys
- Ready-made views: group command list, slots, mouse zone, indicators, reset, layout switching, a rollback source for RollbackSystem

Out of scope:

- Glyphs, key name localization, menu layout
- Firing rules for overlapping binds (Ctrl, A and Ctrl+A) — external handlers, including Input System's `shortcutKeysConsumeInput`
- Out-of-system bindings (axes, interactions, processors, other composites) — the system neither sees nor touches them
- `InputAction`s created in code (e.g. `KeyboardHandler`) — they bypass the system

## Dependencies

**Core** — `ru.vortex.sdk.rebind`:

| Dependency | Purpose |
|------------|---------|
| `ru.vortex.system` | `SystemController`, `Singleton`, `ISystemDriver` |
| `ru.vortex.extensions` | `InitValve`, Vortex serializer, string compression |
| `ru.vortex.apploader` | `Loader` — loading as a process |
| `ru.vortex.unity.filesystem` | `FileBus` — file driver path |
| `ru.vortex.unity.CoreAssetsSystem` | `ICoreAsset` — settings asset auto-creation |
| `ru.vortex.unity.extensions` | `AssetDatabaseExt`, `MenuConfigSearchController` — config menu item |
| `Unity.InputSystem` | Actions asset, binding overrides, input events |
| UniTask | `RunAsync` of the loading process |
| Sirenix Odin Inspector | Command and layout dropdowns, `[InfoBox]` in the config |

**Views** — `ru.vortex.sdk.rebind.views`:

| Dependency | Purpose |
|------------|---------|
| `ru.vortex.sdk.rebind` | Core |
| `ru.vortex.system` | `IDataStorage`, the bus base `SystemController` |
| `ru.vortex.extensions` | `SwitcherState` |
| `ru.vortex.unity.ui.misc` | `Pool`, `UIComponent`, `UIStateSwitcher` |
| `ru.vortex.extenums` | `UIStateSwitcher.Set` overloads |
| `ru.vortex.unity.ui.rollback` | `RollbackSource` — rollback source |
| `ru.vortex.unity.editortools` | `[ClassFilter]`, `[AutoLink]` |
| `Unity.InputSystem` | Map dropdown in the editor, device layout in `CaptureMouseHandler` |

Both assemblies compile only with the `USING_VORTEX_REBIND` symbol (`defineConstraints`); the symbol is enabled by the `rebindSdk` toggle in the `SdkSettings` asset.

## Architecture

### Data

```
InputSystem.actions (factory layout)     RebindSettings (rules)
                 │                               │
                 └──────────────┬────────────────┘
                                ▼
                  RebindModel: commands → groups → slots
                                ▲        │ occupancy index
                diff snapshot ──┘        ▼
                (storage driver)    AssetWriter ──► InputSystem.actions (override layer)
```

Effective bindings live only in the Input System asset. The model is derived from the asset, the config and the snapshot, and is not saved itself. The snapshot holds only differences from the factory layout and slots explicitly assigned by the player.

### Loading

`RuntimeInitializeOnLoadMethod` binds the controller to the bus and registers the process in `Loader` (`WaitingFor()` is empty). Storage drivers register themselves; `DriverConfig` lets only the selected one through. In `RunAsync`:

1. clean slate of the asset: remove overrides and erase the system's auxiliary bindings;
2. model from the asset and config: factory slots per group, empty slots up to X, default group activity;
3. snapshot from storage under the loading rules (see "Snapshot");
4. occupancy index, write to the asset;
5. `OnReady` opens, `OnRebuilt` is raised.

Input handlers subscribe on `AppStates.Running`, i.e. after loading: rebinds are applied before the first in-game press.

### Writing to the asset

Factory bindings are not changed: changes go into the override layer (`overridePath` is not serialized). A real binding is added only when there is nothing to override — a slot beyond the factory ones or a change of value shape (plain key ↔ combination). Such bindings are named `VortexRebind:<guid>`. In the editor Input System reimports the asset on exiting Play Mode, so nothing leaks into the asset file. An inactive group and a cleared key are overrides to an empty path.

## Key concepts

| Concept | Description |
|---------|-------------|
| Command (`RebindCommand`) | An Input System action, id `Map/Action`. A skipped command has no slots |
| Device group (`DeviceGroupSettings`) | Key, device layouts (with inheritance: `Gamepad` covers DualShock, DualSense, XInput), slot count X, switch set key |
| Slot (`BindSlot`) | A position in the command's key list within a group. Address — `bindKey`: `Map/Action#Group#N` |
| Value (`BindingValue`) | Trigger and 0–2 modifiers — control paths. Immutable |
| Signature | Normalized comparison key of values: case, modifier order, modifier sides per setting |
| Occupancy index | "Group + signature → slots". Candidate checks without scanning all bindings |
| Origin (`SlotOrigin`) | `Default`, `Overridden`, `Added`, `Cleared`, `Empty` |
| Conflict (`SlotConflict`) | `None`, `CrossMap` (another map — allowed), `IntraMapAllowed` (allowed pair), `IntraMap` (only from loading or import) |
| Explicit assignment (`IsExplicit`) | The player assigned the slot. Saved even when equal to the factory value; cleared by a reset |
| Valve (`CaptureValve`) | The "waiting for a press for a slot" state |

### Service rule

The system sees only bindings that can be assigned via "press a key":

- a single control of the Button family or a synthetic axis direction (`up`/`down`/`left`/`right`: mouse wheel, stick directions);
- an `OneModifier` / `TwoModifiers` / `ButtonWithOneModifier` / `ButtonWithTwoModifiers` composite with Shift/Ctrl/Alt modifiers and a keyboard or mouse trigger.

Bindings with interactions or processors, axes, other composites, usage and wildcard paths are out-of-system. So is a binding whose device belongs to no group.

### Candidate check order

The first failure is `Rejected` with a reason:

1. the slot exists: command, not skipped, group, index < X (`UnknownCommand`, `SkippedCommand`, `UnknownGroup`, `UnknownSlot`);
2. the candidate is serviceable: at most two modifiers, modifiers are Shift/Ctrl/Alt of different families (`TooManyModifiers`, `AmbiguousModifiers`, `InvalidTrigger`);
3. the device belongs to the slot's group (`DeviceNotInGroup`);
4. not forbidden (`ForbiddenKey`);
5. gluing: the same key on this command in the group moves to the target slot;
6. an intra-map conflict without an allowed pair (`IntraMapConflict` with a list);
7. protection — for operations that remove a key from a command (`ProtectedLastBinding`).

## Critical requirements

1. **Only the system changes the project asset's bindings.** Changes that bypass it (`ApplyBindingOverride`, `AddBinding` on `InputSystem.actions`) are not tracked: they do not reach the model or index and are overwritten by the system's next write.
2. **Direct assignment uses generalized paths.** The signature compares paths as strings: `<DualSenseGamepadHID>/buttonSouth` and `<Gamepad>/buttonSouth` are different keys to it. The capture valve generalizes by itself; a caller of `Assign` / `Take` passes paths in the same form.
3. **The storage driver is ready synchronously.** The loading process depends on nothing and reads the snapshot in `RunAsync`; a driver with asynchronous initialization would be read before it is ready.
4. **No more than X factory bindings per group.** A violation logs an error on every start. The first operation on the command (except a reset) drops the extra ones.
5. **A group key contains no `#` or `/`.** It is part of the slot address.
6. **Simultaneously active overlapping groups outside a set are the developer's responsibility.** Conflicts between groups are not checked.

## Contract

### Input

- The `RebindSettings` asset (created automatically, see "Usage")
- The project-wide Input System asset (`InputSystem.actions`)
- A storage driver assigned to `RebindBus` in `DriverConfig`: `RebindPlayerPrefsDriver` or `RebindFileDriver`

### Output

- The model `RebindBus.Data`: commands, slots per group, group activity
- Operation response — `RebindResult`: status, reason, conflicts, changed slots
- Valve state `RebindBus.Capture`
- Flags `RebindBus.CanPersist` (changes are saved) and `HasChanges()` (differs from factory)
- Bus events: `OnSlotsChanged`, `OnRebuilt`, `OnGroupsChanged`, `OnCaptureChanged`
- The snapshot in the driver's storage and the override layer in the asset

### Guarantees

- One changing operation — one save and one event. Exceptions — `ResetAll` and `Import`: `OnRebuilt`, then `OnGroupsChanged`
- Operations never create intra-map conflicts: `IntraMap` is possible only from loading or import
- An operation never leaves a protected command without keys
- After an operation on a command, each of its groups has at most X keys and no duplicates; reset and loading do not trim to X
- Factory values of the asset are never changed: a reset removes overrides
- Loaded data is applied as is, even if it breaks the rules; it is brought to them on the first operation on the command
- Unreadable data is replaced with factory values level by level; the source is backed up before the first overwrite
- An interrupted capture changes nothing
- While the valve is closed there are no input subscriptions

### Limitations

- **Changes that bypass the system are not tracked** (see "Critical requirements").
- **A re-run without a domain reload is not supported.** The model and asset are rebuilt from a clean slate, but `OnReady` is static and does not reopen — as with other Vortex buses.
- **A storage read error disables writing until restart.** The snapshot content is unknown and must not be overwritten; the player's changes work in memory, `CanPersist` is `false`.
- **`Import` of fully unreadable text returns `Applied`.** Factory settings remain; the result carries no degradation flag, only a log warning.
- **While a caught key is held, other presses of its device are suppressed.** Otherwise the held key would reach the game with the device's next event.
- **Of several candidates in one input update, the first is taken.**
- **Cancelling capture with a forbidden key works only without a modifier.** A held modifier is already swallowed and its device's events are suppressed: Ctrl+Esc will not reach the game.
- **Conflicts are checked only within a group.**

## API Reference

```csharp
// ── Access ────────────────────────────────────────────────────────────────
RebindBus.OnReady.Subscribe(OnRebindReady);    // InitValve: Subscribe / Unsubscribe
bool              RebindBus.IsReady;
bool              RebindBus.CanPersist;        // false — in memory only: no driver or storage unreadable
IRebindController RebindBus.Controller;
RebindModel       RebindBus.Data;              // null before loading
CaptureValve      RebindBus.Capture;

// ── Operations (bindKey = "Map/Action#Group#N") ───────────────────────────
RebindResult Assign(string bindKey, BindingValue value);  // intra-map conflict → Rejected
RebindResult Take(string bindKey, BindingValue value);    // take from conflicting commands
RebindResult Clear(string bindKey);
RebindResult Swap(string bindKeyA, string bindKeyB);
void ResetCommand(string commandId);
void ResetMap(string map);                                // null — no-op
void ResetAll();                                          // including group activity
void SetGroupActive(string groupKey, bool active);        // enabling turns off groups of its set

// ── Reading (non-empty slots only, in config group and slot order) ────────
BindSlot                GetFirst(string commandId, string groupKey);
IReadOnlyList<BindSlot> GetAll(string commandId, string groupKey = null);
string                  BindSlot.GetDisplayString();      // "Ctrl+A"; glyphs and localization — the view
bool                    HasChanges();                     // differs from factory (slots or group activity)

// ── Capture ───────────────────────────────────────────────────────────────
RejectReason SaveSignalForBind(string bindKey);           // None — opened; an open one is retargeted
void         CancelSaving();
void         AddCaptureFilter(Func<BindingValue, bool> filter);     // persistent candidate filter
void         RemoveCaptureFilter(Func<BindingValue, bool> filter);

// ── Rollback ──────────────────────────────────────────────────────────────
string       Export();
RebindResult Import(string json);

// ── Model ─────────────────────────────────────────────────────────────────
IReadOnlyList<DeviceGroupSettings> RebindModel.Groups;
RebindCommand                      RebindModel.GetCommand(string id);
IEnumerable<RebindCommand>         RebindModel.GetCommands(string map = null);
BindSlot                           RebindModel.GetSlot(string bindKey);
bool                               RebindModel.IsGroupActive(string groupKey);
IReadOnlyList<BindSlot>            RebindCommand.GetSlots(string groupKey);

// ── Events ────────────────────────────────────────────────────────────────
RebindBus.OnSlotsChanged   += keys => { };   // changed slots, including conflict state changes
RebindBus.OnRebuilt        += () => { };     // everything rebuilt: loading, map / full reset, import
RebindBus.OnGroupsChanged  += () => { };     // group activity
RebindBus.OnCaptureChanged += valve => { };  // valve: open, modifiers, rejection, close
```

## Usage

### 1. Enabling the package

Turn on the `rebindSdk` toggle in the `SdkSettings` asset — this adds the `USING_VORTEX_REBIND` symbol.

### 2. Settings asset

`RebindSettings` implements `ICoreAsset` and is created automatically in `Assets/Resources/Settings/`. If auto-creation is off — `Tools → Vortex → Debug → Check Core Assets`. Quick access to the asset — `Tools → Vortex → Configs → Rebind Settings` (the item exists only while the package is enabled).

| Field | Default | Meaning |
|-------|---------|---------|
| `groups` | `Keyboard&Mouse` (Keyboard, Mouse; X = 2), `Gamepad` (Gamepad; X = 2) | Device groups |
| `allowedPairs` | empty | Command pairs of one map allowed to share a key |
| `protectedCommands` | empty | Commands whose last key an operation will not remove |
| `skippedCommands` | empty | Commands entirely outside the system |
| `forbidden` | system keys and shortcuts | Forbidden keys: the key itself, with a specific modifier set, or with any |
| `distinguishModifierSides` | off | Distinguish left and right modifiers |
| `snapshotFolder` | `Controls` | Snapshot file folder relative to `FileBus.GetAppPath()` |

Default forbidden keys: synthetic and OS-captured keys (`anyKey`, `IMESelected`, Win, `contextMenu`, `printScreen`), `numLock`, `scrollLock`, `pause`, `f12` (Steam screenshot), `OEM1`–`OEM5`; any modifier + F1–F12, Tab, Enter, NumpadEnter, Esc; Alt+Space.

Protections, pairs and skips are set by the developer: lists are empty by default, there are no built-in safeguards.

The `Gamepad` group covers all layout descendants: DualShock, DualSense, XInput (including Steam Deck and controllers under Steam Input), Switch Pro. Adding `Joystick` captures HID gamepads and joysticks not recognized as `Gamepad`; they have no factory bindings, and button paths are tied to the device model.

### 3. Registering the driver

In the `DriverConfig` asset: **Reload** → pick a driver for the `RebindBus` system → **Save Config**.

| Driver | Storage | Backup of unreadable data |
|--------|---------|---------------------------|
| `RebindPlayerPrefsDriver` | key `VortexRebindSnapshot` | key `VortexRebindSnapshot_backup_<yyyyMMdd_HHmmss>` |
| `RebindFileDriver` | `<FileBus.GetAppPath()>/<snapshotFolder>/bindings.json`, atomic write | `bindings_<yyyyMMdd_HHmmss>.json` next to it |

Without a driver the system works in memory only, and an error is logged.

### 4. Press capture

```csharp
private void OnEnable() => RebindBus.OnCaptureChanged += OnCaptureChanged;
private void OnDisable() => RebindBus.OnCaptureChanged -= OnCaptureChanged;

public void OnSlotClicked(string bindKey)
{
    var reason = RebindBus.Controller.SaveSignalForBind(bindKey);
    if (reason != RejectReason.None)
        view.ShowError(reason);
}

private void OnCaptureChanged(CaptureValve valve)
{
    var result = valve.LastResult;
    if (valve.IsOpen)
    {
        view.ShowWaiting(valve.BindKey, valve.HeldModifiers);
        if (result != null)
            view.ShowHint(result.Reason);        // forbidden key — keep waiting
        return;
    }

    switch (result.Status)
    {
        case RebindStatus.Applied:  view.Highlight(result.Conflicts); break;   // cross-map
        case RebindStatus.Rejected: view.OfferTakeOrSwap(result.Conflicts); break;
        case RebindStatus.Cancelled: view.Close(); break;
    }
}
```

Valve rules:

- only devices of the slot's group are caught; presses of other devices work as usual;
- presses held at the moment of opening are not caught until released;
- buttons and synthetic directions are caught: mouse wheel, stick directions; pointer motion (`delta`) is not;
- a held modifier waits for the main key; a single modifier released without it becomes the key itself; three modifiers, or two released without a key, are rejected;
- **a forbidden key is not captured**: `ForbiddenKey` rejection, the press reaches the game, the valve stays open. This is how capture is cancelled with a forbidden key: a blacklisted Esc reaches `UICancel`;
- an invalid trigger is rejected and the valve stays open; an intra-map conflict and success close the valve;
- a repeated `SaveSignalForBind` retargets the valve; `CancelSaving` and window focus loss — `Cancelled`;
- a candidate rejected by a filter (`AddCaptureFilter`) is ignored and reaches the game and UI;
- the caught press does not reach game commands;
- the path is generalized to the most generic layout still in the group: DualSense in the `Gamepad` group gives `<Gamepad>/buttonSouth`.

Filters are persistent: a view registers and removes them, the valve applies all registered ones. A ready example is `CaptureMouseHandler` (see "Views").

### 5. Conflict: take or swap

```csharp
private void OnTake(string bindKey, ConflictInfo conflict)
{
    var value = RebindBus.Data.GetSlot(conflict.OtherBindKey).Value;
    RebindBus.Controller.Take(bindKey, value);
}

private void OnSwap(string bindKey, ConflictInfo conflict) =>
    RebindBus.Controller.Swap(bindKey, conflict.OtherBindKey);
```

### 6. In-game key hints

```csharp
var slot = RebindBus.Controller.GetFirst("Player/Jump", "Keyboard&Mouse");
hint.text = slot?.GetDisplayString() ?? string.Empty;
```

Subscribing to `OnSlotsChanged`, `OnRebuilt` and `OnGroupsChanged` keeps the hint up to date.

### 7. Switchable layouts

Groups with a shared `switchSet` are mutually exclusive: `SetGroupActive(key, true)` turns off the other groups of the set. Binds of an inactive group do not fire, but are editable, and conflicts in it are counted. By default the first group of each set and all standalone groups (no `switchSet`) are active; layout switching does not affect standalone groups.

## Views

Assembly `ru.vortex.sdk.rebind.views`, namespace `Vortex.Sdk.RebindSystem.Views`.

| Component | Purpose |
|-----------|---------|
| `RebindGroupHandler` | Outputs the serviced commands of its maps for one group into a `Pool`. Fields: group key, map list, pool. Item data — command and group. The pool is refilled on `OnRebuilt` |
| `RebindCommandView` | Group pool item: command title (`titlePattern`: `{0}` — id, `{1}` — map, `{2}` — action; localized) and a nested `Pool` of the group's slots with index < X |
| `RebindSlotView` | Slot pool item: key text (`GetDisplayString`), conflict (`SlotConflict`), origin (`SlotOrigin`) and waiting (`SwitcherState`) switchers. Public `SaveNewKey()` goes on a button and opens capture |
| `CaptureMouseHandler` | Mouse capture zone: while enabled, mouse keys are caught only with the pointer over it (`IPointerEnter/Exit`); outside the zone the mouse reaches the UI. The object receives UI raycasts and contains no buttons |
| `CaptureStateHandler` | `SwitcherState` switcher: `On` — the valve is waiting for a key, `Off` — not |
| `CaptureCancelHandler` | Public `Cancel()` for the "Cancel" button; the `cancelOnDisable` flag interrupts waiting when the object is disabled |
| `SwitchSetHandler` | Layout switching within a set: `Next()` / `Previous()` in config order, active group title in a `UIComponent`. An empty set key logs an error |
| `ChangesStateHandler` | `SwitcherState` switcher: `On` — differs from factory (`HasChanges()`) |
| `ResetToFactoryHandler` | Public `ResetAll()` for the "Reset all" button; an open capture is interrupted |
| `RebindRollback` | Rollback source for `RollbackHandler` (RollbackSystem package): the point is an `Export()` snapshot, rollback — `Import()`, changes — by a model fingerprint |

Menu layout: a group panel with `RebindGroupHandler` → a row pool on a prefab with `RebindCommandView` → a nested slot pool on a prefab with `RebindSlotView`. The capture panel — `CaptureStateHandler`, `CaptureMouseHandler`, `CaptureCancelHandler`.

### Rolling back changes

The rebinding screen connects to RollbackSystem: add a `RebindRollback` source to the screen's `RollbackHandler`. The rollback point is the state when the screen opened or at the last "Save", including group activity; rollback interrupts an open capture and restores the snapshot. Snapshots are not compared as text: sections are ZIP-compressed and archive entries carry a creation time — two exports of the same state give different strings.

`ChangesStateHandler` and rollback answer different questions: the former compares with factory settings, rollback — with the rollback point.

## Snapshot

Format — the Vortex serializer; nested sections are stored as compressed strings. The root is a dictionary of strings without POCO classes: renaming types does not break the snapshot.

| Level | Content | Unreadable → |
|-------|---------|--------------|
| Root | `version`, `active` (active groups), `g:<group>` per group | everything factory |
| Group section | command → command section | group factory |
| Command section | list of slot records | command in the group factory |
| Slot record | `b[e]:<binding id>=<value>` — factory, `a[e]:<position>=<value>` — added; `e` — assigned explicitly; value — `trigger\|modifier\|modifier`, empty — key cleared | slot factory |

Loading rules:

- commands, bindings and groups that no longer exist are dropped silently;
- if the config moved a factory binding to another group, its record moves with it;
- a value whose device no longer fits the group goes as an added slot into the first fitting group;
- violations of limits, forbidden keys and switch sets are applied as is;
- a snapshot newer than the format — fully factory;
- on any unreadable part the source text is backed up before the first overwrite; if the backup fails, the overwrite is postponed.

The snapshot is global: it is not tied to a save slot. Export returns the same text; import fully replaces the user state, including group activity.

## Edge cases

| Situation | Behavior |
|-----------|----------|
| No `RebindSettings` asset | Error in the log; no groups — the system services nothing |
| No driver assigned in `DriverConfig` | Error in the log, in-memory only, `CanPersist` is `false` |
| Storage could not be read | Factory settings, snapshot writing disabled until restart |
| Snapshot partially unreadable | Unreadable parts — factory, source backed up before the first overwrite |
| More than X factory bindings in a group | Error in the log at start; the first operation on the command (except a reset) drops the extra ones |
| Operation before loading | `Rejected` (`UnknownSlot`); `Import` — `Cancelled`; resets and switching — no-op |
| `ResetMap(null)` | No-op: resetting everything is `ResetAll` only |
| `Clear` of an empty slot | `Applied` with no changes |
| A protected command would lose its last key | `Rejected` (`ProtectedLastBinding`); slots beyond X do not count |
| LeftCtrl+RightCtrl+A | `Rejected` (`AmbiguousModifiers`) |
| Forbidden key during capture | `ForbiddenKey` rejection, the press reaches the game, the valve stays open |
| Intra-map conflict during capture | The valve closes, `Rejected` with a list |
| Window focus loss during capture | `Cancelled`, the slot is unchanged |
| A command from `skippedCommands` | No slots, `GetAll` is empty, operations — `SkippedCommand` |

## File structure

```
RebindSystem/
├── IRebindController.cs                  # controller contract
├── IRebindStorageDriver.cs               # storage driver contract
├── Bus/
│   └── RebindBus.cs                      # bus: access, events, driver channel
├── Controllers/
│   ├── RebindController.cs               # lifecycle, Loader process
│   ├── RebindController.Build.cs         # model from asset and config, index, conflicts, HasChanges
│   ├── RebindController.Operations.cs    # slot and group operations
│   ├── RebindController.Snapshot.cs      # snapshot, loading, export and import
│   ├── RebindController.Capture.cs       # capture valve, candidate filters
│   ├── AssetWriter.cs                    # the only writer to the asset
│   ├── ServiceRule.cs                    # service rule
│   ├── GroupResolver.cs                  # binding-to-group membership
│   ├── Signature.cs                      # value signature
│   └── ForbiddenMatcher.cs               # forbidden key matching
├── Model/
│   ├── RebindModel.cs                    # state model
│   ├── RebindCommand.cs                  # command
│   ├── BindSlot.cs                       # slot
│   ├── BindingValue.cs                   # slot value
│   ├── SlotStates.cs                     # SlotOrigin, SlotConflict
│   ├── OccupancyIndex.cs                 # occupancy index
│   ├── RebindResult.cs                   # operation response
│   └── CaptureValve.cs                   # valve state
├── Presets/
│   ├── RebindSettings.cs                 # ICoreAsset: rules
│   ├── DeviceGroupSettings.cs            # device group
│   ├── CommandPair.cs                    # allowed pair
│   └── ForbiddenEntry.cs                 # forbidden key
├── Drivers/
│   ├── RebindPlayerPrefsDriver.cs
│   └── RebindFileDriver.cs
├── Editor/
│   └── MenuController.cs                 # Tools/Vortex/Configs/Rebind Settings
├── Views/                                # assembly ru.vortex.sdk.rebind.views
│   ├── RebindGroupHandler.cs
│   ├── RebindCommandView.cs
│   ├── RebindSlotView.cs
│   ├── CaptureMouseHandler.cs
│   ├── CaptureStateHandler.cs
│   ├── CaptureCancelHandler.cs
│   ├── SwitchSetHandler.cs
│   ├── ChangesStateHandler.cs
│   ├── ResetToFactoryHandler.cs
│   ├── RebindRollback.cs
│   └── ru.vortex.sdk.rebind.views.asmdef
├── DefineSettings/
│   ├── SdkSettings.Rebind.cs             # USING_VORTEX_REBIND toggle
│   └── sdk.settings.system.ext.asmref
└── ru.vortex.sdk.rebind.asmdef
```

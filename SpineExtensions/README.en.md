# SpineExtensions

Bridge between the Vortex framework and Spine (Esoteric Software). Contains subpackages that use `Spine.Unity` directly and are therefore extracted from the core layers.

## Purpose

- Tween logic that switches skeleton states in sync with `TweenerHub` — single-clip and weighted-random flavours
- Spine animation scrub driven by a `FloatData` value (track time bound to a 0..1 parameter)
- Reaction of Spine skeletons to `GameStates` transitions (freeze on pause/loading)
- Skeleton skin swap via `UIStateSwitcher`
- Desync of identical animations (random offset on the active track)

Every Tween/Scrub class ships as a pair `SkeletonGraphic` (UGUI) / `SkeletonAnimation` (MeshRenderer) over a common generic base.

Out of scope: Spine runtime, asset import, skeleton rendering, cutscene logic (see [`NaniExtensions/CutsceneSystem`](../NaniExtensions/README.en.md)).

## Activation

The package is activated via `SdkSettingsSystem`: the `SdkSettings` asset (`Vortex → Configs → SDK Settings`), toggle **`spineExt`**. The field is annotated with `[DefineSymbol("USING_SPINE")]` and syncs the `USING_SPINE` symbol across Scripting Define Symbols of every platform when toggled.

The `ru.vortex.spine` assembly has `defineConstraints: ["USING_SPINE"]` — when the toggle is off, the module does not compile, is not built into the player, and does not reference `Spine.Unity`. The partial extension of `SdkSettings` lives in `DefineSettings/SdkSettings.Spine.cs` and is included only when the `sdk.settings.system` assembly is present (via `.asmref`).

## Assembly

A single asmdef for the entire module: **`ru.vortex.spine`** (at the `SpineExtensions/` root).

## Subfolders

| Subfolder | Purpose |
|-----------|---------|
| [TweenerSystem](TweenerSystem/) | `TweenLogic` implementations: single-clip and weighted-random animation for `SkeletonGraphic` and `SkeletonAnimation` |
| [UIs](UIs/) | Scene handlers: `FloatData`-driven scrub, Spine/`Animator` freeze on `GameStates`, desync, skin and `MeshRenderer` order switchers |
| [Addressable](Addressable/) | Addressable support for Spine: typed `AssetReferenceSkeletonDataAsset` reference, layer gate by skeleton readiness `SpineReadyGateHandler` |
| [DefineSettings](DefineSettings/) | Partial extension of `SdkSettings` with the `spineExt` toggle (`USING_SPINE`) |

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `spine-unity` | `SkeletonGraphic`, `SkeletonAnimation`, `AnimationState`, `SkeletonData`, `TrackEntry` |
| `ru.vortex.unity.ui.misc` | base `TweenLogic`, `DataStorageView<T>`, `StateItem` |
| `ru.vortex.extensions` | `IsNullOrWhitespace`, `ActionExt`, `ReactiveValue`/`FloatData` |
| `ru.vortex.unity.editortools` | `[ValueSelector]`, `[AutoLink]`, `[ClassFilter]`, `[ClassLabel]` attributes |
| `ru.vortex.unity.app` | `TimeController` (re-roll and desync scheduling) |
| `ru.vortex.sdk.game.core` | `GameController`, `GameStates` |
| `ru.vortex.system` | base abstractions (asmdef reference) |
| `sdk.settings.system` (via `.asmref`) | partial `SdkSettings` + `[DefineSymbol]` |
| `Unity.Addressables` | `AssetReferenceT<SkeletonDataAsset>` (the `Addressable/` subfolder, under `#if ENABLE_ADDRESSABLES`) |
| `ru.vortex.unity.assetcachesystem` | loading addressable skeletons via `AssetCache` (optional) |
| `UniTask` | async loading of addressable assets |
| Sirenix Odin Inspector | `[InfoBox]`, `[OnValueChanged]`, `[OnInspectorGUI]`, `[HideReferenceObjectPicker]` |

> The module references both layer-2 (Unity) and layer-3 (Sdk) assemblies. This is intentional: a single assembly under `USING_SPINE` is easier to manage; isolation from the rest of the framework is provided by the constraint, not by layer separation.

---

## TweenerSystem

**Namespace:** `Vortex.SpineExtensions.TweenerSystem`

Every class is a `TweenLogic` implementation invoked by `TweenerHub`. Each family is built as `Base<TSkeleton>` plus two ready-to-use `[Serializable]` subclasses:

| Class | Base | Skeleton type |
|-------|------|---------------|
| `SpineAnimationLogic` | `SpineAnimationLogicBase<SkeletonGraphic>` | UGUI |
| `SpineSkeletonAnimationLogic` | `SpineAnimationLogicBase<SkeletonAnimation>` | MeshRenderer |
| `SpineAnimationRandomLogic` | `SpineAnimationRandomLogicBase<SkeletonGraphic>` | UGUI |
| `SpineSkeletonAnimationRandomLogic` | `SpineAnimationRandomLogicBase<SkeletonAnimation>` | MeshRenderer |

The base `skeleton` field is constrained with `[ClassFilter(typeof(IAnimationStateComponent), typeof(IHasSkeletonDataAsset))]` — both interfaces are provided by Spine and are shared by UGUI and Mesh variants.

### SpineAnimationLogicBase&lt;T&gt; — single animation

Binary switching principle:
- `value == 0` → idle animation `animationIdle0`
- `value == 1` → idle animation `animationIdle1`
- intermediate value, forward direction → `animationFrw` then `animationIdle1`
- intermediate value, back direction → `animationBack` then `animationIdle0`

If the corresponding animation is missing (or set to `[NONE]`) the switch in that direction is skipped.

| Field | Type | Description |
|-------|------|-------------|
| `skeleton` | `TSkeleton` | Target skeleton |
| `animationChannel` | `byte` (0..10) | AnimationState track index |
| `animationIdle0` | `string` (selector) | Idle animation in Back position |
| `animationIdle1` | `string` (selector) | Idle animation in Forward position |
| `animationFrw` | `string` (selector) | Transition into Forward |
| `animationBack` | `string` (selector) | Transition into Back |
| `skipIfNotEqual` | `bool` | Run transition only when matching idle is currently playing |

`SwitchOn` / `SwitchOff` toggle `skeleton.gameObject` activity (matches `TweenPreset.offOnStartPoint/EndPoint`).

In Editor `[ValueSelector("GetListAnimations")]` populates the dropdown from `skeleton.SkeletonDataAsset` plus a `[NONE]` entry.

### SpineAnimationRandomLogicBase&lt;T&gt; — weighted random animation

Behaviour mirrors the single-clip variant, but each of the four animation fields becomes an array of `SpineAnimationVariant` (name + weight 0..100). On every switch the actual clip is drawn at random with probability proportional to weight.

| Field | Type | Description |
|-------|------|-------------|
| `skeleton` | `TSkeleton` | Target skeleton |
| `animationChannel` | `byte` (0..10) | AnimationState track index |
| `animationsIdle0` | `SpineAnimationVariant[]` | Idle variants in Back |
| `animationsIdle1` | `SpineAnimationVariant[]` | Idle variants in Forward |
| `animationsFrw` | `SpineAnimationVariant[]` | Transition-into-Forward variants |
| `animationsBack` | `SpineAnimationVariant[]` | Transition-into-Back variants |
| `skipIfNotEqual` | `bool` | Run the transition only when one of the matching idle variants is active |

**Idle re-roll.** If an idle array contains more than one variant, a `TimeController.Call` is scheduled for the duration of the current clip — when it fires, a new random variant is picked and the cycle repeats. This produces a "living" idle with rotating variations. The schedule is cancelled on any new switch (`CancelIdleReroll` → `TimeController.RemoveCall(this)`).

**Inspector.** `SpineAnimationVariant` is drawn with `[ClassLabel("$Label")]` showing the clip name and computed probability share (`weight / Σweights`). Share recomputation runs via `[OnValueChanged]`; when `skeleton` changes the variant arrays receive a fresh name list.

### Edge cases (shared by both families)

| Situation | Behaviour |
|-----------|-----------|
| Transition animation missing / array empty | Switch in that direction is skipped |
| `skipIfNotEqual = true`, different animation active | Transition is not started |
| Idle animation empty | `SetEmptyAnimation` is applied to the channel |
| Repeated call during transition | Ignored (`_isRunningState` flag) |
| Sum of variant weights ≤ 0 | No clip is picked, the switch is cancelled |

---

## UIs

**Namespace:** `Vortex.SpineExtensions.UIs`

### SpineAnimationScrubHandler / SpineSkeletonAnimationScrubHandler

A scrub handler: binds the track time of a Spine animation to a `FloatData` value (0..1). Shipped as a pair over a common base `SpineAnimationScrubHandlerBase<TSkeleton>` which inherits `DataStorageView<FloatData>` (the data source is any `IDataStorage`).

The animation is applied to the channel with `TimeScale = 0` (Spine's regular playback is frozen), and `TrackTime` is written manually in `OnDataUpdated`:

```
track.TrackTime = Mathf.Clamp01(Data.Value) * track.Animation.Duration;
```

Spine then applies the pose in `LateUpdate`.

| Field | Type | Description |
|-------|------|-------------|
| `skeleton` | `TSkeleton` | Target skeleton |
| `animationName` | `string` (selector) | Animation placed on the scrub track |
| `channel` | `byte` (0..10) | AnimationState track index |
| `source` | `IDataStorage` (from `DataStorageView`) | `FloatData` source |

`DeInit` clears the track via `SetEmptyAnimation`.

| Subclass | Skeleton type |
|----------|---------------|
| `SpineAnimationScrubHandler` | `SkeletonGraphic` (UGUI) |
| `SpineSkeletonAnimationScrubHandler` | `SkeletonAnimation` (MeshRenderer) |

### SpinePauseHandler

A `MonoBehaviour` handler that pauses the skeleton on Loading/Paused. Supports both `SkeletonGraphic` and `SkeletonAnimation` on the same object — both fields are bound via `[AutoLink]`.

The pause mechanism differs per skeleton type: `SkeletonGraphic` is driven by its built-in `freeze` flag; `SkeletonAnimation` is paused by toggling the MonoBehaviour itself (`enabled = false`). When the component is disabled, Unity does not invoke its `Update` or `LateUpdate`, and `AnimationState` physically does not advance — regardless of what external code writes into the component's fields.

> `timeScale = 0` and `updateMode = UpdateMode.Nothing` are intentionally not used. In projects with external Spine-actor pollers (typical case: Naninovel character actor — its update loop overwrites `timeScale` every frame for fast-forward / skip-mode), both settings are clobbered within 1–2 frames and the skeleton keeps animating. A Unity-disabled MonoBehaviour cannot be re-enabled by such a poller — external systems work through their own Actor layer, not by holding direct references to Spine components.

| Field | Type | Description |
|-------|------|-------------|
| `spine` | `SkeletonGraphic` | UGUI skeleton (optional) |
| `spineAnimation` | `SkeletonAnimation` | Mesh skeleton (optional) |

Reaction to `GameController.OnGameStateChanged`:

| `GameStates` | `spine.freeze` | `spineAnimation.enabled` |
|--------------|----------------|--------------------------|
| `Off`, `Play`, `Win`, `Fail` | `false` | `true` |
| `Loading`, `Paused` | `true` | `false` |

Subscribe/unsubscribe lives in `OnEnable`/`OnDisable`.

### SpineRandomizationStart

A desync handler for identical animations: on `OnEnable` it queues a call via `TimeController.Accumulate` that offsets the current track's `TrackTime` to a random value in `[0; Animation.Duration]`. Useful when several copies of the same skeleton are on stage and their phases need to be spread apart.

| Field | Type | Description |
|-------|------|-------------|
| `skeletonGraphic` | `SkeletonGraphic` | UGUI skeleton (optional), `[AutoLink]` |
| `skeletonAnimation` | `SkeletonAnimation` | Mesh skeleton (optional), `[AutoLink]` |
| `channelAnimation` | `int` (0..10) | Track index to offset |

Cancellation of the queued call lives in `OnDisable` (`TimeController.RemoveCall(this)`).

### SpineSkinSwitch

A `StateItem` for `UIStateSwitcher`: when the owning state activates, it applies the configured skin to the skeleton.

| Field | Type | Description |
|-------|------|-------------|
| `skin` | `string` (selector) | Skin name from `SkeletonData.Skins` |
| `spine` | `SkeletonGraphic` | Target skeleton |

`Set()` invokes `Skeleton.SetSkin` + `SetSlotsToSetupPose` + `UpdateMesh`. In the `UIStateSwitcher` inspector dropdown the entry is registered as **Animator Control → Switch Spine Skin**.

### AnimatorPauseHandler

A `MonoBehaviour` handler that freezes a Unity `Animator` (not Spine) based on game state. The target is wired via `[AutoLink]`.

| Field | Type | Description |
|-------|------|-------------|
| `animator` | `Animator` | Target animator, `[AutoLink]` |

Reaction to `GameController.OnGameStateChanged`:

| `GameStates` | Action |
|--------------|--------|
| `Off`, `Play`, `Win`, `Fail` | `UnPause()` — restores the saved `animator.speed` |
| `Loading`, `Paused` | `Pause()` — saves current speed, `animator.speed = 0` |

Subscription happens in `OnEnable` (with an immediate call), unsubscription and `UnPause()` in `OnDisable`. The `Pause`/`UnPause` methods are exposed as Odin buttons (`[Button, HorizontalGroup]`).

### MeshRendererOrderSwitch

A `StateItem` for `UIStateSwitcher`: when the owning state activates it sets `sortingOrder` on an array of `MeshRenderer`, and `DefaultState()` returns it to `0`.

| Field | Type | Description |
|-------|------|-------------|
| `order` | `int` (`[Min(1)]`) | Sorting order in the active state |
| `meshRenderers` | `MeshRenderer[]` | Target renderers |

In the `UIStateSwitcher` inspector dropdown the entry is registered as **Animator Control → Switch MeshRenderer Order**.

---

## Addressable

**Namespace:** `Vortex.SpineExtensions.Addressable`

Support for loading Spine skeletons through Addressables — so the heavy `SkeletonDataAsset` (atlas + meshes + animations) is not resident the whole time but pulled in on demand.

### AssetReferenceSkeletonDataAsset (`#if ENABLE_ADDRESSABLES`)

A typed concrete wrapper `AssetReferenceT<SkeletonDataAsset>` (Addressables ships none built-in, same as for `AudioClip`). `[Serializable]`, constructor from `string guid`. Used in fields where the skeleton is linked by an addressable reference instead of a direct `SkeletonDataAsset` reference — the inspector picker is filtered to `SkeletonDataAsset`, and the asset loads on demand.

### SpineReadyGateHandler

A `MonoBehaviour` gate: keeps a set of UI layers disabled until the skeleton has its `skeletonDataAsset` (typical case — the skeleton is loaded by an addressable reference asynchronously, and the layer must not show before it is ready).

| Field | Type | Description |
|-------|------|-------------|
| `skeletonAnimation` | `SkeletonAnimation` | Mesh skeleton (optional), `[AutoLink]` |
| `skeletonGraphic` | `SkeletonGraphic` | UGUI skeleton (optional), `[AutoLink]` |
| `targets` | `GameObject[]` | Layers gated by skeleton readiness |

Lifecycle:
- `Awake` — disables all `targets` (it is recommended to keep them disabled in the editor too; there is an editor button `PreparingForStart` for that).
- `OnEnable` — queues `Check` via `TimeController.Accumulate`.
- `Check` — if the assigned skeleton's `skeletonDataAsset == null` (not loaded yet), reschedules itself for the next tick; once the data is present, enables all `targets`.
- `OnDisable` — `TimeController.RemoveCall(this)` and disables `targets` again.

> The gate checks **only** the appearance of `skeletonDataAsset`; it does not track data loss after loading. It is an entry gate, not an observer.

### SpineAnimationLogicBase and asmdef

The `ru.vortex.spine` assembly gained references to `Unity.Addressables`, `ru.vortex.unity.assetcachesystem`, `UniTask` (for addressable skeleton loading). The logic of the `TweenLogic` classes themselves (`SpineAnimationLogicBase` etc.) did not change — this is only the assembly prep for the addressable subfolder. `defineConstraints: ["USING_SPINE"]` is preserved; addressable-specific code is additionally guarded with `#if ENABLE_ADDRESSABLES`.

---

## Installation

1. Import the Spine Unity Runtime (Esoteric Software).
2. Open `Vortex → Configs → SDK Settings` and enable the **`spineExt`** toggle. The `USING_SPINE` symbol is added to every platform automatically.
3. Once the toggle is on, the `ru.vortex.spine` assembly starts compiling.

Disabling the toggle removes the symbol and turns SpineExtensions off entirely without any code changes.

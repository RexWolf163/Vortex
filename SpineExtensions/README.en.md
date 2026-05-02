# SpineExtensions

Bridge between the Vortex framework and Spine (Esoteric Software). Contains subpackages that use `Spine.Unity` directly and are therefore extracted from the core layers.

## Purpose

- Tween logic that switches `SkeletonGraphic` states in sync with `TweenerHub`
- Reaction of Spine skeletons to `GameStates` transitions (freeze on pause/loading)

Out of scope: Spine runtime, asset import, skeleton rendering, cutscene logic (see [`NaniExtensions/CutsceneSystem`](../NaniExtensions/README.en.md)).

## Activation

The package is activated via `SdkSettingsSystem`: the `SdkSettings` asset (`Vortex → Configs → SDK Settings`), toggle **`spineExt`**. The field is annotated with `[DefineSymbol("USING_SPINE")]` and syncs the `USING_SPINE` symbol across Scripting Define Symbols of every platform when toggled.

The `ru.vortex.spine` assembly has `defineConstraints: ["USING_SPINE"]` — when the toggle is off, the module does not compile, is not built into the player, and does not reference `Spine.Unity`. The partial extension of `SdkSettings` lives in `DefineSettings/SdkSettings.Spine.cs` and is included only when the `sdk.settings.system` assembly is present (via `.asmref`).

## Assembly

A single asmdef for the entire module: **`ru.vortex.spine`** (at the `SpineExtensions/` root).

## Subfolders

| Subfolder | Purpose |
|-----------|---------|
| [TweenerSystem](TweenerSystem/) | `SpineAnimationLogic` — TweenLogic for `SkeletonGraphic` |
| [UIs](UIs/) | `SpinePauseHandler` — skeleton freeze driven by `GameStates` |
| [DefineSettings](DefineSettings/) | Partial extension of `SdkSettings` with the `spineExt` toggle (`USING_SPINE`) |

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `spine-unity` | `SkeletonGraphic`, `AnimationState`, `SkeletonData` |
| `ru.vortex.unity.ui.misc` | base `TweenLogic` |
| `ru.vortex.extensions` | `IsNullOrWhitespace`, `ActionExt` |
| `ru.vortex.unity.editortools` | `[ValueSelector]`, `[AutoLink]` attributes |
| `ru.vortex.sdk.game.core` | `GameController`, `GameStates` |
| `ru.vortex.system` | base abstractions (asmdef reference) |
| `sdk.settings.system` (via `.asmref`) | partial `SdkSettings` + `[DefineSymbol]` |
| Sirenix Odin Inspector | `[InfoBox]` |

> The module references both layer-2 (Unity) and layer-3 (Sdk) assemblies. This is intentional: a single assembly under `USING_SPINE` is easier to manage; isolation from the rest of the framework is provided by the constraint, not by layer separation.

---

## TweenerSystem

**Namespace:** `Vortex.SpineExtensions.TweenerSystem`

### SpineAnimationLogic

A `TweenLogic` implementation that switches skeleton states in sync with the tween progress.

Binary switching principle:
- `value == 0` → idle animation `animationIdle0`
- `value == 1` → idle animation `animationIdle1`
- intermediate value, forward direction → `animationFrw` then `animationIdle1`
- intermediate value, back direction → `animationBack` then `animationIdle0`

If the corresponding animation is missing (or set to `[NONE]`) the switch is skipped.

| Field | Type | Description |
|-------|------|-------------|
| `skeleton` | `SkeletonGraphic` | Target skeleton |
| `animationChannel` | `byte` (0..10) | AnimationState track index |
| `animationIdle0` | `string` (selector) | Idle animation in Back position |
| `animationIdle1` | `string` (selector) | Idle animation in Forward position |
| `animationFrw` | `string` (selector) | Transition into Forward |
| `animationBack` | `string` (selector) | Transition into Back |
| `skipIfNotEqual` | `bool` | Run transition only when matching idle is currently playing |

`SwitchOn` / `SwitchOff` toggle `skeleton.gameObject` activity (matches `TweenPreset.offOnStartPoint/EndPoint`).

In Editor `[ValueSelector("GetListAnimations")]` populates the dropdown from `skeleton.SkeletonData.Animations`.

### Edge cases

| Situation | Behaviour |
|-----------|-----------|
| Transition animation missing | Switch in that direction is skipped |
| `skipIfNotEqual = true`, different animation active | Transition is not started |
| Idle animation empty | `SetEmptyAnimation` is applied to the channel |
| Repeated call during transition | Ignored (`_isRunningState` flag) |

---

## UIs

**Namespace:** `Vortex.SpineExtensions.UIs`

### SpinePauseHandler

A `MonoBehaviour` handler that synchronises `SkeletonGraphic.freeze` with `GameStates`.

| Field | Description |
|-------|-------------|
| `spine` | `SkeletonGraphic`, bound via `[AutoLink]` |

Reaction to `GameController.OnGameStateChanged`:

| `GameStates` | `freeze` |
|--------------|----------|
| `Off`, `Play`, `Win`, `Fail` | `false` |
| `Loading`, `Paused` | `true` |

Subscribe/unsubscribe lives in `OnEnable`/`OnDisable`.

---

## Installation

1. Import the Spine Unity Runtime (Esoteric Software).
2. Open `Vortex → Configs → SDK Settings` and enable the **`spineExt`** toggle. The `USING_SPINE` symbol is added to every platform automatically.
3. Once the toggle is on, the `ru.vortex.spine` assembly starts compiling.

Disabling the toggle removes the symbol and turns SpineExtensions off entirely without any code changes.

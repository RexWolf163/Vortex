# SpineExtensions

Bridge between the Vortex framework and Spine (Esoteric Software). Contains subpackages that use `Spine.Unity` directly and are therefore extracted from the core layers.

## Purpose

- Tween logic that switches `SkeletonGraphic` states in sync with `TweenerHub`
- Reaction of Spine skeletons to `GameStates` transitions (freeze on pause/loading)

Out of scope: Spine runtime, asset import, skeleton rendering, cutscene logic (see [`NaniExtensions/CutsceneSystem`](../NaniExtensions/README.en.md)).

## Conditional compilation

All SpineExtensions subpackages compile only when the `USING_SPINE` symbol is defined (`defineConstraints: ["USING_SPINE"]`). Without the symbol, the assemblies are not built into the player and `Spine.Unity` is not referenced. Same approach as Steam integration: the symbol is managed manually via PlayerSettings or centrally via `DefineSymbolManager`.

## Assembly

A single asmdef for the entire module: **`ru.vortex.spine`** (at the `SpineExtensions/` root).

## Subfolders

| Subfolder | Purpose |
|-----------|---------|
| [TweenerSystem](TweenerSystem/) | `SpineAnimationLogic` — TweenLogic for `SkeletonGraphic` |
| [UIs](UIs/) | `SpinePauseHandler` — skeleton freeze driven by `GameStates` |

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `spine-unity` | `SkeletonGraphic`, `AnimationState`, `SkeletonData` |
| `ru.vortex.unity.ui.misc` | base `TweenLogic` |
| `ru.vortex.extensions` | `IsNullOrWhitespace`, `ActionExt` |
| `ru.vortex.unity.editortools` | `[ValueSelector]`, `[AutoLink]` attributes |
| `ru.vortex.sdk.game.core` | `GameController`, `GameStates` |
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
2. Add `USING_SPINE` to `Project Settings → Player → Scripting Define Symbols` (for all target platforms).
3. Once the symbol is set, the `ru.vortex.spine` assembly starts compiling.

Removing the symbol disables SpineExtensions entirely without any code changes.

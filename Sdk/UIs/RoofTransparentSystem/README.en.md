# RoofTransparentSystem

**Namespace:** `Vortex.Sdk.UIs.RoofTransparentSystem`
**Assembly:** `ru.vortex.sdk.ui.rooftransparent`

## Purpose

A mechanic for smoothly fading the alpha of overhead sprites (roofs, canopies, ceilings) when a tracked target (character, NPC) enters a trigger zone. For example, when a character walks under a roof, the roof sprite becomes semi-transparent; when leaving, it returns to full opacity.

Capabilities:
- Registering an arbitrary number of targets via reactive `Vector2Data` positions
- Multiple trigger zones per sprite (`TriggerZone` array)
- Smooth fade via `AsyncTween` with configurable duration and minimum alpha
- Throttled position polling (`FixedUpdate`, 0.1s)
- Event batching via `TimeController.Accumulate` — one invocation per frame regardless of registered target count

Out of scope:
- The target's movement model (only its position is consumed)
- Alpha control on components other than `SpriteRenderer`

## Activation

The package is enabled through `SdkSettingsSystem`:

- Toggle: `roofTransparentSdk` in the `SdkSettings` asset inspector
- Define symbol: `USING_VORTEX_ROOF_TRANSPARENCY`
- Menu: `Vortex → Configs → SDK Settings`

When the toggle is off, the define is removed from PlayerSettings and the package does not compile (the asmdef declares `defineConstraints: ["USING_VORTEX_ROOF_TRANSPARENCY"]`). Activation canon — `Vortex/Sdk/SdkSettingsSystem/README.en.md`.

## Dependencies

- `Vortex.Core.Extensions.LogicExtensions` — `AddNew`
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController.Accumulate`
- `Vortex.Unity.Extensions.ReactiveValues` — `Vector2Data`
- `Vortex.Unity.UI.TweenerSystem.UniTaskTweener` — `AsyncTween`, `EaseType`
- `Vortex.Unity.EditorTools.Attributes` — `[AutoLink]`
- Sirenix Odin Inspector — `[InfoBox]`

## Architecture

```
RoofTransparentSystem/
├── RoofTransparentBus.cs          ← static bus: target index, movement event
├── RoofTransparentHandler.cs      ← MonoBehaviour on roof sprite: trigger zones and fade
├── TransparentFocusHandler.cs     ← MonoBehaviour on target: publishes position
├── TriggerZone.cs                 ← MonoBehaviour zone (radius)
├── DefineSettings/
│   ├── SdkSettings.RoofTransparency.cs   ← partial chunk of SdkSettings
│   └── sdk.settings.system.ext.asmref
└── ru.vortex.sdk.ui.rooftransparent.asmdef
```

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `RoofTransparentBus` | `static class` | `Dictionary<Vector2Data, float>` registry; `OnUpdatePositions` event, batched through `TimeController.Accumulate` |
| `RoofTransparentHandler` | `MonoBehaviour` | Placed on the roof object. Holds a `TriggerZone[]`, a `SpriteRenderer` (via `[AutoLink]`), and parameters `fadeTime`, `minAlpha`. On each event invocation it checks intersection and runs the fade |
| `TransparentFocusHandler` | `MonoBehaviour` | Placed on the target. Registers `Vector2Data _positionContainer` in the bus, updates position in `FixedUpdate` with a 0.1s throttle |
| `TriggerZone` | `MonoBehaviour` | A spherical zone with `Radius` (0.01–2). Draws Gizmos in the editor |

## Contract

### Input
- `RoofTransparentBus.Register(Vector2Data position, float size)` — register a target
- `RoofTransparentBus.Unregister(Vector2Data position)` — unregister
- Target position is updated through its `Vector2Data` (via `Set`)

### Output
- The `SpriteRenderer.color.a` on the `RoofTransparentHandler` object smoothly transitions between `1.0` and `minAlpha`

### Guarantees
- Trigger fires when `(position − triggerCenter).magnitude < triggerCenter.Radius + size`
- Fade duration is proportional to remaining distance: an in-flight tween does not restart from zero
- Unregistering clears the `OnUpdateData` subscription
- The handler's `OnDisable` kills the active tween

### Constraints
- Alpha is controlled only on `SpriteRenderer` (color is hardcoded as `new Color(1, 1, 1, f)`)
- At least one `TriggerZone` is required — otherwise `LogWarning` and the handler does not subscribe
- `OnValidate` auto-collects child `TriggerZone` components if the array is empty

## Usage

```csharp
// On the target (character):
[RequireComponent(typeof(TransparentFocusHandler))]
public class Character : MonoBehaviour { /* ... */ }

// On the roof:
// 1. Add a SpriteRenderer
// 2. Add a RoofTransparentHandler (sprite is auto-linked via [AutoLink])
// 3. Create child GameObjects with TriggerZone (or one on the object itself)
// 4. Configure fadeTime and minAlpha in the inspector
```

## Edge cases

| Situation | Behavior |
|-----------|----------|
| `triggersCenter.Length == 0` on `OnEnable` | `LogWarning`, subscription is skipped |
| Target unregistered during fade | Tween continues to completion (while the handler is active) |
| Multiple targets inside zones simultaneously | One match is enough — `isTransparent = true`, loop breaks early |
| Handler deactivated | `OnDisable` kills the tween and unsubscribes from the bus |
| `ResetIndex()` | Full registry wipe and all subscriptions removed |

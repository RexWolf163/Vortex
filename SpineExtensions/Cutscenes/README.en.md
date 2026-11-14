# SpineExtensions — Cutscenes

**Namespace:** `Vortex.SpineExtensions.Cutscenes`
**Assembly:** `ru.vortex.spine.cutscene` (defineConstraints: `USING_SPINE`)

---

## Purpose

Everything related to the Spine implementation of cutscenes for [`CutsceneSystem`](../../Unity/CutsceneSystem/README.en.md). The cutscene package itself knows nothing about Spine; this folder holds the handlers placed in the content prefab.

- `SpineCutsceneAutoNext` — phase auto-transition at the end of an animation.
- `SpineCutsceneEventSounds` — sounds on animation events.

Both handlers accept either `SkeletonGraphic` or `SkeletonAnimation`: one of the two fields is assigned. If both are assigned, `SkeletonGraphic` is used.

Out of scope:

- Starting a phase animation — that is `SpineAnimationSwitch` in the content switcher state.
- Freezing the skeleton on pause — `SpinePauseHandler`.

The subpackage has its own assembly rather than the shared `ru.vortex.spine`: it needs references to the cutscene package, audio and the database, which the other Spine extensions do not.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `spine-unity`, `spine-csharp` | the skeleton, `AnimationState`, events |
| `Vortex.Unity.CutsceneSystem` | `CutsceneContentHandler.Next` — the auto-transition signal |
| `Vortex.Unity.UI.Misc` | the base type of the content handler |
| `Vortex.Core.AudioSystem`, `Vortex.Unity.AudioSystem` | `AudioController.PlaySound`, the `Sound` record type |
| `Vortex.Core.DatabaseSystem`, `Vortex.Unity.DatabaseSystem` | `[DbRecord]` — picking a sound record |

---

## SpineCutsceneAutoNext

When the current animation of the track completes a pass, the bus gets the "next" signal. It goes through the content handler (`CutsceneContentHandler.Next()`): the handler tells the bus which phase is shown, and a signal from a phase the chain has already left is dropped.

| Field | Purpose |
|---|---|
| `content` | the cutscene content handler the signal goes through |
| `skeletonGraphic` / `skeletonAnimation` | the cutscene skeleton |
| `track` | the track whose animation end is awaited |

- Works while enabled and fires **once per enable**.
- Placed on an object enabled by the state of a phase with auto-transition (`GameObjectsSwitch`). Phases that wait for a command must not have it.
- Only the current animation of the track counts: an animation mixing out does not produce the signal.
- On pause the skeleton is frozen, so there is no signal.

---

## SpineCutsceneEventSounds

Plays a sound database record on a skeleton event.

| Field | Purpose |
|---|---|
| `skeletonGraphic` / `skeletonAnimation` | the cutscene skeleton |
| `sounds` | the table: event name → `Sound` record |

- The event name is picked from the skeleton's event list.
- Event sounds are light and repeat across cutscenes, so they come from the shared database and are not unloaded with the content.
- An event with no table row or with an empty sound is skipped.
- An event sound already playing finishes during pause; no new ones appear while paused.

---

## Example

`Example/baseSpineCutscene.prefab` — a base for the content prefab of a Spine cutscene. A world object: the root is a `Transform`, the skeleton is a `SkeletonAnimation`.

```
baseSpineCutscene       DataStorage, CutsceneContentHandler, UIStateSwitcher
├── spine               SkeletonAnimation, SpinePauseHandler; disabled
├── [AutoNextControl]   SpineCutsceneAutoNext; disabled
└── [AudioControl]      AdvancedMusicHandler, AdvancedSoundHandler, SpineCutsceneEventSounds; disabled
```

| State | Items |
|---|---|
| `Empty` | nothing — the cutscene is not shown yet |
| `Phase 1 (auto next)` | `GameObjectsSwitch` (`spine`, `[AudioControl]`); `SpineAnimationSwitch` without `loop`; `GameObjectsSwitch` (`[AutoNextControl]`) — the phase moves on by itself when the animation ends |
| `Phase 2 (loop)` | `GameObjectsSwitch` (`spine`, `[AudioControl]`); `SpineAnimationSwitch` with `loop` — the phase waits for the "next" command |

- **The order of state items matters:** enabling `spine` first, then `SpineAnimationSwitch`. The skeleton creates its `AnimationState` when the object is enabled.
- **`[AutoNextControl]` is switched off without delay**, the other objects use delayed switch-off. So the auto-transition is re-armed in every phase, while the skeleton and audio do not restart between phases.
- **Audio slots** are [`AudioAdvancedControl`](../../Sdk/AudioAdvancedControl/README.en.md) handlers from the SDK.

Your own cutscene:

1. Create a prefab variant.
2. Assign a `SkeletonDataAsset` in `SkeletonAnimation`.
3. Pick animations in the phase states instead of the `[NONE]` placeholder; a new phase is a new state.
4. On `[AudioControl]`, fill in the event sound table and the clips of the music and sound handlers.

Limitations of the base:

- Until a skeleton is assigned, the switcher logs a state validation error in the editor.
- An audio slot with an empty clip list logs `Debug.LogError` "Список клипов пуст" on enable. An unused slot handler is removed from `[AudioControl]`: all three components sit on one object, and removing the object from the `GameObjectsSwitch` lists would switch all of them off.
- A cutscene under a canvas needs `SkeletonGraphic`: the skeleton component and the references to it in handlers and states are replaced by hand.

The base for a sprite cutscene lives in [`Sdk/CutsceneSystem`](../../Sdk/CutsceneSystem/README.en.md).

---

## Edge cases

| Situation | Behaviour |
|---|---|
| No skeleton assigned | `NullReferenceException` on enable |
| The skeleton has no data | `NullReferenceException` on enable |
| The track animation changed before the pass ended | the end of the new animation is awaited |
| An event appears in the table twice | the first row plays; if its sound is empty, nothing plays |

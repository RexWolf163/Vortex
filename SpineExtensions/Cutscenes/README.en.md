# SpineExtensions — Cutscenes

> ⚠️ **Experimental package** — a companion of [CutsceneSystem](../../Unity/CutsceneSystem/README.en.md), which has not been battle-tested in production. The set and settings of the handlers may change along with it without backwards compatibility.

**Namespace:** `Vortex.SpineExtensions.Cutscenes`
**Assembly:** `ru.vortex.spine.cutscene` (defineConstraints: `USING_SPINE`)

---

## Purpose

Everything related to the Spine implementation of cutscenes for [`CutsceneSystem`](../../Unity/CutsceneSystem/README.en.md). The cutscene package itself knows nothing about Spine; this folder holds the handlers placed in the content prefab.

- `SpineCutsceneAutoNext` — auto-transition of a cutscene graph node at the end of an animation.
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
| `Vortex.Core.AudioSystem`, `Vortex.Unity.AudioSystem` | `AudioController.PlaySound`, the `Sound` record type |
| `Vortex.Core.DatabaseSystem`, `Vortex.Unity.DatabaseSystem` | `[DbRecord]` — picking a sound record |

---

## SpineCutsceneAutoNext

When the current animation of the track completes a pass, the bus gets the "next" signal. It goes through the content handler (`CutsceneContentHandler.Next()`): the handler tells the bus which graph node is shown, and a signal from a node the graph has already left is dropped. In the graph such a node has an "on `Next`" transition.

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

`Example/baseSpine42Cutscene.prefab` and `Example/baseSpine43Cutscene.prefab` — content prefab bases of a Spine cutscene for Spine 4.2 and 4.3. Below — the 4.2 base: a world object, the root is a `Transform`, the skeleton is a `SkeletonAnimation`.

```
baseSpine42Cutscene     StateGraphView, CutsceneContentHandler, UIStateSwitcher
├── [AudioControl]      AdvancedMusicHandler, SpineCutsceneEventSounds; disabled
├── [AutoNextControl]   SpineCutsceneAutoNext; disabled
└── spine               SkeletonAnimation, SpinePauseHandler; disabled
```

| State | Items |
|---|---|
| `End` | nothing — the state of the graph's end node |
| `Phase 1 (auto next)` | `GameObjectsSwitch` (`spine`, `[AudioControl]`); `SpineAnimationSwitch` without `loop`; `GameObjectsSwitch` (`[AutoNextControl]`) — the phase moves on by itself when the animation ends |
| `Phase 2 (loop)` | `GameObjectsSwitch` (`spine`, `[AudioControl]`); `SpineAnimationSwitch` with `loop` — the phase waits for the "next" command |

- **The order of state items matters:** enabling `spine` first, then `SpineAnimationSwitch`. The skeleton creates its `AnimationState` when the object is enabled.
- **`[AutoNextControl]` is switched off without delay**, the other objects use delayed switch-off. So the auto-transition is re-armed in every phase, while the skeleton and audio do not restart between phases.
- **Audio** uses [`AudioAdvancedControl`](../../Sdk/AudioAdvancedControl/README.en.md) handlers from the SDK. `[AudioControl]` holds the music (`AdvancedMusicHandler`) and the event sound table. The base has no ambient: a phase's ambient is a child layer of `[AudioControl]` with an `AdvancedSoundHandler`, enabled by the phase state through a `GameObjectsSwitch` with delayed switch-off.
- **The cutscene graph** is not set in the base: every cutscene has its own. `StateGraphView` is in `Restart` mode without a switch delay.

Your own cutscene:

1. Create a prefab variant.
2. Assign a `SkeletonDataAsset` in `SkeletonAnimation`.
3. Pick animations in the phase states instead of the `[NONE]` placeholder; a new phase is a new state.
4. Create the cutscene graph (`Database/State Graph`): a node per state with the same name, including `End`; link the phases with "on `Next`" transitions, the last one — to `End`. Assign the graph in `StateGraphView`. The View's Sync button reconciles states with nodes.
5. On `[AudioControl]`, fill in the event sound table and the music handler clips; phase ambient goes into layers (see above).

All the steps can be done by the [asset factory](../../../Plugins/AssetFactory/README.en.md); it creates and enables the ambient layers itself.

Limitations of the base:

- Until a skeleton is assigned, the switcher logs a state validation error in the editor.
- The base music handler, with auto start on and an empty clip list, logs `Debug.LogError` "Список клипов пуст" on enable. Without music, turn its `autoStart` off (the factory does this itself) or remove it. Do not take `[AudioControl]` out of the `GameObjectsSwitch` lists: the event sound table and the ambient layers live on it too.
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

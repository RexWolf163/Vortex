# CutsceneSystem — content examples

> ⚠️ **Experimental package** — a companion of [CutsceneSystem](../../Unity/CutsceneSystem/README.en.md), which has not been battle-tested in production. The structure of the prefab example may change along with it without backwards compatibility.

**Assembly:** none — the folder holds assets only

---

## Purpose

A ready-made base for the content prefab of [`CutsceneSystem`](../../Unity/CutsceneSystem/README.en.md): a sprite-swap cutscene with a smooth transition and audio slots.

The example lives in the SDK rather than in the cutscene package: its audio uses [`AudioAdvancedControl`](../AudioAdvancedControl/README.en.md) handlers, and the cutscene package one layer below knows nothing about them.

The base for a Spine cutscene lives in [`SpineExtensions/Cutscenes`](../../SpineExtensions/Cutscenes/README.en.md).

---

## Contents

```
Example/
  baseSpriteCutscene.prefab   — base of a sprite cutscene
  CutsceneCrossFade.asset     — transition tween preset: linear curve, 0.4 s
```

---

## baseSpriteCutscene

A UI prefab: the root is a `RectTransform` stretched over its parent. The orchestrator's `target` must be under a canvas.

```
baseSpriteCutscene   StateGraphView, UIStateSwitcher, TweenerHub
├── [AudioControl]   AdvancedMusicHandler; disabled
└── Layers           disabled; enabled by phase states
    ├── current      Image — the picture of the current phase
    └── old          Image above current — the picture of the previous phase, fades out on transition
```

The base has no ambient: a phase's ambient is a child layer of `[AudioControl]` with an `AdvancedSoundHandler`, enabled by the phase state through a `GameObjectsSwitch` with delayed switch-off.

### Switcher states

| State | Items |
|---|---|
| `End` | nothing — the state of the graph's end node |
| `Phase 1` | `GameObjectsSwitch` (`Layers`, `[AudioControl]`); `SpritesSwitch` → `current` |
| `Phase 2`, `Phase 3` | the same; `SpritesSwitch` → `old` (the previous phase's sprite); `EventFire`: `TweenerHub.Forward(true)`, then `TweenerHub.Back(false)` |

### The old → current transition

- At rest `old` is transparent and disabled.
- On entering a phase `current` gets the phase sprite and `old` gets the previous one.
- `Forward(true)` makes `old` opaque instantly: the previous picture stays on screen.
- `Back(false)` fades `old` out and disables it at the end (the preset has `offOnStartPoint` set). `current` shows from underneath.
- The first phase appears without a transition: there is nothing to fade.

The tween is restarted through `EventFire` rather than `TweenerHubSwitch`: a repeated `Forward` on a hub that has already played starts nothing. A restart is a jump to the end followed by the reverse run.

The phases' `GameObjectsSwitch` uses delayed switch-off: objects stay enabled between phases and the audio does not restart.

### Delay between switches

The root `StateGraphView` has `switchDelay` set (0.5 s): it sets the next state no earlier than this delay after the previous one. Without the delay a "next" in the middle of a transition would cut the tween short and make the frame jump in brightness.

The View knows nothing about the tweens. The transition must fit into the delay: the example uses a 0.4 s tween with a 0.5 s delay. Changing one means changing the other.

The base has no content handler (`CutsceneContentHandler`): a picture has no auto-transition, every phase waits for the "next" command.

### Your own cutscene

1. Create a prefab variant.
2. In each phase state set the sprite for `current`; for `old` — the sprite of the phase it is entered from.
3. A new phase is a new state: the switcher duplicates the last one, only the sprites need changing.
4. Create the cutscene graph (`Database/State Graph`): a node per state with the same name, including `End`; link the phases with "on `Next`" transitions, the last one — to `End`. Assign the graph in `StateGraphView`. The View's Sync button reconciles states with nodes.
5. On `[AudioControl]`, add clips to the music handler. Phase ambient goes into a layer (see above); the volume channel is picked in its `AdvancedSoundHandler`.
6. The transition duration is changed in the preset. For a custom one, copy the preset and assign the copy in the hub's `ColorLogic`. The View's `switchDelay` must be no less than the transition duration.

The graph drives the show. The `old` slot holds one picture, so a phase entered from different phases gets a matching previous picture for only one of them. Steps 1–5 can be done by the [asset factory](../../../Plugins/AssetFactory/README.en.md); it creates and enables the ambient layers itself.

---

## Edge cases

| Situation | Behaviour |
|---|---|
| The music handler has an empty clip list | auto start is on in the base — `Debug.LogError` "Список клипов пуст" on enable. Without music, turn the handler's `autoStart` off (the factory does this itself) or remove it. Do not take `[AudioControl]` out of the `GameObjectsSwitch` lists: the ambient layers live in it |
| No previous-phase sprite set for `old` | a white rectangle fades out on transition |
| "Next" arrives while a transition is running | the next phase is shown once `switchDelay` expires; if several arrived, the latest is shown at once |
| The transition tween is longer than `switchDelay` | the next switch cuts the transition short: a brightness jump in the frame |
| The cutscene completes while a phase waits out the delay | the content is unloaded, the phase is not shown |
| The picture and screen aspect ratios differ | the picture fits entirely (`Preserve Aspect`), leaving margins at the edges |

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
baseSpriteCutscene   DataStorage, CutsceneSpriteContentHandler, UIStateSwitcher, TweenerHub
├── Layers           disabled; enabled by phase states
│   ├── current      Image — the picture of the current phase
│   └── old          Image above current — the picture of the previous phase, fades out on transition
└── [AudioControl]   AdvancedMusicHandler, AdvancedSoundHandler; disabled
```

### Switcher states

| State | Items |
|---|---|
| `Empty` | nothing — the cutscene is not shown yet |
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

The root carries `CutsceneSpriteContentHandler` rather than the base content handler: it sets the next state no earlier than `switchDelay` (0.5 s) after the previous one. Without the delay a "next" in the middle of a transition would cut the tween short and make the frame jump in brightness.

The handler knows nothing about the tweens. The transition must fit into the delay: the example uses a 0.4 s tween with a 0.5 s delay. Changing one means changing the other.

### Your own cutscene

1. Create a prefab variant.
2. In each phase state set the sprite for `current`; from the second phase on, set the previous phase's sprite for `old`.
3. A new phase is a new state: the switcher duplicates the last one, only the sprites need changing.
4. On `[AudioControl]`, add clips to the music and sound handlers; pick a volume channel for the sound handler.
5. The transition duration is changed in the preset. For a custom one, copy the preset and assign the copy in the hub's `ColorLogic`. The handler's `switchDelay` must be no less than the transition duration.

The number of states is "number of phases + 1"; their order matches the order of the chain steps.

---

## Edge cases

| Situation | Behaviour |
|---|---|
| An audio slot has an empty clip list | `Debug.LogError` "Список клипов пуст" on enable. An unused slot handler is removed from `[AudioControl]`: both sit on one object, and removing the object from the `GameObjectsSwitch` lists would switch both off |
| No previous-phase sprite set for `old` | a white rectangle fades out on transition |
| "Next" arrives while a transition is running | the next phase is shown once `switchDelay` expires; several phases go one by one |
| The transition tween is longer than `switchDelay` | the next switch cuts the transition short: a brightness jump in the frame |
| The cutscene completes while a phase waits out the delay | the content is unloaded, the phase is not shown |
| The picture and screen aspect ratios differ | the picture fits entirely (`Preserve Aspect`), leaving margins at the edges |

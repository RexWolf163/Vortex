# NaniExtensions

Bridge between the Vortex framework and the Naninovel visual novel engine. Contains subpackages across different layers.

## Purpose

- Volume settings translation from Vortex to Naninovel
- Nani scene management from Vortex (pause, stop, actor reset)
- Multi-channel localization (UI, dialogue, voice)
- Spine-animated cutscene management via Nani backgrounds

Out of scope: Naninovel scripts, scene content, Spine asset configuration.

## Subpackages

| Subpackage | Assembly | Layer | Purpose |
|------------|----------|-------|---------|
| [Core](Core/) | `ru.vortex.nani.core` | 3 | `NaniWrapper` — lazy access to Naninovel services |
| [AudioSystem](AudioSystem/) | `ru.vortex.nani.audiosystem` | 3 | Volume translation, Nani audio control |
| [CutsceneSystem](CutsceneSystem/) | `ru.vortex.nani.cutscenes` | 3 | Spine cutscene controller |
| [LocalizationSystem](LocalizationSystem/) | `ru.vortex.nani.localization` | 2 | Localization driver with channels |

---

## Core

**Namespace:** `Vortex.NaniExtensions.Core`

`NaniWrapper` — static class with lazy-cached access to Naninovel services via `Engine.GetService<T>()`.

### Available Services

`AudioManager`, `StateManager`, `L10N`, `CommunityL10N`, `ScriptPlayer`, `BackgroundManager`, `CharacterManager`, `TextPrinterManager`, `ChoiceHandlerManager`, `UnlockableManager`, `UIManager`, `VariablesManager`

### Lifecycle

- `[RuntimeInitializeOnLoadMethod]` — subscribes to `GameController.OnNewGame`, `OnLoadGame`, `OnGameStateChanged`
- `OnNewGame` / `OnLoadGame` → `ScriptPlayer.Stop()` + `ResetNani()`
- `GameStates.Off/Win/Fail` → `ScriptPlayer.Stop()` + `ResetNani()`

### API

| Method | Description |
|--------|-------------|
| `ResetNani()` | Stop all audio, reset variables, hide backgrounds, characters, text printers, reset choices |
| `NaniIsPlaying()` | `true` if ScriptPlayer is playing or choice handler is visible |

---

## AudioSystem

**Namespace:** `Vortex.NaniExtensions.AudioSystem`, `Audio`

### NaniVortexAudioConnector

Volume settings translation from Vortex channels → Naninovel.

- Channels (`bgm`, `sfx`, `voice`, `voiceCutscene`) loaded from `AudioChannelsConfig` via partial extension `AudioChannelsConfigExtNani`
- When `GameStates.Off` — settings changes are immediately projected to Nani
- During active game — only via explicit calls `GetNaniBgmVolume()`, `GetNaniSfxVolume()`, `GetNaniVoiceVolume()`
- `SetCutsceneMode(bool)` — switches voice volume source between `voiceChannel` and `voiceCutsceneChannel`

### AudioNaniController

Nani audio control from Vortex.

| Method | Description |
|--------|-------------|
| `StopNaniMusic()` | Pause current BGM, save path to `PausedMusicPath` |
| `PlayNaniMusic()` | Resume BGM from `PausedMusicPath` |
| `StopNaniVoice()` | Stop voice |
| `StopNaniSfx()` | Stop all SFX |

### AudioChannelsConfigExtNani

Partial extension of `AudioChannelsConfig` (assembly `ru.vortex.unity.audiosystem.ext`). Adds 4 fields with `[AudioChannelName]` attribute for mapping Nani channels to Vortex channels.

---

## CutsceneSystem

**Namespace:** `Vortex.NaniExtensions.CutsceneSystem`, `Vortex.NaniExtensions.CutsceneSystem.Models`

Controller for Spine-animated cutscenes displayed as Naninovel `SpineBackground`.

### CutsceneController

Static controller. Lifecycle:

```
Open(key) → load CutsceneData → SpineBackground → LoadPhase → [NextPhase]* → Close
```

| Method | Description |
|--------|-------------|
| `Open(key, canBeClosedByButton)` | Load cutscene by Addressable key, start first phase |
| `NextPhase()` | Advance to next phase |
| `Close()` | Stop, cleanup, remove background |

Phases:
- Each `CutscenePhase` — animation (looped/non-looped) + optional ambient sound
- Non-looped animations automatically advance to next phase
- Spine events (`Event`) are mapped to sounds via `EventToAudioData`

`GameStates` reaction:
- `Play` → `timeScale = 1`, resume sounds
- `Paused` → `timeScale = 0`, stop all sounds
- `Off` → `Close()`

### Models

| Class | Description |
|-------|-------------|
| `CutsceneData` | ScriptableObject: `SkeletonDataAsset`, `List<CutscenePhase>`, `List<EventToAudioData>`, `List<string> SexSceneAmbients` |
| `CutscenePhase` | Phase: `AnimationKey`, `AnimationLooped`, `AmbientAudioPack` |
| `CutscenePhaseData` | Dialogue data: `AuthorTextKey`, `DialogueTextKey`, voice-over (Ru/En) |
| `EventToAudioData` | Spine event → sound mapping: `EventName`, `AudioPack` |

`CutsceneData.SyncWithSpine()` — Editor button: synchronizes phases and events with `SkeletonDataAsset`.


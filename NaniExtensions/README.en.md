# NaniExtensions

Bridge between the Vortex framework and the Naninovel visual novel engine. Contains subpackages across different layers.

## Purpose

- Volume settings translation from Vortex to Naninovel
- Nani scene management from Vortex (pause, stop, actor reset)
- Multi-channel localization (default, dialogue, voice)
- Spine-animated cutscene management via Nani backgrounds

Out of scope: Naninovel scripts, scene content, Spine asset configuration.

## Subpackages

| Subpackage | Assembly | Layer | Purpose |
|------------|----------|-------|---------|
| [Core](Core/) | `ru.vortex.nani.core` | 3 | `NaniWrapper` — lazy access to Naninovel services |
| [AudioSystem](AudioSystem/) | `ru.vortex.nani.audiosystem` | 3 | Volume translation, Nani audio control, `NaniVoicePlayBus` |
| [CutsceneSystem](CutsceneSystem/) | `ru.vortex.nani.cutscenes` | 3 | Spine cutscene controller |
| [LocalizationSystem](LocalizationSystem/) | `ru.vortex.nani.localization` | 2 | Localization driver with channels |
| [QuestSystem](QuestSystem/) | `ru.vortex.nani.quests` | 3 | `NaniPlayerState` quest condition — reacts to Nani player start/stop (`defineConstraints`: `USING_VORTEX_QUESTS`, `USING_NANINOVELL`) |
| [SaveSystem](SaveSystem/) | `ru.vortex.nani.saves` | 3 | `NaniDataSaveController` — saves/restores Nani variables via `GameController` |
| [Misc](Misc/) | `ru.vortex.nani.misc` | 3 | Scene-glue handlers (active speaker, dialog bubble, emotions, Z-position, gallery test tools) |

The whole Nani package family is activated via the `USING_NANINOVELL` symbol, set by the `naninovellExt` toggle (`SdkSettings`, `[DefineSymbol("USING_NANINOVELL")]` attribute).

---

## Core

**Namespace:** `Vortex.NaniExtensions.Core`

`NaniWrapper` — static class with lazy-cached access to Naninovel services via `Engine.GetService<T>()`.

### Available Services

`AudioManager`, `StateManager`, `L10N`, `CommunityL10N`, `ScriptPlayer`, `BackgroundManager`, `CharacterManager`, `TextPrinterManager`, `ChoiceHandlerManager`, `UnlockableManager`, `UIManager`, `VariablesManager`

### Lifecycle

- `[RuntimeInitializeOnLoadMethod]` — subscribes to `GameController.OnNewGame`, `OnLoadGame`, `OnGameStateChanged`
- `OnNewGame` → `ScriptPlayer.Stop()` + `ResetNani()` + `VariablesManager.ResetAllVariables()`
- `OnLoadGame` → `ScriptPlayer.Stop()` + `ResetNani()`
- `GameStates.Off/Win/Fail` → `ScriptPlayer.Stop()` + `ResetNani()`

### API

| Method | Description |
|--------|-------------|
| `ResetNani()` | Stop all audio, reset variables, hide backgrounds, characters, text printers, reset choices |
| `NaniIsPlaying()` | `true` if ScriptPlayer is playing or choice handler is visible |

### NaniSessionService

Adapter that wraps `Naninovel.Engine` into the `IGameSessionService` contract from `Sdk/Core`. Registers automatically via `[RuntimeInitializeOnLoadMethod]`. Once registered, `GameController` awaits `Engine.Initialized` before every transition to `Play` (after `NewGame` and `OnLoad`). Previously this wait was hard-wired into `Sdk/Core` — now Sdk/Core has no knowledge of Naninovel, and the dependency lives exclusively here.

```csharp
public sealed class NaniSessionService : IGameSessionService
{
    public bool IsReady => Engine.Initialized;
    public string Name => "Naninovel.Engine";
}
```

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

### NaniVoicePlayBus

Event bus for line start / line end. Joins two sources — `ITextPrinterManager` and `IAudioManager` — into a single contract:

```csharp
public static event Action<string> OnVoiceStart;   // authorId of the speaker
public static event Action<string> OnVoiceStop;    // authorId of the speaker
```

Voice playback is detected by polling `IAudioManager.GetPlayedVoice()`. The bus correctly catches:
- end of voice (path → null);
- voice switch to a different actor (pathA → pathB) even without a null gap in between;
- "silent transition" (the same author continues with a new voice track — the animator stays in Forward, neither Stop nor Start is emitted);
- a line without a voice — Stop is emitted on `PrintFinished` so the subscriber does not get stuck in the "speaking" state when no next `PrintStarted` follows (end of dialog, pause for a choice).

Algorithm:
1. `OnPrintStarted` — close the previous line (if it had no voice), emit `OnVoiceStart`, start/continue the polling loop, run an immediate `PollVoice()` reconciliation (voice might already be playing by the time the event fires: `@print` awaits `PlayVoice` before firing).
2. The polling loop via `TimeController.AddCallback` catches any path transitions and emits `OnVoiceStop` / remembers the new author as `cached`.
3. `OnPrintFinished` — emits `OnVoiceStop` if the finished line had no voice of its own. If voice is playing, closure goes through the polling loop (voice may legitimately continue past the end of text printing).
4. `OnNaniStop` / `App.OnExit` — `FlushAll`, close every open line.

Lifecycle: subscriptions in `App.OnStart`, unsubscription in `App.OnExit`. No project-side registration or initialisation required.

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

---

## Misc

**Namespace:** `Vortex.NaniExtensions.Misc`
**Assembly:** `ru.vortex.nani.misc`

A grab-bag of small handlers that glue a Naninovel scene to project UI and logic. Each one is a self-contained MonoBehaviour attached to scene UI objects and works on top of `NaniWrapper` / `NaniVoicePlayBus`.

| Handler | What it does |
|---|---|
| `ActiveCharacterHandler` | Tracks "who is speaking" via `NaniVoicePlayBus.OnVoiceStart/Stop` and switches the active-actor visual (StateSwitcher / highlight). |
| `CharacterVoiceTweenerHandler` | Wires `NaniVoicePlayBus` to a `TweenerHub` on a character: Forward on voice start, Back on voice end. |
| `LookCharacterHandler` | Rotates a character toward the current speaker (source — `NaniVoicePlayBus`). |
| `VisibilityCharacterHandler` | Hides/shows characters by scene rules (based on `CharacterManager.GetActor` / `Appearance`). |
| `DialogBubbleSwitcher` | State switcher for the dialog bubble (line type, emotion, background). |
| `BubblePositionTarget` | Bubble position anchor relative to a world object (actor / slot). |
| `TextBubbleResizer` | Dynamic bubble size based on the text length of the line (after `OnPrintStarted`). |
| `ZPositionSwitch` | Z-position switching by domain state (active / inactive speaker). |
| `ResetAllGalleryCardsHandler` | Editor tool: resets the unlocked-card progress in `UnlockableManager` (for testing gallery scenes from a clean state). |


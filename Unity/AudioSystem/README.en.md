# AudioSystem (Unity)

Unity driver implementation for the audio system.

## Purpose

Platform adaptation of `AudioController`: sound and music playback via `AudioSource`, sound pooling, fade transitions, situational music, channel system, settings persistence, scene-oriented components.

- Sound playback through a pool with automatic release
- Music playback with fade in/out via `AsyncTween`
- Situational music with automatic restoration of the main theme
- Named channels with independent volume and mute
- Settings save/load through `PlayerPrefs`
- Inspector components: sound handlers, music handlers, UI switches, channel sliders

Out of scope: spatial audio (3D), simultaneous mixing of multiple music tracks, dynamic audio resource loading.

## Dependencies

- `Vortex.Core.AudioSystem` — `AudioController` bus, models, `IDriver`
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController` (deferred calls, pool cleanup)
- `Vortex.Unity.UI.PoolSystem` — `Pool` (sound source pool)
- `Vortex.Unity.UI.TweenerSystem.UniTaskTweener` — `AsyncTween` (fade animations)
- `Vortex.Unity.DatabaseSystem` — `RecordPreset`, `[DbRecord]` attribute
- `Sirenix.OdinInspector` — editor attributes

---

## AudioDriver

`IDriver` implementation. Partial class across three files.

### Architecture

```
AudioDriver (Singleton<AudioDriver>, IDriver)
├── AudioDriver.cs              — Init/Destroy, index population from Database, Save/LoadSettings
├── AudioDriverExtLoading.cs    — [RuntimeInitializeOnLoadMethod] auto-registration
├── AudioDriverExtEditor.cs     — [InitializeOnLoadMethod] editor registration
└── AudioDriverExtPlayControl.cs — IDriver method delegation to AudioPlayer
```

### Contract

**Input:**
- Automatic registration via `[RuntimeInitializeOnLoadMethod]`
- Index population on `Database.OnInit`
- Channel loading from `AudioChannelsConfig` (Resources)

**Output:**
- Populated `IndexSound` / `IndexMusic` registries in `AudioController`
- Populated `Settings.Channels` from `AudioChannelsConfig`
- `OnInit` event after index population
- Settings in `PlayerPrefs` (key `AudioSettings`)

**Save format:**

```
MasterOn;MasterVol;MusicOn;MusicVol;SoundOn;SoundVol[;ChName:MuteFlag:Vol]...
```

Example: `Y;0.8;Y;1;Y;1;dialog:Y:0.7;ambient:Y:0.5`

`MuteFlag` values: `Y` — not muted, `N` — muted. Numbers use `CultureInfo.InvariantCulture`.

**Guarantees:**
- Settings are saved on every change via subscription to `AudioController.OnSettingsChanged`
- Settings loading from `PlayerPrefs` with `try/catch` — on corrupt data, defaults are restored
- Channels from `PlayerPrefs` not matching the current `AudioChannelsConfig` are ignored
- `TimeController.RemoveCall(this)` on `Destroy()` — deferred call cleanup

**Limitations:**
- If `AudioController.SetDriver` returns `false` — instance is destroyed (`Dispose()`)
- Depends on `Database.OnInit` — indices are empty until database initialization

---

## Channels

### AudioChannelsConfig

`ScriptableObject` (`ICoreAsset`), placed in Resources. Defines the list of named channels for the project.

- Field `channels: string[]` — channel names
- On editor change — automatic `AudioDriver.ResetChannels()`

Menu: `Vortex/Configs/Audio Channels Settings` — navigate to config.

### AudioChannelNameAttribute

`[AudioChannelName]` attribute for `string` fields. Renders a dropdown with channel list from `AudioController.GetChannelsList()`.

### AudioChannelVolumeSlider

UI volume slider for a channel.

- `channel` field with `[AudioChannelName]` attribute — channel selection
- `OnEnable` — reads current channel volume, subscribes to `onValueChanged`
- `OnDisable` — unsubscribes
- On missing channel — slider is set to 0

The slider is a change source, not a visualizer. External channel changes are not reflected — this is intentional to prevent recursive updates.

### Channel in Presets and Handlers

Channel is assigned at two points:

- **Preset** (`SoundSamplePreset`, `MusicSamplePreset`) — default channel for the sample. Used during pool playback.
- **Handler** (`AudioHandler`) — channel of the specific component instance. Used for playback through a personal `AudioSource`.

During playback through `AudioPlayer` (pool), the channel is determined by the preset. The `defaultChannel` parameter in `IDriver` is a fallback when the sound model has no channel assigned.

---

## AudioPlayer

Central playback controller. `MonoBehaviourSingleton`, internal API.

### Architecture

```
AudioPlayer (MonoBehaviourSingleton<AudioPlayer>)
├── pool              — Pool (sound AudioSource pool)
├── musicPlayer       — MusicPlayer (main music)
├── musicCoverPlayer  — MusicPlayer (situational music)
├── musicFadeTime     — float (0–3s, default 1s)
├── FadeTween         — AsyncTween (main music fade)
└── FadeCoverTween    — AsyncTween (situational music fade)
```

### Sound Playback

`PlaySound(object, bool loop, string channelOverrideName)` — pattern matching on type:

| Type | Behavior |
|------|----------|
| `string` | Lookup `Sound` in `Database` by GUID. Channel: override or from preset |
| `Sound` | Direct access to `Sample` |
| `AudioClip` | Wrapped in `SoundClipFixed` |

Creates `SoundClipFixed`, adds to pool. For non-loop sounds — automatic removal via `TimeController.Call` after clip duration.

`StopAllSounds(string channel)` — when `null`, clears entire pool; when specified, removes only sounds with matching `Channel.Name`.

### Music Playback

Only one main and one situational track can play simultaneously.

**Main music (`PlayMusic`):**
1. If a track is playing and `fadingEnd = true` — fade out → callback → start new track
2. If `fadingEnd = false` — instant stop → start
3. New track starts with fade in (when `fadingStart = true`) or instantly

**Situational music (`PlayCoverMusic`):**
1. Fade out current situational or main music
2. Start situational track via `musicCoverPlayer`
3. On `StopCoverMusic` — situational track fades out, main theme restores with fade in

`GetMusicClip` — pattern matching: `string`, `Music`, `SoundClip`, `AudioClip`. All branches create `SoundClipFixed` with `overrideChannel` pass-through.

---

## MusicPlayer

Music playback component. Single `AudioSource`.

### Contract

- `Play(SoundClip)` / `Play(AudioClip)` — start with pitch/volume settings from clip, stores clip channel
- `Stop()` — stop playback
- `IsPlay()` — state check
- `SetVolumeMultiplier(float)` / `GetVolumeMultiplier()` — volume multiplier (for fade)
- On `OnEnable` — subscribe to `AudioController.OnSettingsChanged`, apply settings
- On `OnDisable` — unsubscribe, stop
- Mute/unmute toggles automatically on settings change. On unmute (`mute → !mute`), `audioSource.Play()` is called to resume playback
- Final volume: `GetMusicVolume(channel) × clip.volume × volumeMultiplier`

---

## Models

### SoundClip

Audio clip with pitch and volume ranges. Implements `ICloneable`. Each playback uses random values from ranges.

```
SoundClip (ICloneable)
├── AudioClips    — AudioClip[] (clip array for randomization)
├── PitchRange    — Vector2
├── ValueRange    — Vector2
├── Channel       — AudioChannel (sound channel)
├── Loop          — bool
├── GetPitch()    → Random.Range(PitchRange.x, PitchRange.y)
├── GetVolume()   → Random.Range(ValueRange.x, ValueRange.y)
├── GetClip()     → random from array (or the only one)
└── Clone()       → deep clone (new SoundClip with same parameters)
```

Constructors accept `string channelName` or `AudioChannel channel`. With `channelName` — resolved via `AudioController.GetChannel()`.

### SoundClipFixed

Inherits `SoundClip`. Pitch, volume, and clip are fixed at creation.

```
SoundClipFixed (: SoundClip)
├── AudioClip     — selected clip
├── GetPitch()    → fixed value
├── GetVolume()   → fixed value
├── GetDuration() → clip.length / |pitch| (or float.MaxValue when pitch == 0)
└── GetClip()     → fixed clip
```

Constructors support `channelOverrideName` / `channelOverride` — channel override.

### Sound / Music

Typed wrappers for Unity:
- `Sound : SoundSample<SoundClip>` — sound effect
- `Music : MusicSample<SoundClip>` — music track

---

## Components (Handlers)

### AudioHandler

Sound playback component. Works with a personal `AudioSource` or relays to `AudioPlayer`.

- Sample GUID via `[DbRecord(typeof(Sound))]`
- Channel via `[AudioChannelName]` — used for volume calculation with personal `AudioSource`
- On `Play()`: if `audioSource != null` — `PlayOneShot`; otherwise — `AudioController.PlaySound`
- `SetVolumeMultiplier(float)` / `GetVolumeMultiplier()` — volume multiplier
- Final volume: `GetSoundVolume(channel) × clip.volume × volumeMultiplier`
- Final mute: `!GetSoundOn(channel)`
- `OnEnable` — subscribes to `AudioController.OnSettingsChanged`, applies settings
- `OnDisable` — unsubscribes, stops playback
- Initialization deferred until `AudioController.OnInit` via `TimeController.Accumulate`

### MusicHandler

Music playback component triggered on GameObject activation.

- Sample GUID via `[DbRecord(typeof(Music))]`
- `OnEnable` → start music with `UniTask.DelayFrame(2)` delay to guarantee order after `OnDisable`
- `OnDisable` → deferred stop via `TimeController.Call` (bypass "hot restart")
- `isCoverMusic` field — switch between main and situational music
- `fadeStart` / `fadeEnd` fields — fade transition control
- Initialization deferred until `AudioController.OnInit` via `TimeController.Accumulate`

### AudioSourceHandler

Sound playback from `IDataStorage`.

- `[RequireComponent(typeof(AudioSource))]`
- `dataStorageObject` field (`GameObject`) — `IDataStorage` source (external object)
- Gets `SoundClip` via `IDataStorage.GetData<SoundClip>()`
- Channel taken from `SoundClip.Channel`
- Final volume: `GetSoundVolume(channel) × clip.volume`
- `OnEnable` → Play, `OnDisable` → Stop

### AudioSwitcher

UI toggle for on/off.

- Works through `UIComponent` (`SetAction`, `SetSwitcher`)
- Control type: `SoundType` (Master / Sound / Music)

### AudioValueSlider

UI volume slider.

- Bound to `UnityEngine.UI.Slider`
- Control type: `SoundType` (Master / Sound / Music)
- `OnEnable` — value synchronization, subscribe to `onValueChanged`
- `OnDisable` — unsubscribe

---

## Presets

### SoundSamplePreset

`ScriptableObject` (`RecordPreset<Sound>`), menu: `Database/SoundSample`.

- `AudioClip[]` — clip array
- `pitchRange` / `valueRange` — ranges via `[MinMaxSlider]`
- `channel` — channel via `[AudioChannelName]`
- `RecordTypes.Singleton` (forced in `OnValidate`)
- Editor: `TestSound` button — creates temporary `AudioSource`, self-destructs after playback

### MusicSamplePreset

`ScriptableObject` (`RecordPreset<Music>`), menu: `Database/MusicSample`.

- Single `AudioClip`, fixed pitch and volume
- `channel` — channel via `[AudioChannelName]`
- `Duration` — auto-calculated: `clip.length / |pitch|`
- `RecordTypes.Singleton` (forced in `OnValidate`)
- Editor: `TestSound` / `StopSound` buttons

---

## Usage

### 1. Channel Setup

1. Create `AudioChannelsConfig` in Resources
2. Define channel names: `dialog`, `ui`, `ambient`, `sfx`, etc.
3. Channels become available in `[AudioChannelName]` dropdown and `AudioController` API

### 2. Creating Presets

1. `Create → Database → SoundSample` — configure clips, pitch/volume ranges, channel
2. `Create → Database → MusicSample` — configure clip, pitch, volume, channel
3. Register presets in the database (unique GUID)

### 3. Playback from Code

```csharp
// Sound by GUID
AudioController.PlaySound("explosion_01");

// Sound by instance
var sound = AudioController.GetSample("explosion_01") as Sound;
AudioController.PlaySound(sound);

// Music
AudioController.PlayMusic("main_theme");

// Situational music
AudioController.PlayCoverMusic("battle_theme");
AudioController.StopCoverMusic(); // main theme restores
```

### 4. Sound Component

Add `AudioHandler` to a GameObject, assign sample GUID via `[DbRecord]`, select channel via `[AudioChannelName]`. Optionally add an `AudioSource` — if absent, the sound goes through `AudioPlayer`'s pool.

### 5. Music Component

Add `MusicHandler` to a GameObject, assign GUID via `[DbRecord(typeof(Music))]`. Music starts on `OnEnable`, stops on `OnDisable`. For situational music — enable `isCoverMusic`.

### 6. Settings UI

- `AudioSwitcher` — on/off toggle (Master / Sound / Music)
- `AudioValueSlider` — volume slider (Master / Sound / Music)
- `AudioChannelVolumeSlider` — channel volume slider

## Editor Tools

- `SoundSamplePreset.TestSound()` — play a random clip with random pitch/volume
- `MusicSamplePreset.TestSound()` / `StopSound()` — preview a music track
- `AudioHandler`, `MusicPlayer` — Play/Stop buttons in the inspector (Play Mode)
- `[DbRecord]` — sample picker with type filtering
- `[AudioChannelName]` — channel dropdown
- `Vortex/Configs/Audio Channels Settings` — quick navigation to channel config

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `PlaySound` with non-existent GUID | Log `[AudioPlayer] Unknown sound ID`, sound not played |
| `AudioHandler` with empty GUID | Log `[AudioHandler] Empty Sample data.` on initialization |
| `AudioHandler` without `AudioSource` | Sound relayed through `AudioController.PlaySound` (pool) |
| `PlayMusic` while music is playing | Current track fades out → new track fades in |
| `PlayCoverMusic` while main is playing | Main fades out, situational starts |
| `StopCoverMusic` | Situational fades out, main restores with fade in |
| `AudioPlayer.Instance == null` | `PlaySound` — silent return |
| No settings in `PlayerPrefs` | Default values (everything on, volume 1) |
| Corrupt data in `PlayerPrefs` | `try/catch`, defaults restored |
| `MusicHandler` quick disable/enable | `TimeController.RemoveCall` cancels pending stop, `UniTask.DelayFrame(2)` ensures play-after-stop order |
| pitch == 0 in `SoundClipFixed` | `GetDuration()` → `float.MaxValue` |
| `StopAllSounds(channel)` with `null` | Clears entire pool |
| `StopAllSounds(channel)` with name | Removes only sounds with matching `Channel.Name` from pool |
| Channel removed from `AudioChannelsConfig` | Old channel data in `PlayerPrefs` ignored on load |
| `AudioChannelVolumeSlider` with missing channel | Slider set to 0, subscription not created |

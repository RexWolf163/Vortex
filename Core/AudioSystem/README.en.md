# AudioSystem (Core)

Platform-independent audio system bus.

## Purpose

Abstract audio controller: sample indices, three-level volume model (Master → type → channel), event model, driver interface.

- Storage and GUID-based access to sound and music indices
- Settings management (volume, on/off) separately for Master, sounds, and music
- Named channel system — independent volume multipliers and mute flags
- Playback delegation to a platform driver
- Separation into main music and situational (cover) music
- Settings change event

Out of scope: playback, fade transitions, source pooling, settings persistence — driver's responsibility.

## Dependencies

- `Vortex.Core.System.Abstractions` — `SystemController`, `Singleton`, `ISystemDriver`, `IReactiveData`
- `Vortex.Core.DatabaseSystem` — `Record` (base class for samples)
- `Vortex.Core.LoggerSystem` — error logging

## Architecture

```
AudioController (SystemController<AudioController, IDriver>)
├── IndexSound       — Dictionary<string, IAudioSample>
├── IndexMusic       — Dictionary<string, IAudioSample>
├── Settings         — AudioSettings
├── OnSettingsChanged — event Action
└── → Driver         — IDriver (platform implementation)

AudioSettings
├── MasterVolume — float (0–1, default 1)
├── MasterOn     — bool (default true)
├── SoundVolume  — float (0–1, default 1)
├── SoundOn      — bool (default true)
├── MusicVolume  — float (0–1, default 1)
├── MusicOn      — bool (default true)
└── Channels     — Dictionary<string, AudioChannel>

AudioChannel (IReactiveData)
├── Name         — string (immutable)
├── Volume       — float (0–1, default 1)
├── Mute         — bool (default false)
├── OnUpdateData — event Action
└── ToSave() / FromSave() — serialization "Name:MuteFlag:Volume"

Model hierarchy:
Record → AudioSample<T> (abstract, IAudioSample)
           ├── SoundSample<T>
           └── MusicSample<T> (abstract)
```

### Three-Level Volume Model

```
Final volume = MasterVolume × TypeVolume × ChannelVolume
Final mute   = !MasterOn || !TypeOn || Channel.Mute
```

- `GetSoundVolume(channelName)` → `MasterVolume × SoundVolume × ChannelVolume`
- `GetMusicVolume(channelName)` → `MasterVolume × MusicVolume × ChannelVolume`
- `GetSoundOn(channelName)` → `MasterOn && SoundOn && !Channel.Mute`
- `GetMusicOn(channelName)` → `MasterOn && MusicOn && !Channel.Mute`

If channel is not specified or not found — channel multiplier = 1, channel mute = false.

### SoundType

```csharp
enum SoundType { Master, Sound, Music }
```

Used by UI components to select the control type.

### IDriver

Platform driver contract:

| Method | Returns | Description |
|--------|---------|-------------|
| `SetLinks(indexSound, indexMusic, settings)` | `void` | Receive references to indices and settings |
| `PlaySound(object, bool loop, string defaultChannel)` | `void` | Play a sound (fire-and-forget) |
| `PlaySoundWithControl(object, bool loop, string defaultChannel)` | `AudioSampleWrapper` | Same, but with a control handle |
| `StopAllSounds(string channel)` | `void` | Stop all sounds (or by channel) |
| `PlayMusic(object, fadingStart, fadingEnd, string defaultChannel)` | `void` | Play main music |
| `PlayMusicWithControl(object, fadingStart, fadingEnd, string defaultChannel)` | `AudioSampleWrapper` | Same, with a handle |
| `StopMusic()` | `void` | Stop main music |
| `PlayCoverMusic(object, fadingStart, fadingEnd, string defaultChannel)` | `void` | Play situational music |
| `PlayCoverMusicWithControl(object, fadingStart, fadingEnd, string defaultChannel)` | `AudioSampleWrapper` | Same, with a handle |
| `StopCoverMusic()` | `void` | Stop situational music |

The `object` parameter represents a platform-specific audio data type. Typing is resolved by the driver via pattern matching. The `defaultChannel` parameter is a fallback channel used when the sound model has no channel assigned. On `*WithControl` and `AudioSampleWrapper` — see the [Playback control via wrappers](#playback-control-via-wrappers-audiosamplewrapper) section.

## Contract

### Input
- Driver registration via `AudioController.SetDriver(IDriver)` — invokes `OnDriverConnect`, passes references to indices
- Populating `IndexSound` / `IndexMusic` — driver's responsibility
- Populating `Settings.Channels` — driver's responsibility

### Output
- Settings: `AudioController.Settings` (read properties)
- Samples: `AudioController.GetSample(guid)` → `IAudioSample` or `null`
- Playback: `PlaySound`, `PlayMusic`, `PlayCoverMusic`, `StopAllSounds`, `StopMusic`, `StopCoverMusic`
- Volume: `GetSoundVolume(channel)`, `GetMusicVolume(channel)`, `GetSoundOn(channel)`, `GetMusicOn(channel)`
- Channels: `GetChannelsList()`, `GetChannels()`, `GetChannel(name)`, `GetChVolume(id)`, `SetChVolume(id, value)`
- Event: `AudioController.OnSettingsChanged`

### Guarantees
- `GetSample` searches both indices (sounds, then music)
- On missing GUID — returns `null` + `Error` log
- Every call to `Set*State` / `Set*Volume` / `SetChVolume` triggers `OnSettingsChanged`
- All playback calls are delegated to the driver without transformation
- `SetChVolume` invokes `AudioChannel.OnUpdateData` for reactive subscription

### Limitations
- `AudioSettings` has `internal set` — modification only through `AudioController` methods
- `AudioChannel.Volume` / `Mute` have `internal set` — modification through `SetChVolume` or `FromSave`
- `AudioSample<T>.GetDataForSave()` returns `null` — samples do not participate in the save system
- `MusicSample<T>` is abstract — direct instantiation is not possible

## Usage

### Settings

```csharp
// Read
bool soundOn = AudioController.Settings.SoundOn;
float musicVol = AudioController.Settings.MusicVolume;
bool masterOn = AudioController.Settings.MasterOn;

// Modify
AudioController.SetMasterState(false);     // mute everything
AudioController.SetSoundState(false);      // mute sounds
AudioController.SetMusicVolume(0.5f);      // music volume 50%
AudioController.SetMasterVolume(0.8f);     // master volume 80%

// Subscribe
AudioController.OnSettingsChanged += () => UpdateUI();
```

### Channels

```csharp
// Channel list
var channels = AudioController.GetChannelsList();

// Channel volume
float vol = AudioController.GetChVolume("dialog");
AudioController.SetChVolume("dialog", 0.7f);

// Calculated volume including Master and type
float finalVol = AudioController.GetSoundVolume("ui");  // Master × Sound × Channel

// Extension on AudioChannel
var channel = AudioController.GetChannel("ambient");
channel.SetVolume(0.5f);
```

### Playback

```csharp
// Sound (fire-and-forget)
AudioController.PlaySound(sample);
AudioController.PlaySound(sample, loop: true);
AudioController.StopAllSounds();

// Sound with a control handle (see the wrappers section)
var voice = AudioController.PlaySoundWithControl(sample);
voice.OnFinished += OnDone;

// Main music
AudioController.PlayMusic(music, fadingStart: true, fadingEnd: true);
AudioController.StopMusic();

// Situational music
AudioController.PlayCoverMusic(battleTheme);
AudioController.StopCoverMusic(); // main theme restores (driver)
```

### Sample Retrieval

```csharp
IAudioSample sample = AudioController.GetSample("explosion_01");
```

## Playback control via wrappers (`AudioSampleWrapper`)

A handle to a specific playback: control (Play/Pause/Stop) and observation (Play/Paused/Finished events,
state, duration) over a **single** started sound/music. The abstraction and facade live in Core; the
concrete wrappers (`SoundWrapper` via the pool, `MusicWrapper` via the player) live in the Unity layer.

A plain `PlaySound` is fire-and-forget: you start it and lose the reference, the only lever back is
`StopAllSounds(channel)` (coarse, per channel). The wrapper closes the gap: the `*WithControl` versions
return a control token for that specific instance. A separate entry point (rather than "always return a
handle") is intentional: 95% of calls are one-shot, and the plain `PlaySound` stays `void` with no extra
allocations.

### API: `Play` vs `PlayWithControl`

| Fire-and-forget (`void`) | With a handle (`AudioSampleWrapper`) |
|---|---|
| `PlaySound(sound, loop)` | `PlaySoundWithControl(sound, loop)` |
| `PlayMusic(clip, fadingStart, fadingEnd)` | `PlayMusicWithControl(clip, fadingStart, fadingEnd)` |
| `PlayCoverMusic(clip, fadingStart, fadingEnd)` | `PlayCoverMusicWithControl(clip, fadingStart, fadingEnd)` |

`*WithControl` returns an `AudioSampleWrapper`, or `null` if playback did not start (unknown id, driver
not registered, etc.).

### The handle

```
AudioSampleWrapper : IDisposable
├── event OnPlay        — actually started playing (including resume after pause)
├── event OnPaused      — went to pause
├── event OnFinished    — finished (stop / natural end / preemption)
├── IsLoop   : bool     — whether it loops
├── IsPaused : bool     — paused (only the controller sets it)
├── Duration : float    — duration (accounting for pitch)
├── State    : PlaybackState  — state (only the controller sets it)
└── (Play/Resume/Stop/Pause — protected internal; outward — via the controller)
```

The holder cannot catch the `OnPlay` of the initial start (for SFX it is synchronous inside
`PlaySoundWithControl`, before the handle is returned) — read the initial state via `State`; the practical
value of `OnPlay` is **resume after pause**.

### Control facade (`AudioSampleWrapperController`)

The single public control point (extension methods). Only it changes `State`/`IsPaused` — the raw
`Play/Stop/Pause` of the model are hidden.

```csharp
wrapper.Play();    // start or resume (by state: from Paused — UnPause, otherwise Play)
wrapper.Pause();   // pause (only from Playing)
wrapper.Stop();    // external stop: kills the source + finishes
wrapper.Finish();  // finish the handle WITHOUT stopping the source (preemption; audio is faded by tweens/player)
```

| Call | From state | Action |
|------|------------|--------|
| `Play` | `Pending` | `→ Playing`, start (`Play`), `OnPlay` |
| `Play` | `Paused` | `→ Playing`, **`UnPause`** (resume), `OnPlay` |
| `Play` | `Finished` | ignored (terminal) |
| `Pause` | `Playing` | `→ Paused`, `OnPaused` |
| `Pause` | other | ignored |
| `Stop` / `Finish` | not `Finished` | `→ Finished`, `OnFinished`, `Dispose` |
| `Stop` / `Finish` | `Finished` | ignored (no repeated `OnFinished`) |

`Paused` is reachable only from `Playing` → resume is always via `UnPause`. `Finished` is terminal; further
calls are no-ops (idempotent; dual ownership holder+pool is safe).

`PlaybackState`: `Pending` (0, not yet playing) / `Playing` / `Paused` / `Finished`.

### Concrete wrappers (Unity)

- **`SoundWrapper` (SFX via the pool):** lives in the pool element's data next to the clip; `AudioSourceHandler`
  finds it and connects `Init(source, stopCallback)`. A non-loop one finishes itself by `Duration`
  (timer → finish via the controller); `Pause`/`Stop`/`Dispose` cancel the timer. `Stop` fires `stopCallback`
  → the element is auto-evicted from the pool.
- **`MusicWrapper` (music/cover):** a single owner — `MusicPlayer` keeps one current handle; on a new track
  the previous one is finished via `Finish` (without stopping the source — the fade-out is not cut off).
  `*WithControl` returns this handle in all branches, including the deferred start after a fade-out (then it
  lives in `Pending`).

### Ownership and lifetime

A fire-and-forget sound is owned by the pool (auto-release). `*WithControl` is co-ownership: **whoever
controls, finishes**. In particular: `Pause()` cancels the auto-stop timer, and `Resume` does **not**
restore it — once you pause and resume, call `Stop()` yourself when the sound is no longer needed.

### Migration from the old system

The old system was fire-and-forget: `Play*` returned nothing, and a targeted stop was only possible via
`StopAllSounds(channel)`.

- **Existing code needs no changes.** The signatures of the plain `Play*` are unchanged (`void`, same
  parameters) — all old calls compile and work as before.
- **To get control** — replace the call with `*WithControl` and work with the handle (check for `null`):
  ```csharp
  var line = AudioController.PlaySoundWithControl("hero_line");
  line.OnFinished += OnLineDone;
  line.Stop(); // kills this specific sound, not the whole channel
  ```
- **Custom drivers (`IDriver`)** must implement the `*WithControl` methods in addition to the `void`
  versions (the stock `AudioDriver` already does).

## Edge Cases

- **Driver not registered:** bus methods call `Driver.X(...)` directly, with no null-guard in `SystemController` — accessing an unregistered driver throws a `NullReferenceException`.
- **Duplicate GUID:** during index population by the driver — depends on implementation (Unity driver uses `AddNew`, last one overwrites).
- **OnSettingsChanged without subscribers:** safe invocation via `?.Invoke()`.
- **Channel not found:** `GetChVolume` returns `baseValue` (default 1f), `GetChannel` returns `null`, calculated methods use multiplier 1.
- **AudioChannel.FromSave with corrupt data:** fail-fast — exception. Error handling is the caller's responsibility.

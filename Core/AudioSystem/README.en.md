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

| Method | Description |
|--------|-------------|
| `SetLinks(indexSound, indexMusic, settings)` | Receive references to indices and settings |
| `PlaySound(object, bool loop, string defaultChannel)` | Play a sound |
| `StopAllSounds(string channel)` | Stop all sounds (or by channel) |
| `PlayMusic(object, fadingStart, fadingEnd, string defaultChannel)` | Play main music |
| `StopMusic()` | Stop main music |
| `PlayCoverMusic(object, fadingStart, fadingEnd, string defaultChannel)` | Play situational music |
| `StopCoverMusic()` | Stop situational music |

The `object` parameter represents a platform-specific audio data type. Typing is resolved by the driver via pattern matching. The `defaultChannel` parameter is a fallback channel used when the sound model has no channel assigned.

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
// Sound
AudioController.PlaySound(sample);
AudioController.PlaySound(sample, loop: true);
AudioController.StopAllSounds();

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

## Edge Cases

- **Driver not registered:** playback calls access `Driver` — behavior defined by `SystemController` (null-guard at the base class level).
- **Duplicate GUID:** during index population by the driver — depends on implementation (Unity driver uses `AddNew`, last one overwrites).
- **OnSettingsChanged without subscribers:** safe invocation via `?.Invoke()`.
- **Channel not found:** `GetChVolume` returns `baseValue` (default 1f), `GetChannel` returns `null`, calculated methods use multiplier 1.
- **AudioChannel.FromSave with corrupt data:** fail-fast — exception. Error handling is the caller's responsibility.

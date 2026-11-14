# AudioAdvancedControl

**Namespace:** `Vortex.Sdk.AudioAdvancedControl.*`
**Assembly:** `ru.vortex.sdk.audio.advancedcontrol`

---

## Purpose

Controllable, unloadable game audio: music and sound loops bound to an object's lifetime. A clip is loaded before it starts and leaves memory after it stops.

- **Two handlers:** `AdvancedMusicHandler` (the cover-music slot) and `AdvancedSoundHandler` (a regular sound with a volume channel).
- **A clip list behind `AssetHandle<AudioClip>`** — only the playing track's clip is in memory.
- **Three modes:** the selected track looped, random with anti-repeat, once.
- **Control from `UIStateSwitcher`:** public methods for `EventFire`.
- **Two pauses:** game pause and manual pause.

Out of scope:

- **Short shared sounds** (UI clicks, effects) — that is `AudioSystem` and sound database records.
- **Per-clip volume and pitch** — not configurable.
- **Deciding what plays and when** — up to whoever enables the object or calls the methods.

When to use: the audio belongs to loadable content (a cutscene, a scene, a mini-game) and must not stay in memory after it. Sound database records are never unloaded.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Core.AssetHandleSystem` | `AssetHandle<AudioClip>` |
| `Vortex.Core.AudioSystem`, `Vortex.Unity.AudioSystem` | `AudioController`, playback control handles, `SoundClipFixed`, the music fade time |
| `Vortex.Sdk.Core.GameCore` | `GameController`, `GameStates.Paused` — game pause |
| `UniTask` | clip loading, deferred release |

---

## Usage

Attach a handler to a content object and fill in the clip list.

| Field | Purpose |
|---|---|
| `clips` | the list of clip handles |
| `mode` | playback mode |
| `autoStart` | start when the object is enabled; on by default |
| `channel` | volume channel — `AdvancedSoundHandler` only |

**Cutscene music:** `AdvancedMusicHandler` on the content root with auto-start.

**Phase ambient:** `AdvancedSoundHandler` in `Index` mode on an object enabled by the phase state (`GameObjectsSwitch`). When the state goes away the object is disabled, the sound stops and the clip is released.

**Control from a switcher state:** `EventFire` calling a handler method.

```csharp
handler.Play(2);       // track #2
handler.PlayNext();    // the next one per mode
handler.Stop();
handler.Pause();
handler.Unpause();
```

---

## Architecture

```
Handlers/
  AdvancedAudioHandler (abstract)  — clip list, mode, track selection, loading and release, pauses
  ├── AdvancedMusicHandler         — output: the cover-music slot
  └── AdvancedSoundHandler         — output: a regular sound, a volume channel

Model/
  AdvancedPlaybackMode             — Index / Random / OneShot
```

The package consists of handlers: it has no shared state, no bus and no controller.

### Modes

| Mode | Behaviour |
|---|---|
| `Index` | the selected track plays in a loop |
| `Random` | when a track ends a random one starts; recently played ones are not picked |
| `OneShot` | the track plays once, then the audio stops and the clip is released |

### Track selection

The current track number is -1 when the object is enabled. The next number comes from a single internal method:

- `Index` and `OneShot` — increment by one, wrapping to the first after the last;
- `Random` — a random number outside the history. The history holds half of the list (rounded down) — the same mechanic as database sounds.

Auto-start and `PlayNext` go through it: from -1 the first result is track 0, in `Random` — a random one. `Play(number)` sets the number directly.

### Lifecycle

```
OnEnable  → number = -1 → [autoStart] PlayNext
PlayNext / Play(n) → load the clip → start
Stop      → stop → release the clip
OnDisable → Stop
```

Cancelling a load on stop or on a track change is a normal situation.

### Pause

There are two independent pauses:

- **game pause** — the `GameStates.Paused` state;
- **manual** — `Pause()` / `Unpause()`.

Audio plays only when neither is active. `Unpause()` during a game pause does not resume it.

### Music specifics

- **The cover-music slot.** The main theme is dimmed for the duration and returns after the stop. Volume follows the general music settings.
- **The handler tracks the end of a track itself.** The slot does not report it, so in `Random` and `OneShot` the handler counts the clip duration (pauses excluded) and acts one fade time before the end — the switch and the stop land on the audio system's regular fade.
- **Track switching without silence.** While the next clip loads, the previous one keeps playing.
- **The clip is released after the fade**, not at the moment of the command.
- **Displacement.** If other music takes the slot, the handler goes silent and releases the clip.

### Sound specifics

- Plays as a regular audio system sound; volume follows the channel from the `channel` field.
- The end of a track is reported by the audio system. A stop coming from its side (for example, a global sound clear) also starts the next track in `Random` mode.

---

## Contract

### Guarantees

- Only the playing track's clip is in memory.
- On `OnDisable` the audio is stopped, the load is cancelled and the clip is released (for music — after the fade).
- Restarting the same track does not reload the clip if it has not been released yet.

### Limitations

- **One cover-music slot per application.** Two music handlers, or a handler and another system, override each other.
- **Music looping is set by the slot's source**, not by the handler: in `Index` the track loops thanks to the cover player's setting.
- **Slow loading in `Random` for music.** If the next clip loads longer than the remaining tail, the previous track briefly starts over.
- **Stopping music before it actually starts.** The audio system starts cover music with a delay while it fades the main theme. A stop inside that window cancels the start, but the main theme may stay dimmed until the next music change.

---

## Edge cases

| Situation | Behaviour |
|---|---|
| Empty clip list | `Debug.LogError` on start, no audio |
| `Play(n)` with a number outside the list | `Debug.LogError`, state unchanged |
| A list element is not set | `Debug.LogError`, audio stops |
| Clip load failure, empty clip | `Debug.LogError`, audio stops |
| Object disabled during loading | the load is cancelled |
| Start during a pause | the track is paused right away and plays once the pause is lifted |
| One clip in the list, `Random` mode | that clip plays |
| `Stop` with no playing track | nothing |

---

## Public API

```csharp
namespace Vortex.Sdk.AudioAdvancedControl.Handlers
{
    public abstract class AdvancedAudioHandler : MonoBehaviour
    {
        public void Play(int number);
        public void PlayNext();
        public void Stop();
        public void Pause();
        public void Unpause();
    }

    public class AdvancedMusicHandler : AdvancedAudioHandler { }
    public class AdvancedSoundHandler : AdvancedAudioHandler { }
}

namespace Vortex.Sdk.AudioAdvancedControl.Model
{
    public enum AdvancedPlaybackMode { Index, Random, OneShot }
}
```

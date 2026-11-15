# AudioAdvancedControl

**Namespace:** `Vortex.Sdk.AudioAdvancedControl.*`
**Assembly:** `ru.vortex.sdk.audio.advancedcontrol`

---

## Purpose

Controllable, unloadable game audio: music and sound loops bound to an object's lifetime. A clip is loaded before it starts and leaves memory after it stops.

- **Two handlers:** `AdvancedMusicHandler` (the cover-music slot) and `AdvancedSoundHandler` (a regular sound with a volume channel).
- **A clip list behind `AssetHandle<AudioClip>`** — only the playing track's clip is in memory.
- **Three modes:** tracks in list order, random with anti-repeat, once.
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
| `Vortex.Core.AudioSystem`, `Vortex.Unity.AudioSystem` | `AudioController`, playback control handles and their end-of-track events, `SoundClipFixed` |
| `Vortex.Sdk.Core.GameCore` | `GameController`, `GameStates.Paused` — game pause |
| `UniTask` | clip loading |

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

**Phase ambient:** `AdvancedSoundHandler` in `Index` mode with a single clip in the list (it plays in a loop) on an object enabled by the phase state (`GameObjectsSwitch`). When the state goes away the object is disabled, the sound stops and the clip is released.

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
  AdvancedAudioHandler (abstract)  — clip list, mode, track selection, loading and release, end of track, pauses
  ├── AdvancedMusicHandler         — output: the cover-music slot
  └── AdvancedSoundHandler         — output: a regular sound, a volume channel

Model/
  AdvancedPlaybackMode             — Index / Random / OneShot
```

The package consists of handlers: it has no shared state, no bus and no controller.

### Modes

| Mode | Behaviour |
|---|---|
| `Index` | tracks play in list order: when a track ends the next one starts, after the last one — the first |
| `Random` | when a track ends a random one starts; recently played ones are not picked |
| `OneShot` | the track plays once, then the audio stops and the clip is released |

Only `OneShot` stops after playing. In `Index` and `Random` the audio keeps going until it is stopped.

### Track selection

The current track number is -1 when the object is enabled. The next number comes from a single internal method:

- `Index` and `OneShot` — increment by one, wrapping to the first after the last;
- `Random` — a random number outside the history. The history holds half of the list (rounded down) — the same mechanic as database sounds.

Auto-start, `PlayNext` and the switch to the next track at the end of one go through it: from -1 the first result is track 0, in `Random` — a random one. `Play(number)` sets the number directly.

### Lifecycle

```
OnEnable  → number = -1 → [autoStart] PlayNext
PlayNext / Play(n) → load the clip → stop the previous track → start
track ended → [Index / Random] next track  |  [OneShot] stop → release the clip
Stop      → stop → release the clip
OnDisable → Stop
```

The start order is the same for music and sound:

1. **Track request.** Accepted only on an enabled handler. The clip is requested asynchronously from the asset handle. Every request has its own cancellation token: a new request cancels a previous one that has not been executed yet, and that one never reaches the start. A repeated request for the same clip while it is loading joins the load in progress — the track starts once.
2. **Clip loading.** While it runs, the previous track keeps playing. If the load fails — an error in the log and the previous track is stopped.
3. **Stopping the previous track**, if there was one. Its clip is released, unless the new track plays that same clip.
4. **Playback.**

A stop or a new request during loading is a normal situation: the request is cancelled, nothing starts. The load itself is not interrupted: the clip finishes loading and stays in the asset cache buffer, and the cache itself removes the hold of the cancelled request. A failure of a cancelled load is logged but does not touch the playing track. The end of the previous track while the requested one is loading neither cancels nor replaces the request.

A released clip is not unloaded at once: it stays in the asset cache buffer and is unloaded by its rules (see `AssetCacheSystem`). A repeated request for such a clip is instant.

### End of track

The handler does not count track time itself. The end is reported by the audio system through the events of the control handle (`AudioSampleWrapper`); pauses are already accounted for in them:

| Event | When it comes | What the handler does |
|---|---|---|
| `OnLoopRestart` | a looped clip played to the end and started a new lap | `Index`, `Random` — requests the next track; `OneShot` — stops the audio |
| `OnFinished` | a non-looped clip played to the end, or the audio was removed from outside | stops the audio and releases the clip |

- **Looping.** In `Index` and `Random` the clip is started looped, in `OneShot` it is not. Music in the slot always loops, so in `OneShot` it is stopped by the restart.
- **The next track turned out to be the same one** (a single clip in the list) — the handler does nothing: the clip has already started a new lap by itself.
- **While the requested clip is loading**, neither event touches the request.
- **The restart comes after the wrap**, up to ~0.1 s late (see `AudioSystem`). By that moment the clip has already started a new lap.

### Pause

There are two independent pauses:

- **game pause** — the `GameStates.Paused` state;
- **manual** — `Pause()` / `Unpause()`.

Audio plays only when neither is active. `Unpause()` during a game pause does not resume it.

The manual pause lives while the object is enabled: it is reset when the object is enabled, and `Pause()` on a disabled handler has no effect. The exceptions to "no audio during a pause" for music are listed under "Limitations".

### Music specifics

- **The cover-music slot.** The main theme is dimmed for the duration and returns after the stop. Volume follows the general music settings.
- **A track in the slot always loops.** The end of a track comes only as a restart, already after the wrap. A track change and the stop in `OneShot` go with a fade, so the beginning of the previous track is heard during it.
- **The slot itself replaces the previous track**, with a fade: the handler gives no separate stop command before starting the new one.
- **The clip is released at the moment of the command.** The fade tail plays out of the asset cache buffer.
- **Displacement.** If other music takes the slot, the handler goes silent and releases the clip.

### Sound specifics

- Plays as a regular audio system sound; volume follows the channel from the `channel` field.
- The previous track is stopped at the moment the new one starts, without a fade.
- In `Index` and `Random` the clip is looped: between the restart and the start of the next track (the event delay and the clip load) the beginning of the previous one is heard.
- In `OneShot` the clip is not looped and finishes by itself, without starting over.
- A stop coming from the audio system's side (for example, a global sound clear) stops the handler in any mode — the next track is not started.

---

## Contract

### Guarantees

- The handler holds the playing track's clip; during a track change — the next track's clip as well. When a released clip is unloaded is decided by the asset cache.
- On `OnDisable` the audio is stopped and the clip is released. A request that was loading at that moment is cancelled.
- A track request during loading — for the same track or another — gives a single start: of the last requested track.
- Restarting the same track does not reload the clip if it has not been released yet.

### Limitations

- **One cover-music slot per application.** Two music handlers, or a handler and another system, override each other.
- **Music looping is set by the slot's source**, not by the handler: a music track cannot be played without looping.
- **A track change is not seamless.** The next track is requested after the previous one restarts, so the beginning of the previous track is heard: for a sound — for the event delay and the clip load, for music — for the fade time as well.
- **Short clips.** On clips shorter than ~0.5 s the audio system may miss restarts — the switch to the next track is delayed by a lap.
- **A pause that bypasses the handle** (`AudioListener.pause`, a minimised window) throws off one restart countdown: the next track may start at the wrong moment.
- **The music fade tail relies on the asset cache buffer.** With a cache group capacity of 0 a released clip is unloaded at once and the fade is cut short. Music clips need a group with a non-zero capacity.
- **Stopping music before it actually starts.** The audio system starts cover music with a delay while it fades the main theme. A stop inside that window cancels the start, but the main theme may stay dimmed until the next music change. If the previous track was fading out in that window, its fade is cut off at once.
- **Stopping paused music is audible.** The audio system stops only playing music, so the track is taken off pause and fades out with the regular fade.
- **A pause in the music track change window.** The tail of the previous track plays out its fade; the new track is paused at the moment it starts.

---

## Edge cases

| Situation | Behaviour |
|---|---|
| Empty clip list | `Debug.LogError` on start, no audio |
| `Play(n)` with a number outside the list | `Debug.LogError`, state unchanged |
| `Play` / `PlayNext` on a disabled handler | `Debug.LogError`, the request is skipped. In a switcher state the call must come after the object is enabled |
| A list element is not set | `Debug.LogError`, the previous track is stopped |
| Clip load failure, empty clip | an error in the log, the previous track is stopped |
| A failure of a load cancelled by a new request | an error in the log, the playing track is not touched |
| A clip that is already loading is requested | the previous request is cancelled, the new one joins the same load; the track starts once |
| The start did not happen (the audio system returned no track control) | the new track is silent, its clip is released, the handler is stopped; the handler logs nothing. A previous music track stays in the slot uncontrolled |
| The previous track ended while the requested one is loading | the request stays in force; in `Index` and `Random` the next-in-turn track does not replace it |
| The audio was removed from outside (displacement from the music slot, a sound clear) | the handler stops and releases the clip; if a requested track is loading at that moment, it will start |
| The requested track is the one already playing | the clip is not reloaded; the track starts over |
| Object disabled during loading | the request is cancelled, nothing starts; the clip finishes loading and stays in the cache buffer |
| Object disabled and enabled again during loading | the track starts once |
| `Pause()` on a disabled handler | has no effect, nothing is logged |
| Start during a pause | the track is paused right away and plays once the pause is lifted |
| One clip in the list, `Index` or `Random` mode | the clip plays in a loop without being restarted |
| `Stop` with no playing track | the audio does not change; a request still loading is cancelled |

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

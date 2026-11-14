# CutsceneSystem

**Namespace:** `Vortex.Sdk.CutsceneSystem.*`
**Assembly:** `ru.vortex.sdk.cutscene` (defineConstraints: `USING_SPINE`)

---

## Purpose

Playback of a Spine cutscene split into phases: one cutscene at a time, phase switching by command or automatically, music, per-phase ambient and animation-event sounds.

- **Control bus** `CutsceneController` — holds which cutscene is open and which phase it is on, reports changes through events.
- **View** `CutsceneView` — shows the open cutscene on its Spine component, plays its audio, loads and releases its assets.
- **Cutscene description** `CutsceneData` — a light asset: the skeleton, ambients and music sit behind `AssetHandle<T>` and occupy memory only while the cutscene is shown.
- **Catalog** `CutsceneCatalog` — the project's cutscene index by key (asset name).

Out of scope:

- **Launching a cutscene.** The package does not know who opens a cutscene or when: scripts, the gallery and launch handlers call `CutsceneController.Open` themselves.
- **Naninovel.** The package does not depend on it.
- **Freezing the skeleton on pause.** Done by `SpinePauseHandler` from `SpineExtensions`.
- **Unlocking and saving.** The playback state is not part of the save.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `spine-unity`, `spine-csharp` | `SkeletonAnimation`, `SkeletonGraphic`, `SkeletonDataAsset`, `AnimationState` |
| `Vortex.Core.AssetHandleSystem` | `AssetHandle<T>` — the cutscene's skeleton and clips |
| `Vortex.Core.AudioSystem`, `Vortex.Unity.AudioSystem` | `AudioController`, playback control handles, `SoundClip` (clip selection with anti-repeat), `AntiRepeatMode`, `[AudioChannelName]` |
| `Vortex.Core.DatabaseSystem`, `Vortex.Unity.DatabaseSystem` | `[DbRecord(typeof(Sound))]` — animation-event sounds |
| `Vortex.Sdk.Core.GameCore` | `GameController.OnGameStateChanged`, `GameStates` — pause and closing when the session ends |
| `Vortex.Unity.Extensions` | `AssetDatabaseExt.GetSingletonAsset` — catalog lookup |
| `UniTask` | async loading inside the view |
| Odin Inspector | inspector attributes of the model and the catalog |

The package does not choose the `AssetHandle<T>` implementation: `DirectAssetHandle<T>` or `AddressableAssetHandle<T>` is set in the cutscene data.

---

## Usage

### Setting up a cutscene (for designers)

1. Create the asset: `Create → Vortex → Cutscenes → Cutscene Data`. The asset name is the cutscene key.
2. In the `skeleton` field pick a handle implementation and assign the `SkeletonDataAsset`.
3. Press **Sync Spine Phases** — phases and events are pulled from the skeleton.
4. Arrange the phase order. For each phase:
   - `Animation Looped` on — a held loop: the phase keeps playing until the `Next` command;
   - off — a pass-through phase: after one pass it switches to the next phase by itself.
5. Assign phase ambients, music and event sounds (see "Audio").
6. Add the cutscene to `CutsceneCatalog` (`Create → Vortex → Cutscenes → Cutscene Catalog`, one per project).

### The view object

On a scene object or a prefab:

- `CutsceneView` — exactly one of its fields is assigned: `Skeleton Animation` or `Skeleton Graphic`;
- the Spine component itself with no skeleton assigned — the view supplies it;
- `SpinePauseHandler` — so the animation freezes on pause.

### Launching from code

```csharp
public class CutsceneLauncher : MonoBehaviour
{
    [CutsceneKey] [SerializeField] private string cutscene;

    public void Launch() => CutsceneController.Open(cutscene);
    public void Continue() => CutsceneController.Next();
    public void Interrupt() => CutsceneController.Close();
}
```

`Open` also accepts the asset itself: `CutsceneController.Open(cutsceneData)` — no catalog needed for that.

---

## Architecture

```
Bus
  CutsceneController (static)      — playback state + events; loads no assets, shows nothing

Models/
  CutsceneModel                    — playback state: Cutscene, PhaseIndex (written by the controller only)
  CutsceneData : ScriptableObject  — cutscene description
  CutscenePhase                    — phase: animation, looping, ambient
  EventToAudioData                 — animation event → sound from the database
  CutsceneSound                    — clip set behind AssetHandle + channel + anti-repeat

Catalog/
  CutsceneCatalog : ScriptableObject — "key → CutsceneData" index

View/
  CutsceneView : MonoBehaviour     — playback, audio, asset loading and release

Attributes/, Editor/
  CutsceneKeyAttribute (+ drawer)  — catalog key dropdown on a string field
```

The controller is static, with no config and no interface: it has three commands and two state fields.

### Phases

A phase animation is always started looped. Only the way out of the phase differs:

| Phase | `AnimationLooped` | Exit |
|---|---|---|
| Held loop | yes | only by `CutsceneController.Next()` |
| Pass-through | no | by itself, at the end of the first pass |

The phase index lives in the controller. The view reports the end of a pass-through phase's pass via `CompletePhase(index)`; the controller switches the phase — with the same event as `Next`. On a pass-through phase `Next` does nothing: this rules out a double step when the command coincides with the automatic transition.

### Audio

| What | Where it is set | Storage | Memory lifetime |
|---|---|---|---|
| Music | `CutsceneData.music` | `CutsceneSound` | while the cutscene is shown |
| Phase ambient | `CutscenePhase.Ambient` | `CutsceneSound` | while the cutscene is shown |
| Animation-event sound | `EventToAudioData.AudioPack` | `[DbRecord(typeof(Sound))]` | per the sound database policy |

`CutsceneSound` is a clip set, a channel and an anti-repeat mode. One clip is picked per playback; the pick is done by AudioSystem's `SoundClip` — the same way as for database sounds. Volume and pitch are not configurable.

Event sounds are light and repeat across cutscenes, so they come from the shared database and are not loaded with the cutscene.

**The phase ambient** is resolved by walking back from the current phase:

- the phase has an ambient — it plays;
- none, `EmptyAmbient = Inherit` — the ambient of the nearest previous phase is used;
- none, `EmptyAmbient = Stop` — silence.

Moving to a phase with the same resulting ambient does not restart the sound.

**Music** plays through AudioSystem's cover-music slot. The clip is picked once per showing; after a pause the same clip plays.

The view starts its own sounds with a control handle and stops them itself. It does not touch other systems' sounds.

### Loading

The view owns loading and release.

```
CutsceneController.OnOpened
    ↓
[View] start set, in parallel:
    ├── skeleton
    ├── music
    └── ambient of the current phase
    ↓
[View] skeleton applied → phase animation, music, ambient
    ↓
[View] in the background: ambients of the remaining phases
```

- A phase switches immediately: all animations live in one skeleton. The phase ambient, if still loading, starts once loaded.
- Closing the cutscene and `OnDisable` cancel unfinished loads and release every loaded handle.
- The handle's loaded state lives in the `CutsceneData` asset itself: there is no runtime copy of the data. The handles are owned by the active view, which is why there is only one.

### Pause

| `GameStates` | View audio | Skeleton |
|---|---|---|
| `Paused`, `Loading` | ambient and event sounds paused, music stopped | frozen by `SpinePauseHandler` |
| others | unpaused, music started again | unfrozen by `SpinePauseHandler` |
| `Off` | the controller closes the cutscene | — |

The view does not freeze the skeleton: `timeScale = 0` gets overwritten by external polling of Spine actors (see the `SpineExtensions` README).

---

## Contract

### Guarantees

- **One cutscene.** `Open` over an open cutscene closes the previous one first: subscribers get `OnClosed`, then `OnOpened`.
- **State before event.** `Data` changes before the event is raised — a subscriber always sees the result of the command, including when commands are called from a handler.
- **Late subscriber.** A view enabled while a cutscene is already open picks it up from the current phase.
- **Idempotence.** `Close` with no open cutscene, `Next` on the last phase, a stale `CompletePhase` — do nothing.
- **Release.** Everything the view loaded it releases when the cutscene closes and on `OnDisable` (including object destruction).

### Discipline

- **One active view.** A second one enabled at the same time takes no part in playback — an error is logged.
- **Exactly one skeleton is assigned in `CutsceneView`.**
- **One `CutsceneCatalog` per project**, located in `Resources` — otherwise lookup by key does not work in a build.
- **`SpinePauseHandler` on the view object** — without it the animation does not freeze on pause.
- **Phase animation keys exist in the skeleton.** A pass-through phase with a missing animation never completes.

### Limitations

- Load failures and setup errors are logged; the cutscene stays open — closing it is up to the calling code.
- AudioSystem has a single cover-music slot per application: a cutscene and another system playing cover music at the same time override each other.
- In edit mode the skeleton of a lazy handle is found by the GUID of its `reference` field — the animation dropdowns and **Sync Spine Phases** depend on that field name in `AddressableAssetHandle<T>`.

---

## Edge cases

| Situation | Behaviour |
|---|---|
| `Open(null)` | `Debug.LogError`, state unchanged |
| `Open` of a cutscene with no phases | `Debug.LogError`, state unchanged |
| `Open(key)` with no catalog or an unknown key | `Debug.LogError`, state unchanged |
| `Open` while a cutscene is open | `OnClosed`, then `OnOpened` |
| `Next` on a pass-through phase | nothing |
| `Next` / end of a pass-through phase on the last phase | nothing; the animation keeps looping |
| `Next` before the start set is loaded | the phase index changes; the view starts from the new phase |
| `Next` before the phase ambient is loaded | the animation switches immediately, the ambient starts once loaded |
| `GameStates.Off` | the cutscene closes |
| View enabled while a cutscene is open | playback from the current phase |
| View disabled and enabled again | assets released and loaded again, playback from the current phase |
| A second view enabled at the same time | `Debug.LogError`, takes no part in playback |
| No skeleton or both skeletons assigned in the view | `Debug.LogError`, no playback |
| Cutscene skeleton not set or empty | `Debug.LogError`, no playback |
| Phase animation missing in the skeleton | `Debug.LogError`; the animation does not change, the ambient switches |
| Skeleton load failure | `Debug.LogError`, no playback, the cutscene stays open |
| Sound load failure | `Debug.LogError`, playback continues without that sound |
| Empty element in a clip set | skipped |
| Animation event with no sound assigned | nothing |
| Duplicate names in the catalog | warning in the catalog inspector and in the log; the first cutscene is returned for the key |
| Pause during loading | audio does not start until unpaused |

---

## Editor

- **Sync Spine Phases** (`CutsceneData` button) — removes phases whose animations are missing in the skeleton, adds phases for new animations; same for events, duplicate events are removed.
- **Dropdowns** for `AnimationKey` and `EventName` — the animations and events of the cutscene's skeleton.
- **`[CutsceneKey]`** — a catalog key dropdown on a `string` field. The first entry `[NONE]` is the empty value. A key no longer present in the catalog is not overwritten: the field is highlighted red with a "missing" note, replacement only by an explicit pick.
- **Catalog** — the duplicate-key warning is shown right in the inspector.

---

## Public API

```csharp
namespace Vortex.Sdk.CutsceneSystem
{
    public static class CutsceneController
    {
        public static CutsceneModel Data { get; }
        public static bool IsOpen { get; }
        public static IReadOnlyList<string> AllKeys { get; }

        public static event Action OnOpened;
        public static event Action OnPhaseChanged;
        public static event Action OnClosed;

        public static void Open(CutsceneData cutscene);
        public static void Open(string key);
        public static void Next();
        public static void CompletePhase(int phaseIndex);   // reported by the view
        public static void Close();
    }
}

namespace Vortex.Sdk.CutsceneSystem.Models
{
    public class CutsceneModel
    {
        public CutsceneData Cutscene { get; }    // null — no cutscene is open
        public int PhaseIndex { get; }
    }

    public class CutsceneData : ScriptableObject
    {
        public AssetHandle<SkeletonDataAsset> Skeleton { get; }
        public List<CutscenePhase> Phases { get; }
        public List<EventToAudioData> EventToAudioDatas { get; }
        public CutsceneSound Music { get; }
    }

    public class CutscenePhase
    {
        public enum EmptyAmbientMode { Inherit, Stop }

        public string AnimationKey { get; }
        public bool AnimationLooped { get; }
        public CutsceneSound Ambient { get; }
        public EmptyAmbientMode EmptyAmbient { get; }
    }

    public class EventToAudioData
    {
        public string EventName { get; }
        public string AudioPack { get; }         // GUID of a Sound record
    }

    public class CutsceneSound
    {
        public IReadOnlyList<AssetHandle<AudioClip>> Clips { get; }
        public string Channel { get; }
        public AntiRepeatMode AntiRepeat { get; }
        public bool IsEmpty { get; }
    }
}

namespace Vortex.Sdk.CutsceneSystem.Catalog
{
    public class CutsceneCatalog : ScriptableObject
    {
        public IReadOnlyList<string> Keys { get; }
        public CutsceneData Get(string key);
    }
}

namespace Vortex.Sdk.CutsceneSystem.View
{
    public class CutsceneView : MonoBehaviour { }   // configured in the inspector
}

namespace Vortex.Sdk.CutsceneSystem.Attributes
{
    public class CutsceneKeyAttribute : PropertyAttribute { }
}
```

A custom view instead of `CutsceneView` follows the same contract: subscribe to the controller's three events, read `CutsceneController.Data`, call `CompletePhase(index)` at the end of a pass-through phase's pass, load and release the cutscene's handles on its own side.

# RecordMarksSystem

**Namespace:** `Vortex.Sdk.RecordMarksSystem.*`
**Assembly:** `ru.vortex.sdk.recordmarks`

---

## Purpose

A registry of named boolean marks attached to `Database` preset GUIDs. A mark is a fact — «this event occurred for this record» (unlocked, viewed, ever purchased). The package stores only the mark-to-GUID relation; the content of the marked entities is out of scope.

Capabilities:

- Named marks. Each name is declared in a SO config and lives either per-slot or per-account.
- Ratchet semantics: `Mark` is idempotent; `Unmark` is available but is contractually intended for debug/cheat.
- Save routing. `slotMarks` are persisted via `SaveController` through `IGameData`; `globalMarks` via `GlobalSaveController` through `IGlobalData`.
- Change events. `OnMarked` / `OnUnmarked` are fired end-of-frame in a batch through `TimeController.Accumulate`.
- Vortex reactive pipeline. `GameController.CallUpdateEvent` is raised together with the event batch.
- Editor window. Read-only dump of the current state under `Tools/Vortex/Record Marks/Index`.
- Static bootstrap. Initialised via `[RuntimeInitializeOnLoadMethod]`; not part of `Loader` / `DriverConfig`.

Out of scope:

- Content of the marked entities (gallery cards, cutscenes, bestiary entries).
- Enumeration of the full candidate list (for «X out of Y» progress) — the consumer's job.
- UI handlers and mark presentation.
- Driver substitution. The package is pinned to the `SaveController` + `GlobalSaveController` pair.

---

## Dependencies

| Dependency | Purpose |
|-------------|---------|
| `Vortex.Sdk.Core` (`GameController`, `GameModel.IGameData`) | Slot model resolve, update batching |
| `Vortex.Core.SaveSystem` (`GlobalSaveController`, `IGlobalData`) | Global model resolve, commit after mutation |
| `Vortex.Core.LoggerSystem` | Diagnostics |
| `Vortex.Unity.AppSystem.System.TimeSystem` (`TimeController.Accumulate`) | End-of-frame event batching |
| `Vortex.Unity.CoreAssetsSystem` (`ICoreAsset`) | Auto-creates the settings SO in `Resources/Settings/` |
| Unity Engine | `ScriptableObject`, `Resources.Load`, `RuntimeInitializeOnLoadMethod`, EditorWindow |

---

## Architecture

```
[Resources/Settings/RecordMarksSettings.asset]     — SO with two mark-name lists
             │
             ▼
      RecordMarksBus (static)                       — entry point, API + events
             │
             ├──► RecordMarksSlotData  : IGameData    — Dictionary<mark, List<guid>>
             │      (per-slot, in GameModel)
             │
             └──► RecordMarksGlobalData : IGlobalData — Dictionary<mark, List<guid>>
                    (per-account, in GlobalModel)
```

Bootstrap runs in two phases:

1. `[RuntimeInitializeOnLoadMethod BeforeSceneLoad]` — resets static state, loads the SO, validates mark names, subscribes to `GlobalSaveController.OnInit` and `GameController.OnNewGame` / `OnLoadGame`.
2. Readiness is set once both models are resolved. Until `IsReady = true`, any `Mark`/`Unmark` returns warning + no-op.

The runtime cache `Dictionary<mark, HashSet<guid>>` provides O(1) operations over the GUID sets. The models store `List<guid>` because the Vortex serializer handles `IList`, not `ISet`. The bus keeps both `List` and `HashSet` in sync on every mutation.

Vortex serializer merge semantics: when a slot is loaded, the outer `Dictionary<mark, List<guid>>` is merged (marks new in the SO stay as empty lists), while the inner `List<guid>` is replaced wholesale (state comes from the save file).

`OnMarked` / `OnUnmarked` are not raised synchronously. Every mutation appends a record to the `_pending` queue and schedules `FlushEvents` through `TimeController.Accumulate`. The queue is drained once per `LateUpdate` together with `GameController.CallUpdateEvent`.

---

## Contract

### Input

- `Assets/Resources/Settings/RecordMarksSettings.asset` — SO with `slotMarks: string[]` and `globalMarks: string[]`. Names are unique within each list and do not overlap between lists. Name format is not validated beyond non-emptiness.
- `GlobalSaveController` — initialised (has passed `IProcess`). `GameController` — has fired `NewGame` or `LoadGame`.

### Output

- `RecordMarksBus.IsMarked(mark, guid)` — mark presence on a GUID.
- `RecordMarksBus.GetMarked(mark)` — all GUIDs under a mark.
- Events `OnReady`, `OnMarksLoaded`, `OnMarked`, `OnUnmarked`.
- Mutation of `RecordMarksSlotData.Data` / `RecordMarksGlobalData.Data` — Bus only. The `Data` field is `internal`, unreachable outside the assembly.
- Serialisation — standard `IGameData` / `IGlobalData` contract; no hand-written save code in the package.

### Guarantees

- **Ratchet.** `Mark(m, g)` for an already-marked `(m, g)` is a silent no-op; no event.
- **Batching.** `OnMarked` / `OnUnmarked` fire once per `LateUpdate` in accumulation order. There is a 1-frame delay between mutation and event.
- **Owner-lock.** The Bus is the sole writer of `_slotCache` / `_globalCache` / `_pending`. The models are locked **structurally at the assembly boundary**: the `Data` field is `internal` + `[IsPOCO]`, unreachable from outside the assembly for both read and write; the Vortex serializer accesses it via reflection.
- **Re-entrant Bootstrap.** Fast Enter Play does not accumulate subscriptions: `-= then +=` before each hook.
- **Idempotent OnGlobalReady / OnSlotChanged.** A duplicate call re-syncs state against the SO without data loss.
- **Slot isolation.** On `OnLoadGame` / `OnNewGame`, `_pending.Clear()` runs first — an event from the previous session cannot reach the next session's consumer.
- **Fail-safe diagnostics.** Unknown mark, empty guid, mutation before `IsReady`, overlapping mark, mark from save not declared in SO — all log-warning / log-error, none crash.

### Constraints

| Constraint | Rationale |
|-------------|-----------|
| One SO per project in `Resources/Settings/` | Bootstrap loads a single asset at a fixed path |
| Mark name is a globally-unique string | `SlotMarks ∩ GlobalMarks = ∅`; a duplicate within a list is dropped from the index too |
| Renaming a mark between releases loses its save state | Stable key is a developer contract |
| Live-editing the SO at runtime is not supported | Bootstrap loads the SO once |
| Missing SO disables the package | The SO is auto-created as an `ICoreAsset`; if still absent — log-error + every `Mark`/`Unmark` returns warning |
| Dead GUID (preset removed) is not automatically pruned | Cleanup — job of an editor tool or migration |
| Not thread-safe | All operations are on the Unity main thread |

---

## API

### `RecordMarksBus` (static)

| Member | Description |
|--------|-------------|
| `bool IsReady { get; }` | Both models resolved, SO loaded, package ready for mutations |
| `event Action OnReady` | First time `IsReady` becomes `true` |
| `event Action OnMarksLoaded` | After every `OnSlotChanged` (`NewGame` / `LoadGame`). Consumer re-reads state |
| `event Action<string, string> OnMarked` | `(mark, guid)` — mark set; end-of-frame batch |
| `event Action<string, string> OnUnmarked` | `(mark, guid)` — mark removed; end-of-frame batch |
| `void Mark(string mark, string guid)` | Mark GUID. Ratchet: repeat is a no-op |
| `void Unmark(string mark, string guid)` | Remove a mark. Public, but contractually for debug/cheat |
| `bool IsMarked(string mark, string guid)` | Presence check |
| `IReadOnlyCollection<string> GetMarked(string mark)` | All GUIDs under a mark; live wrapper over the internal HashSet, no-alloc |

### `RecordMarksSettings` (SO)

| Field | Description |
|-------|-------------|
| `SlotMarks : IReadOnlyList<string>` | Marks that live per-slot (`SaveController`) |
| `GlobalMarks : IReadOnlyList<string>` | Marks that live per-account (`GlobalSaveController`) |

Creation: automatic — the SO implements `ICoreAsset`, the Core Assets controller places an instance in `Resources/Settings/` (`Tools → Vortex → Debug → Check Core Assets`, or on domain reload when auto-mode is on). Manual — `Create → Vortex → Settings → RecordMarks`.

### Models (for reflection registries, not for direct use)

| Type | Role |
|------|------|
| `RecordMarksSlotData : GameModel.IGameData` | Registered via reflection in `GameModel` |
| `RecordMarksGlobalData : IGlobalData` (`GetGlobalKey() = "Vortex.RecordMarks.Global"`) | Registered via reflection in `GlobalSaveController` |

The `Data` field of both models is `internal` + `[IsPOCO]`: outside the package assembly it can neither be replaced nor read (owner-lock held by `RecordMarksBus`), while the Vortex serializer accesses it via reflection.

---

## Usage

### Setup

1. The settings SO is created automatically (implements `ICoreAsset`): `Tools → Vortex → Debug → Check Core Assets`, or on domain reload when auto-mode is on. Manually — `Create → Vortex → Settings → RecordMarks` in `Assets/Resources/Settings/`.
2. Fill `slotMarks` and `globalMarks` with unique names. Dot-namespace is recommended: `"gallery.unlocked"`, `"codex.viewed"`, `"shop.everPurchased"`.

### Consumer

```csharp
public class GalleryCardView : MonoBehaviour
{
    [SerializeField] private string _cardGuid;

    private void OnEnable()
    {
        RecordMarksBus.OnMarked += HandleMarked;
        Refresh();
    }

    private void OnDisable()
    {
        RecordMarksBus.OnMarked -= HandleMarked;
    }

    private void HandleMarked(string mark, string guid)
    {
        if (mark == "gallery.unlocked" && guid == _cardGuid)
            Refresh();
    }

    private void Refresh()
    {
        var unlocked = RecordMarksBus.IsMarked("gallery.unlocked", _cardGuid);
        // toggle UI
    }
}
```

### Mutation

```csharp
// Standard flow — Mark only. Idempotent.
RecordMarksBus.Mark("gallery.unlocked", cardGuid);

// Debug/cheat.
RecordMarksBus.Unmark("gallery.unlocked", cardGuid);
```

### Progress

```csharp
// RecordMarks knows only how many are marked. The full candidate list is
// the consumer's side (usually enumerating Database presets).
var unlockedCount = RecordMarksBus.GetMarked("gallery.unlocked").Count;
var total = Database.GetRecords<GalleryCard>().Length;
Debug.Log($"{unlockedCount} / {total}");
```

### Editor window

`Tools → Vortex → Record Marks → Index` — read-only dump of the current Bus state. Available only in Play Mode after Bootstrap. Name filter, foldout per mark with the list of GUIDs.

`Tools → Vortex → Record Marks → Settings` — ping the SO in Project window.

---

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| SO not found at `Resources/Settings/RecordMarksSettings` | Normally never — the SO is auto-created (`ICoreAsset`); if auto-creation did not run: `LogError` once in Bootstrap, `IsReady` stays `false` forever, every `Mark`/`Unmark` warns |
| Mark present in both `slotMarks` and `globalMarks` | `LogError`, excluded from both lists; operations with that name become no-op |
| Empty / whitespace name in the list | `LogWarning`, skipped |
| Duplicate name within the same list | `LogWarning`, second and subsequent occurrences skipped |
| `Mark(unknown, guid)` | `LogWarning`, no-op |
| `Mark(mark, "")` | `LogWarning`, no-op |
| `Mark` before `IsReady` | `LogWarning`, no-op |
| `Mark(m, g)` while already marked | Silent no-op, no event |
| `Unmark(m, g)` while not marked | Silent no-op, no event |
| Mark in the save file not declared in SO | `LogWarning`, key removed from model on `Sync` |
| Mark in SO not in the save file | Key created with an empty list |
| GUID of a marked preset is deleted from Database | Dead ID stays in the HashSet, does no harm; cleanup is a migration job |
| `NewGame` during an active session | `_pending.Clear()` first line of `OnSlotChanged`; previous-session events cannot reach consumers |
| Bus accessed from another thread | Not supported; operations expected on the main thread |
| Fast Enter Play, static state persists | `-= then +=` before subscription — no duplicate registration |
| Consumer calls `Mark` from an `OnMarked` handler | Nested Mark joins the current FlushEvents wave; the ratchet guard keeps it finite |
| `GetMarked` returns `HashSet` via `IReadOnlyCollection` | Downcasting to `HashSet<string>` and mutating breaks the read-only contract |

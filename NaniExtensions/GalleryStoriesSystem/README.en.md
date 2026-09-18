# GalleryStoriesSystem

**Namespace:** `Vortex.NaniExtensions.GalleryStoriesSystem.*`
**Assembly:** `ru.vortex.nani.gallery.stories` (define: `USING_NANINOVELL`)
**Layer:** NaniExtensions (Unity-side wrapper over Naninovel)
**Package type:** consumer for [GallerySystem](../../Sdk/GallerySystem/README.en.md) — narrative gallery cards

---

## Purpose

The third gallery content type — **narrative cards**: a preview tile in the gallery grid; on click, the player replays a short nani script (dialog scene, memory, flashback). Example: a scenes gallery in a visual novel, where previously seen lines and dialogues are available for replay.

Features:

- **`GalleryStoryModel`** implements `IGalleryEntry` from GallerySystem — enters the gallery pool automatically.
- **`GalleryStoryPreset`** holds the standard `RecordPreset<T>` fields plus a string path to the nani script (`Script.Path`).
- **Driver-neutral path** instead of a direct reference to `Naninovel.Script`. Reason: Naninovel manages Script instance lifetime itself (`ScriptPlayer.ResetService()` in `OnLoadGame` may unload a loaded script), after which the direct reference becomes fake-null. The string is always valid.
- **Designer UX**: a helper `Naninovel.Script script` field (non-serialized) in the inspector. Drag the asset — an Odin callback copies its `Path` to the serialized field and nulls the reference.
- **InfoBox validation**: the preset shows path status (empty / ok / not found) with the found script name.
- **Direct `NaniWrapper` call** — no bus/viewer intermediary: `model.Show()` → `NaniWrapper.PlayScript(scriptPath)`, `model.Hide()` → `ScriptPlayer.Stop()`. Naninovel owns the UI, nothing to coordinate.

Out of scope:

- **UI for playing the nani script** — Naninovel provides its own UI (`ScriptPlayer` + `TextPrinter` + other services) end-to-end. The package doesn't touch it.
- **Card unlocking** — `RecordMarksSystem`; this package doesn't write there.
- **ScriptPlayer control** — delegated to `NaniWrapper.PlayScript` (which gates Load→Play, mutes spurious Stop events, and handles empty paths cleanly).

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Sdk.GallerySystem` | `IGalleryEntry` contract |
| `Vortex.Core.DatabaseSystem` | `Record`, entry into the gallery pool |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>` |
| `Vortex.Core.Extensions.LogicExtensions` | `ObjectExtCopy.CopyFrom` |
| `Vortex.NaniExtensions.Core` | `NaniWrapper.PlayScript`, `NaniWrapper.ScriptPlayer.Stop` |
| Naninovel | `Script` (auto-referenced) |
| Odin Inspector | `[ShowInInspector]`, `[OnValueChanged]`, `[InfoBox]` (auto-referenced) |
| UniTask | For `Forget()` fire-and-forget |

---

## Architecture

```
GalleryStoryPreset (SO)
    ├── [SerializeField] Icon (preview)         — from RecordPreset<T> base
    ├── [SerializeField, ReadOnly, InfoBox]     — driver-neutral Script.Path string
    │   private string scriptPath
    └── [NonSerialized, ShowInInspector, OnValueChanged]  — UX helper (drag&drop)
        private Script script
                    │
                    │   OnValueChanged: scriptPath = script.Path; script = null
                    │
                    │   Database.GetRecords → RecordPreset<T>.GetData()
                    │       ↓
                    ▼
GalleryStoryModel (Record, IGalleryEntry)
    ├── GuidPreset, Icon, Name, Description     — public (from Record + CopyFrom)
    ├── internal ScriptPath                     — copy of the string from the preset
    ├── Show()      → Show(CancellationToken.None)
    ├── Show(ct)    → +sub OnNaniStop → NaniWrapper.PlayScript(ScriptPath, token: ct).Forget()
    │                     │
    │                     └──(nani finished on its own)── HandleAutoStop → -unsub → Hide() (no-op)
    │
    └── Hide()      → if PlayedScript.Path == ScriptPath → ScriptPlayer.Stop(); else no-op
                    │
                    ▼
Naninovel (owns the UI channel: printers, actors, audio — out of scope for this package)
```

Two layers shorter than `GallerySpritesSystem`: no bus, no viewer, no controller extension. Naninovel is its own UI — nothing to coordinate.

---

## Contract

### `GalleryStoryModel : Record, IGalleryEntry`

```csharp
public class GalleryStoryModel : Record, IGalleryEntry
{
    public Sprite Icon { get; protected set; }
    internal string ScriptPath { get; set; }

    public bool CopyFrom(SoData source);       // copies Path from the preset
    public void Show();                        // NaniWrapper.PlayScript(ScriptPath).Forget()
    public void Show(CancellationToken ct);    // with cancellation token
    public void Hide();                        // Stop only if PlayedScript.Path == ScriptPath
}
```

### `GalleryStoryPreset : RecordPreset<GalleryStoryModel>`

```csharp
public class GalleryStoryPreset : RecordPreset<GalleryStoryModel>
{
    [SerializeField, ReadOnly, InfoBox(...)] private string scriptPath;
    [NonSerialized, ShowInInspector, OnValueChanged(nameof(OnScriptChanged))]
    private Script script;

    public string ScriptPathTemplate => scriptPath;
}
```

### Invariants

- **I1.** `ScriptPath` is internal; unreachable from outside the assembly.
- **I2.** The serialized SO stores only the `scriptPath` string; references to `Naninovel.Script` are not saved.
- **I3.** `Show`/`Hide` are synchronous (as `IGalleryEntry` requires). The async `PlayScript` goes into `Forget()` — the model doesn't block its caller.
- **I4.** `Show` subscribes the model to `NaniWrapper.OnNaniStop`; when **our** script finishes — auto-`Hide()`. Lifecycle is symmetric: `Show → (nani finished) → Hide()`, no external call required.
- **I5.** Auto-`Hide` is idempotent with a manual `Hide`: if the player clicked "Close" → `Stop()` → OnNaniStop → HandleAutoStop → `Hide()` — the second `Hide` no-ops (PlayedScript is already null).

---

## Creating a card (for the designer)

1. **Create the preset:** Project → Create → Vortex → Presets → Gallery → Story. You get a `GalleryStoryPreset`.
2. **Fill the base fields:** Name, Description, Icon (preview for the gallery tile).
3. **Drag the nani script** into the `script` field in the inspector. The Odin callback immediately:
   - Copies `script.Path` into the `scriptPath` (serialized) field.
   - Nulls `script` (not serialized).
   - Marks the preset dirty for saving.
4. **Check the InfoBox under `scriptPath`:**
   - Green (Info) — "OK: `<script_name>` (Path: `<path>`)" — the path is correct, the script is found in the project.
   - Yellow (Warning) — "Path to the script is not set".
   - Red (Error) — "Script with Path not found in the project" — the path is set but no nani script with such Path exists. The script may have been deleted or renamed.
5. **Unlock mark:** `RecordMarksSettings` must contain a mark that story mechanics will apply to the card's `GuidPreset`. The gallery scene designer specifies that mark in `GalleryView.marks`.

The card enters `GalleryView` automatically when its type is allowed and its `GuidPreset` is tagged with a listed mark. Clicking "Show" on the gallery card (`GalleryCardView.Show`) triggers `entry.Show()` → the nani script starts.

---

## Edge cases

| Situation | Behavior |
|---|---|
| Empty `ScriptPath` | `NaniWrapper.PlayScript` logs an error and returns; the script won't start |
| `ScriptPath` points to a deleted nani script | Naninovel throws "Failed to get resource" to the log; NaniWrapper treats it as absence |
| Designer dragged Script manually | `OnValueChanged` copies `Path` into `scriptPath` and clears `script`. Only the serialized field is visible after |
| Nani script unloaded between shows | The package stores only the string; the next `Show` makes Naninovel reload the script by path. This architecture solves the unload issue |
| "Show" clicked while a script is active | `NaniWrapper.PlayScript` gates launches: until one Load→Play completes, the next one waits |
| `Hide()` with no active script | `PlayedScript == null` check — no-op |
| `Hide()` while a different script plays (story launch between Show and Hide) | `PlayedScript.Path != ScriptPath` check — no-op; the other script is left alone |
| The nani script finished on its own (ran out of commands) | `OnNaniStop` fires → `HandleAutoStop` unsubscribes + calls `Hide()` (no-op at that point, but symmetrically closes the cycle) |
| Our script is playing when another one starts (@goto to an external script) | `OnNaniStop` fires with a new `PlayedScript.Path != ScriptPath` → HandleAutoStop unsubscribes + `Hide()` no-op |
| Repeated `Show` on the same instance | `-=` before `+=` in Show guards against double subscription |
| Project without `USING_NANINOVELL` | The package doesn't compile (define constraint) — story cards are simply not present |

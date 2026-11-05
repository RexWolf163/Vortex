# NaniScriptLinks

**Assemblies:** `ru.vortex.nani.script.links` (runtime, contract), `ru.vortex.nani.script.links.editor` (editor, window)
**Namespace:** `Vortex.NaniExtensions.NaniScriptLinks`

## Activation

Both assemblies use `defineConstraints: ["USING_NANINOVELL"]` (the `naninovellExt` toggle in `SdkSettings`). The Vortex quest source is enabled only with `USING_VORTEX_QUESTS`; without it, launches come only from scripts and Naninovel settings.

## Purpose

Naninovel script link validator: `Tools → Vortex → Debug → Nani Script Links`.

- **Orphans** — scripts with no incoming links from outside: no quest or Naninovel setting launches them and no other script jumps into them. Grouped by folder.
- **Unused labels** — labels no jump leads to (jumps from the label's own script included).
- **Broken links** — missing script; missing label; target matches an existing one only case-insensitively; label declared twice in a script; quest logic refers to a deleted script asset.
- **Problems** — what could not be read or resolved: a `.nani` file outside the registry, a registry record without a file, a launching logic without a script, an expression target, an unneeded exclusion.
- **Exclusions** — scripts and labels that are intentionally not launched, with a reason.

Clicking a place opens the script at the line in the external editor or selects the asset (quest preset, Naninovel settings); the **Find** button on each row selects the place's file in the Inspector and highlights it in the Project window. Long places and messages wrap — there is no horizontal scrolling. Works without entering Play Mode. The tool does not modify project assets — only exclusions are written.

Out of scope:
- Scenes, prefabs and other ScriptableObjects are not scanned: scripts launched only from there (`MiniGameStarter`, gallery, cheat console) end up as orphans — cover them with exclusions.
- Quest reachability is not checked: a script of a quest that is never activated counts as reachable.
- Jump conditions (`if:`) are not evaluated: a conditional jump is a link like any other.
- `NarrativeImportTool` windows and the Naninovel Script Graph are not affected.

## Dependencies

- Naninovel — `Script` and its lines (`LabelScriptLine`, `CommandScriptLine`, `GenericTextScriptLine`), `EndpointContextAttribute`, `NamedStringParameter`, `ScriptsConfiguration`, `EditorResources`
- Unity Editor — `AssetDatabase`, `SerializedObject`, `ScriptableSingleton`
- Vortex quests — by preset type name only (`t:QuestPreset`); no reference to the quests assembly

## Usage

### Building the map

The "Собрать" (Build) button. The build reads the Naninovel script registry, every registered script, all quest presets and the start scripts from settings; a progress bar with cancel. The map lives in memory only: it is reset on recompilation and on entering Play Mode. Asset changes after the build are not tracked — the build time in the window tells how fresh it is. Exclusion edits apply immediately, without a rebuild.

### Connecting a quest logic that launches a script

A quest object that launches a script implements `INaniScriptLaunch` — otherwise the validator does not see it and the script may end up an orphan:

```csharp
[Serializable]
public class RunNaniScript : QuestLogic, INaniScriptLaunch
{
    [SerializeField] private Script naninovelScript;

    string INaniScriptLaunch.ScriptPath => naninovelScript != null ? naninovelScript.Path : null;
    // ...
}
```

An object that only refers to a script (`WaitStopNaniScript` — waits for it to stop) does not implement the contract: a reference is not a launch. The contract does not change in-game behaviour. Currently implemented by `RunNaniScript` (the [QuestSystem](../QuestSystem/) package, `Vortex.NaniExtensions.QuestSystem.Logics`).

### Exclusions

Project settings: `ProjectSettings/NaniScriptLinks.asset` (under version control, shared by the team). A record is a target and a reason (required). The target is a registry path:

| Target | Example | Removes from analysis |
|--------|---------|-----------------------|
| Folder (ends with `/`) | `DEBUG/` | All scripts under the path with their labels |
| Script | `DLC1/GYMIntro` | The script with its labels |
| Label | `DLC1/GYMIntro.play` | This label only |

An exclusion applies to "Orphans" and "Unused labels"; broken links are never excluded — they are fixed. The "Исключить" / "Исключить папку" buttons move the target into the form on the "Исключения" tab, where the reason is entered. An exclusion that hides nothing (no entry points under it, or all have incoming links) is shown in "Problems" as unneeded.

## Architecture

```
NaniScriptLinks/
├── INaniScriptLaunch.cs               ← "launches a script" contract (runtime)
└── Editor/
    ├── NaniScriptLinksWindow*.cs      ← window: Orphans / Unused labels / Broken links / Problems / Exclusions
    ├── Model/
    │   ├── LinkModels.cs              ← places, link, script and label (entry points), broken link, problem
    │   ├── LinkIndex.cs               ← the map (Current — static, in memory)
    │   ├── LinkAnalysis.cs            ← orphans, unused labels, unneeded exclusions (cached by map and exclusion revision)
    │   └── ScriptLinksSettings.cs     ← exclusions (ScriptableSingleton in ProjectSettings)
    └── Sources/
        ├── LinkIndexBuilder.cs        ← build and Rebuild: registry → scripts → quests → settings → link resolution
        ├── ScriptSource.cs            ← registry and parsing of compiled scripts
        └── QuestSource.cs             ← quest preset traversal (USING_VORTEX_QUESTS)
```

Dependencies are one-way: window → builder and sources → model. The model knows neither the builder nor the sources. The window only displays the map; the builder runs the build, and only settings methods change exclusions. The place of a link or finding (`LinkLocation`) is polymorphic: `Describe` (caption), `Open` (navigate: a script at its line, anything else is selected), `Find` (select the file in the Inspector).

### Entry points and reachability

The entry point index: `{script}` (start) and `{script}.{label}` — each with its list of incoming links. Entry points are created only from the registry and declared labels; a link that finds no entry point is a broken link.

- A **script** is reachable if it has at least one incoming link from outside — from another script, a quest or settings — to its start or to any of its labels. A link from a script to itself (`.Label`, `@goto <own path>`) does not make it reachable. Reachability is direct: two scripts that refer only to each other are both reachable.
- A **label** is used if at least one link leads to it, including a jump from its own script.
- State is checked in order: "has incoming" → "excluded" → "orphan / unused label".

### Link sources

| Source | What produces a link |
|--------|----------------------|
| Scripts | Every command parameter with `EndpointContextAttribute` — `@goto`, `@gosub`, `goto:` / `gosub:` of `@choice`; commands inlined in text lines (`[goto …]`) as well. Project commands with this attribute are picked up without changing the validator |
| Quests | Every preset object (at any depth) implementing `INaniScriptLaunch` |
| Naninovel settings | Assigned `InitializationScript`, `TitleScript`, `StartGameScript` |

Scripts are read from the compiled asset — the same parse Naninovel plays from: indentation inside `@if`/`@choice`, `if:`, `release!` are handled by it. A `.Label` target is completed with its own script path.

## Contract

### Input
- Naninovel resource registry (script category), script assets, `QuestPreset` assets, `ScriptsConfiguration`
- Quest objects implementing `INaniScriptLaunch`

### Output
- The window; writes only exclusions in `ProjectSettings`

### Guarantees
- A build replaces the map entirely or leaves it unchanged (cancel — previous map)
- Every accepted link belongs to exactly one entry point; every rejected one is exactly one broken link
- Matching is by full registry path and label only, case-sensitive; file names are not used (equal names in different folders are different scripts)
- Orphans, unused labels and unneeded exclusions are computed on display from the current map and exclusions and are not stored
- A failure in one asset does not abort the build

### Limitations
- The map does not track asset changes; rebuild manually
- An expression target (`{…}`) cannot be resolved statically — a build problem, not a link
- A deleted asset in a quest logic is detected via a serialized script asset reference inside the object (an id is present, the object is not); if not detected — "launching logic without a script" in "Problems"
- The preset type name (`t:QuestPreset`) is hard-coded in the quest source. A broken seam is loud: no presets found — a build problem

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| Map not built | Hint and the "Собрать" button; the "Исключения" tab works |
| Entering Play Mode, recompilation | Map reset, build again |
| `@goto .Label` to an undeclared label | Broken link "нет метки" (no label) |
| `@goto Script` to a script missing from the registry | Broken link "нет сценария" (no script) |
| Target differs from an existing one only in case | Broken link "регистр" (case) with the correct spelling |
| Label declared twice | Broken link "дубль метки" (duplicate label) at the second declaration; jumps count for the label |
| `StartGameScript` points to a non-existent path | Broken link "нет сценария" with the place in settings |
| A `.nani` file outside the registry (outside Scenario Root) | Problem; jumps into it are broken links |
| A registry record without a file | Problem placed at the resource registry |
| Launching logic without a script | Problem |
| Launching logic referring to a deleted asset | Broken link "удалён ассет" (deleted asset) |
| Broken type reference in a preset | Problem with the type name |
| Exception while parsing a script or traversing a preset | Problem; everything else is built |
| No quest presets found with quests enabled | Build problem: the preset type was renamed |
| Click or Find on an asset moved after the build | Warning in the console |
| A place without a file (no quest presets found) | Find is disabled |
| Exclusion with nothing under it / everything reachable | Problem "unneeded exclusion" |
| Exclusion with an empty target or reason | Not added |
| Repeated exclusion for the same target | Updates the reason |
| `USING_VORTEX_QUESTS` disabled | Quests are not scanned; the summary says "квесты Vortex отключены" |

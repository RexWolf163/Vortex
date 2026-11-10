# AssetSwapSystem

> ⚠️ **Experimental package.** Public API (`AssetSwapController.NewGroup/AddVariant/RemoveGroup/Validate/ValidateAll/Apply`), the `ProjectSettings/VortexAssetSwapSettings.asset` format and the labels schema may change without backwards compatibility. Do not rely on it in a critical CI pipeline before it has been battle-tested across several publish cycles; keep a clean git state before `Apply` so rollback is trivial.

**Namespace:** `Vortex.Unity.AssetSwapSystem.*`
**Assembly:** `ru.vortex.unity.assetswap` (`includePlatforms: ["Editor"]`)
**Layer:** Unity (Editor-only build-pipeline utility)
**Config:** `ProjectSettings/VortexAssetSwapSettings.asset` (`ScriptableSingleton`, outside `Assets`, not shipped, versioned in git)

> 🚀 **Running from an external builder.** The swap can run without opening the editor — one command line from CI / a build script, with a non-zero exit code on any error:
>
> ```
> Unity.exe -batchmode -quit -projectPath <path> \
>   -executeMethod Vortex.Unity.AssetSwapSystem.AssetSwapController.ApplyFromCommandLine \
>   -assetSwap publisher:nutaku
> ```
>
> Details, scenarios and pitfalls — [Running from an external builder](#running-from-an-external-builder).

---

## Purpose

Editor-only utility for swapping assets to different variants before building for a specific publishing platform (Steam / GOG / mobile / console stores / censored version / etc). The designer keeps all variants in Editor folders, tags them and the target assets with standard Unity Asset Labels, and a single button before the build atomically overwrites the target contents with the chosen variant — **without changing GUIDs**.

Features:

- **Native Asset Labels** — the same ones you see in the Unity inspector (`Prop`, `Vegetation`). No side-channel tagging.
- **Bit-copy of file contents via `File.Copy`** — the target's `.meta` is not touched; GUIDs stay stable; all existing references (`[SerializeField]`, `AssetReference`, Addressables, scenes, prefabs) remain valid.
- **Multi-file case (Spine, FBX + material)** — works natively: each file of the multi-file structure is labeled separately, pairs are matched automatically by file name.
- **Validator before Apply** — checks pair completeness, reverse-completeness, importer type match, name uniqueness, and absence of runtime references to variant assets. Errors block Apply.
- **Running from an external builder (command line)** — `unity -batchmode -executeMethod …ApplyFromCommandLine -assetSwap group:variant` (by names — the Comment fields — or numbers) swaps variants without the editor and fails the process (code 1) on any error; `…ValidateAllFromCommandLine` — a check without swapping. For build scripts inside Unity — synchronous `Apply(int, int)` / `ApplyByComment(string)`. See [Running from an external builder](#running-from-an-external-builder).
- **Separate byte-match window** — diagnostics of which variant the current target contents currently correspond to.

Out of scope:

- **Runtime variant loading** — this utility does not exist in a build. All swaps are editor-time before build.
- **Changing importer settings in `.meta`** — never changed. If variants require different `TextureImporter` settings (sRGB, compression, sprite mode), that's the designer's responsibility via separate `.presetlike` assets.
- **"Currently active variant" state** — not stored. One asset may belong to several groups; the order of applying is up to the user; the final on-disk state is a git matter.
- **Rollback on partial `File.Copy` failure** — not performed. On an exception mid-batch: LogError with the file path + restore via git.

---

## Dependencies

- **UniTask** (`Cysharp.Threading.Tasks`) — the only external dependency (referenced in the asmdef). All heavy operations (`ValidateAsync` / `ValidateAllAsync` / `ApplyAsync`, the holder scanner) are async on UniTask with a pause between steps (`AssetSwapPause.Next()` — `UniTask.Yield()`; inside the sync wrappers — an already completed task, see [CI](#ci-build-pipeline-usage)). The package does not compile without UniTask.
- Built-in `UnityEditor` API — SettingsProvider, ScriptableSingleton, AssetDatabase, AssetImporter, EditorWindow.

No dependencies on other Vortex packages.

---

## Designer usage

> **Important — about labels.** Our labels (`AssetGroup{N}`, `AssetGroup{N}_Variant{K}`) are native Unity Asset Labels, but they **do not show up in the inspector's predefined-labels dropdown** (Unity caches that list and does not rescan it after `SetLabels` until an editor reload). Therefore **all label operations for this package go through `Project Settings → Vortex/AssetSwap`**, via the buttons described below. Do not type `AssetGroup1` by hand in the inspector — a typo will slip through silently and files will land in the wrong group.
>
> **C# scripts (`.cs`) are never labeled or swapped by this package.** `Add label to Selected` / `Add label` skip them with a `LogWarning`; `FindAssetsByLabel` (and thus Validate / Apply / Match) does not see them. Swapping code files via `File.Copy` would desynchronize `.meta`, Assembly Definitions and the compile phase; branching the codebase is a job for git branches, not AssetSwap.

### First group creation

1. **Open settings:** `Project Settings → Vortex/AssetSwap`.
2. **Select target assets in Project View** (the ones that will be replaced) → press `New Group`. `AssetGroupN` is created and the label is already applied to the selection.
3. **Optionally fill the Comment field** — "Steam", "GOG", "Censored", ...
4. **Put variant files into an Editor folder** (example: `Assets/Editor/AssetSwapVariants/Group1/Variant1/hero.png`).
   The file name must **exactly match** the target's name (with extension).
5. **Select variant files in Project View** → press `Add Variant` next to the group on the AssetSwap page. `Variant1` appears with the label already applied to the selection.
6. **Validate:** press `Validate` next to the group. Errors go to the console. `Validate` is the only trigger that refreshes the `Targets` / `Variant K` counts on the page; after manual label edits in the inspector the numbers won't move until the next validation.
7. **Apply:** after a successful validation, press `Apply` next to the desired Variant.
8. **Check the result:** `Match Window` shows which variant the current target contents correspond to.

### Assigning / removing labels after the fact

All actions live on the `Project Settings → Vortex/AssetSwap` page, on the buttons next to the group or variant.

| Task | Action |
|---|---|
| Extend the group's **target** set with more assets | Select them in Project View → next to the group press **`Add label to Selected`** (in the `Targets:` row). |
| Extend the file set for **VariantK** | Select them in Project View → next to the target `VariantK` press **`Add label`**. |
| See which assets carry the group's **target-label** | Next to the group press **`Select in Project`** — highlights them in Project View. |
| See which files carry a **variant-label** | Next to the target `VariantK` press **`Select`**. |
| Remove a target-label / variant-label from a single asset | In the Unity inspector, `Asset Labels` panel, click the × next to the label. Afterwards press `Validate` on the AssetSwap page to refresh the counts. |
| Strip all group labels (target + all variants) from every asset | On the AssetSwap page press **`Remove Group`** next to the group (confirmation dialog). File contents stay; only labels are stripped and the group record is deleted. |
| Add a new variant to an existing group | Select the new variant files → press **`Add Variant`** next to the group. |

`Add label to Selected` / `Add label` merge, not reset: existing labels stay, duplicates are not created. Folders in the selection are skipped. If the selection is empty — a `LogWarning` is emitted to the console and nothing changes.

---

## CI (build pipeline) usage

### Running from an external builder

The swap is invoked from any external builder (Jenkins, TeamCity, GitHub Actions, a bat/sh script) by a regular batchmode Unity launch — no need to open the editor. The result is read from the process exit code: `0` — all variants applied, `1` — an error (unknown group/variant, failed validation, copy failure, missing or malformed `-assetSwap` key); details are in the Unity log (`-logFile`).

**Scenario A — swap as a separate pipeline step, build as the next one:**

```
# step 1: swap variants (group:variant by names, the key repeats, applied in order)
Unity.exe -batchmode -quit -projectPath <path> -logFile swap.log \
  -executeMethod Vortex.Unity.AssetSwapSystem.AssetSwapController.ApplyFromCommandLine \
  -assetSwap publisher:nutaku -assetSwap censorship:soft

# step 2: build — a separate Unity launch with your build method
Unity.exe -batchmode -quit -projectPath <path> -logFile build.log \
  -buildTarget Win64 -executeMethod MyBuilder.Build
```

A check without swapping (e.g. on a pull request):

```
Unity.exe -batchmode -quit -projectPath <path> \
  -executeMethod Vortex.Unity.AssetSwapSystem.AssetSwapController.ValidateAllFromCommandLine
```

**Scenario B — swap inside the build method** (one Unity launch): call `ApplyByComment` / `Apply` from your own `-executeMethod` before `BuildPipeline.BuildPlayer` — see the wrapper below. Selecting a variant by comment («steam», «nutaku») is more robust than by numbers: group and variant numbers depend on creation order.

**Pitfalls:**

- **Packages that react to their asset with a delay.** The swap is synchronous, but a package that reacts to its asset change via `EditorApplication.delayCall` (e.g. `SdkSettings` defines) may not get to run before `-quit` or before `BuildPlayer` in the same call. Sync such a package explicitly in the build method (for `SdkSettings` — `SdkSettings.SyncDefinesNow()`); in scenario A — at the start of step 2.
- **Git state.** The swap overwrites files in the working copy. In CI that is the agent's working copy; locally — keep a clean git state so `git checkout` rolls it back.
- **One variant per group per run.** A repeated `-assetSwap` for the same group simply overwrites the target with the next variant.

Public API (synchronous, for build scripts):

```csharp
bool AssetSwapController.Apply(int groupIndex, int variantIndex); // true — variant copied into every target
bool AssetSwapController.ApplyByComment(string variantComment);  // in every group, the variant with this comment
int  AssetSwapController.FindVariantByComment(int groupIndex, string comment); // -1 — none
bool AssetSwapController.Validate(int groupIndex);
bool AssetSwapController.ValidateAll();
```

`ApplyByComment` matches the variant comment case-insensitively («steam», «nutaku»…); groups without such a variant are skipped with a log line. `false` — no group has the variant or at least one variant was not applied. `Apply` returns `false` on an unknown group/variant, failed validation, cancellation or a `File.Copy` failure (`ApplyAsync` returns the same `bool`).

**How the sync wrappers work.** UniTask has no blocking wait: `GetAwaiter().GetResult()` on an unfinished task throws «Not yet completed». The wrappers therefore switch on the synchronous mode `AssetSwapPause.Synchronous` for the duration of the call: the pause between steps becomes an already completed task, the async method runs to the end without returning to the frame loop, and the result is taken right away. If anything is still pending afterwards, the wrapper throws an `InvalidOperationException` instead of returning an unreliable result. The editor is not responsive during the call — fine for CI and build scripts.

`-executeMethod` calls only parameterless static methods, and the process exit code ignores a `bool` result. For the command line there are methods that throw on failure — Unity in batchmode then exits with code 1:

```
Unity.exe -batchmode -quit -projectPath <path> \
  -executeMethod Vortex.Unity.AssetSwapSystem.AssetSwapController.ApplyFromCommandLine \
  -assetSwap publisher:nutaku -assetSwap censorship:soft

Unity.exe -batchmode -quit -projectPath <path> \
  -executeMethod Vortex.Unity.AssetSwapSystem.AssetSwapController.ValidateAllFromCommandLine
```

The `-assetSwap` value (the key repeats, applied in order):

| Form | Example | What it does |
|---|---|---|
| `group:variant` by names | `publisher:nutaku` | Group — by the group's **Comment** field, variant — by the variant's Comment field in that group |
| `group:variant` by numbers | `1:2` | `AssetGroup1` → `Variant2` |
| Mixed | `1:nutaku`, `publisher:2` | A number is an index, anything else is a name |
| `variant` without a group | `nutaku` | The variant with this name in **all** groups (`ApplyByComment`); groups without it are skipped |

Names are compared case-insensitively, on the text trimmed at the edges. A number is always treated as an index — do not give groups and variants numeric names. Group names must be unique: on a clash the group is not selected (error). Group/variant not found, no key or an unparsable form — exception (code 1).

For complex hooks — a wrapper:

```csharp
public static class MyPublishHooks
{
    public static void BuildForGog()
    {
        if (!AssetSwapController.ApplyByComment("gog"))
            EditorApplication.Exit(1);
        // Assets swapped synchronously; a package that reacts to its asset with a delayCall
        // (e.g. SdkSettings defines) must be synced explicitly before the build.
        BuildPipeline.BuildPlayer(...);
    }
}
```

---

## Data model

### `AssetSwapSettings` (`ScriptableSingleton<T>`)

```csharp
[FilePath("ProjectSettings/VortexAssetSwapSettings.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class AssetSwapSettings : ScriptableSingleton<AssetSwapSettings>
{
    [SerializeField] private List<AssetSwapGroup> groups;
}
```

### `AssetSwapGroup` (POCO)

```csharp
[Serializable]
internal class AssetSwapGroup
{
    public int index;             // N — ordinal, unique in the list
    public int variantsCount;     // K registered variants
    public string comment;        // optional — "Steam", "GOG", "Censored"
}
```

The group name is **computed** as `$"AssetGroup{index}"`, not stored as a string — a single formula, no drift.

### Labels schema

| Role | Label |
|---|---|
| Target asset | `AssetGroup{N}` |
| Variant K of group N | `AssetGroup{N}_Variant{K}` |

### Invariants

- **I1.** `AssetSwapGroup.index` is unique. Duplicate → LogError, the duplicate is dropped (the first entry stays).
- **I2.** The whole package is Editor-only. Runtime never references it.
- **I3.** The `AssetGroup{N}` label exists **only** on assets the designer explicitly registered in group N.
- **I4.** Target's `.meta` is never mutated — only `File.Copy` over the contents.
- **I5.** The batch of applying one Variant K is atomic at the Unity import level (`StartAssetEditing`/`StopAssetEditing`). File-level atomicity (all `File.Copy` calls succeeded) is not guaranteed; on a partial failure — LogError, restore via git.
- **I6.** Identifying a target ↔ variant pair — exact match of `Path.GetFileName` including the extension, case-sensitive.

---

## Validator

The `Validate` (per-group) and `Validate All` buttons run:

| Check | What it looks for | Behavior |
|---|---|---|
| Name uniqueness | Files with the same `Path.GetFileName` inside targets and inside each Variant K | LogError per duplicate |
| Pair completeness | Every target has a pair in every Variant K | LogError for missing pairs |
| Reverse completeness | Every variant has a target | LogError for orphans |
| Importer type match | `AssetImporter.GetAtPath(target).GetType() == GetAtPath(variant).GetType()` | LogError on mismatch |
| Runtime holders | No runtime asset (prefab/scene/SO/mat/controller/anim outside `Editor/`) references a variant asset | LogError with holder + variant |

If any check fails, Apply is **blocked** until the issue is fixed.

---

## Limits (for the designer)

- **Package labels do not enter the inspector's predefined dropdown automatically.** Unity caches the known-labels list and does not rescan it after `SetLabels+SaveAssets` until an editor reload. Practical consequence: **assign or remove these labels only from `Project Settings → Vortex/AssetSwap`** (`Add label to Selected` / `Add label` buttons), not from the Unity inspector. The inspector accepts arbitrary strings silently — a typo like `AsetGroup1` would go unnoticed by the validator.
- **`.cs` files are never swapped.** `AddLabelToSelected` skips them in the selection with a `LogWarning`; `FindAssetsByLabel` does not expose them — Validate / Apply / Match Window will not see them even if a label lingered on a script from a manual inspector edit. Swapping code via `File.Copy` breaks `.meta`, Assembly Definitions and the compile phase; branch the codebase with git instead.
- **Exact file name match including extension.** `hero.png` ↔ `hero.png`, not `hero.jpg`, not `Hero.png`. Case matters.
- **Identical sub-asset structure of variants.** Sprite-multiple, FBX, prefab-with-embedded-mesh — if one variant has 5 sub-assets and another has 3, the `.meta` stays old and sub-asset references break. Rule: export variants **from the same source** (for Spine — from one skeleton).
- **Importer settings in `.meta` are not changed.** Sprite mode, compression, sRGB — come from the target. If variants require different settings, keep preset profiles and apply them separately.
- **Variant files must live in `Editor/` folders** — Unity automatically excludes them from the build. Direct references to variant files from runtime assets are forbidden; the validator checks this.
- **No "currently active" state.** The tool swept once; what's on disk is only known to git. The order of applying between groups is up to the designer.
- **File.Copy can fail** (file locked, permissions) — LogError; part of the batch is already applied. Rollback: `git checkout`.

---

## Edge cases

| Situation | Behavior |
|---|---|
| `AssetSwapSettings.groups` contains two `AssetGroup{N}` with the same `index` | On opening the page — LogError, duplicate is dropped (first entry stays) |
| The project has an Asset Label `AssetGroup5` from another system | Our targets are lumped together with the others — Validate catches the mismatch as "Duplicate file name" or "Type mismatch". LogError is enough |
| Sprites selected as targets, some of them are `Font` instead of `Texture2D` | Validate → LogError "Importer type mismatch" |
| A variant file is placed OUTSIDE an Editor folder | The validator checks runtime references; if the variant is used by something — LogError with holder path |
| `File.Copy` fails on 5 of 10 files | LogError on the 5th; the first 4 are already applied. Rollback via git |
| Apply on an empty Variant K (0 variant files) | Validate catches "Target has no pair in Variant K", Apply is blocked |
| `New Group` with no selection | The group is created empty; the label is not applied. Fine — the designer will add it later |
| Removing a group via `Remove Group` | The `AssetGroup{N}` label and all `AssetGroup{N}_Variant{K}` labels are stripped from every asset in the project. Target contents are unchanged |

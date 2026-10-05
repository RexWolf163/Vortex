# ContentTagsSystem

**Namespace:** `Vortex.Sdk.ContentTagsSystem.*`
**Assembly:** `ru.vortex.sdk.contenttags`
**Conditional compilation:** `defineConstraints: ["USING_VORTEX_CONTENT_TAGS"]`, toggle in `SdkSettings`

---

## Where it applies

The package covers one class of problems: the same game ships in several editions that differ not in content but in what is shown. That decision is made once, at build time, and afterwards changes neither by the player nor by the course of the game.

The typical example is the **main menu**. The Steam build has a "Rate the game" button and a block of social links; the publisher build must have neither — no store button, no external links. A "DLC" section belongs where DLC is sold and makes no sense in a demo. The menu itself is shared: the same prefabs, the same scenes, the same code.

Without the package this is done by editing prefabs per build: build for Steam — put the button back, build for the publisher — take it out. With the package the button is tagged `store_links` once, the tag is enabled in the `steam` bundle and not in `publisher_x`, and before the build a single field changes — the active edition.

Neighbouring problems have the same shape:

| Problem | Tag | Enabled in editions |
|---------|-----|---------------------|
| "Rate on Steam" button | `store_links` | `steam` |
| Social links block | `social_links` | `steam`, `gog` |
| DLC section in the menu | `dlc_section` | everywhere except `demo` |
| Call to buy the full version | `demo_upsell` | `demo` only |
| Debug menu entries | `dev_tools` | none of the release editions |

When the package does not fit:

- the content must be **physically absent** from the build — censorship, age ratings, regional bans: that is `AssetSwapSystem`, which swaps assets before the build. Here what is hidden still ships;
- visibility depends on **what the player did** — unlocked, viewed, bought: that is `RecordMarksSystem` and other progress systems;
- visibility depends on **ownership of paid content**: that is an ownership check on the platform's side, not build configuration;
- the switch belongs to the **player in the options** — then it is an ordinary game setting, not an edition.

---

## Purpose

Configuration of which interfaces and systems ship in a given edition of the build. One game goes out in several editions — the Steam storefront, the GOG storefront, a publisher build, a demo — and they differ in what is shown: a store button, a social links block, a DLC section, a menu item.

The mechanic is three things:

- **tag** — a string key declared in the settings asset;
- **bundle** — an edition: the whitelist of tags enabled in it;
- **active bundle** — a single field that defines the configuration of the current build.

The package components read the active set and show or hide tagged content. Switching editions means changing one value before the build, with no edits to scenes or prefabs.

Out of scope:

- **Physical absence of content.** What is hidden still ships in the build. For editions where content must not be present at all (censorship, age ratings) there is `AssetSwapSystem`, which swaps assets before the build. This package is soft configuration of presentation.
- **Cleaning player data.** Progress made in content disabled in this edition stays in the save and is simply not shown. Quests, inventory and gallery are not filtered.
- **Choosing the edition in the pipeline.** The value in the asset is set by whoever builds — by hand or by a script before the build.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `ru.vortex.extensions` | `SwitcherState` |
| `ru.vortex.unity.ui.misc` | `UIStateSwitcher`, `[StateSwitcher]` |
| `ru.vortex.unity.editortools` | `[ClassLabel]`, `[ToggleButton]` in the SDK toggle |
| Sirenix Odin Inspector | `[ValueDropdown]` for picking tags |
| `UnityEditor` (under `#if UNITY_EDITOR`) | dropdowns and the review window |

---

## Architecture

```
ContentBus (static)
  ├── IsActive(tag): bool                  ← is the tag enabled in the current edition
  ├── Matches(tags, mode): bool            ← match a list by mode
  ├── ActiveBundle: string
  ├── ActiveTags: IReadOnlyCollection<string>
  └── Build()                              ← lazy asset read on first access

ContentSettings (SO, Resources)
  ├── tags: List<ContentTag>               ← declared tags
  ├── bundles: List<ContentBundle>         ← editions
  ├── activeBundle: string                 ← edition of the current build
  └── Validate()                           ← empty and duplicate keys, undeclared tags

Handlers
  ├── ContentTagGateHandler                ← UIStateSwitcher: On / Off
  └── ContentTagObjectHandler              ← SetActive on a list of targets

Editor
  ├── ContentTagsIndex                     ← "tag → holders" index built in one pass
  └── ContentTagsWindow                    ← Tools/Vortex/Content Tags, read-only
```

### Order of work

1. On first access to the bus the asset is read from `Resources`.
2. The tags of the active bundle become the active set; the set lives until the end of the session.
3. In `OnEnable` a component matches its tag list against the active set and switches visibility.

There is no separate loading step, and there should not be: the read is synchronous, there is nothing to wait for and nothing to subscribe to. Unlike systems with an external check, no intermediate "do not know yet" state exists here.

### Match modes

| `TagMatchMode` | Shown when |
|---|---|
| `All` | every listed tag is active |
| `Any` | at least one is active |
| `None` | none is active |
| `Single` | exactly one of the listed tags is active |

An empty tag list on a component is a configuration error: Error and the content is hidden. An unfilled setting must not count as a satisfied condition — forgotten markup would then show the content in every edition.

### What "whitelist" means

A bundle lists what is enabled, but content **without tags is always visible**. The package governs only what is marked up: most of the game is shared across editions, and mandatory markup would cost more than the mistake it prevents. The consequence: a forgotten tag means the object ships to every edition. Reviewing markup before a build is the job of the `Content Tags` window.

---

## Critical requirements

1. **The `ContentSettings` asset lives in `Resources`.** Without it the active set is empty and everything marked up is hidden. With several assets the first one wins, the rest are ignored with an Error.
2. **A broken configuration hides tagged content instead of revealing it.** An empty or unknown active bundle yields an empty set. This is deliberate: the breakage is visible at once and nothing extra leaks into an edition.
3. **Two controlling components on one `UIStateSwitcher` are not allowed.** The one that runs last wins. No check prevents this — watch for it while building scenes.
4. **Never put the component's own object into `ContentTagObjectHandler` targets.** Having disabled itself, the component gets no `OnEnable` and never comes back. Such a target is skipped with an Error.
5. **The package is enabled by a define symbol.** Without the `USING_VORTEX_CONTENT_TAGS` toggle in `SdkSettings` the assembly does not compile.

---

## Contract

### Input

- `ContentSettings` in `Resources`: declared tags, bundles, the key of the active edition.

### Output

- `ContentBus.IsActive(tag)` — whether the tag is enabled;
- `ContentBus.Matches(tags, mode)` — result of matching a list;
- `ContentBus.ActiveBundle`, `ContentBus.ActiveTags` — the current configuration.

### Guarantees

- The API is synchronous: the very first call answers immediately, with no intermediate states.
- The active set is built once and does not change during the session.
- An undeclared tag counts as disabled.
- A broken configuration yields an empty set, not an arbitrary one.

### Limitations

| Limitation | Reason |
|------------|--------|
| Hidden content stays in the build | The package governs visibility; physical removal is `AssetSwapSystem` |
| The edition does not switch at runtime | It is build configuration, not a player setting |
| No edition override in the editor | What the asset says is what ships; to check, edit the field |
| Untagged content is always visible | Deliberate default-allow |
| Closed scenes are scanned as text | Opening every scene for a review is too expensive; the holder inside them is not identified |
| Scene scanning requires `Force Text` | With `Force Binary` the window reports an incomplete result |

---

## API

### `ContentBus` (static)

| Member | Description |
|--------|-------------|
| `IsActive(string tag)` | The tag is enabled in the current edition. Undeclared — `false` + Error once per key |
| `Matches(IReadOnlyList<string> tags, TagMatchMode mode)` | Match a set; an empty list is `false` in every mode |
| `ActiveBundle` | Key of the active edition; empty when the configuration is broken |
| `ActiveTags` | The active tag set |

### `ContentSettings` (SO, `Create → Vortex → Settings → Content Tags`)

| Field | Description |
|-------|-------------|
| `tags` | Declared tags: key plus a reference description |
| `bundles` | Editions: key plus the tag whitelist |
| `activeBundle` | Edition of the current build, a dropdown over bundles |

### `ContentTagGateHandler`

| Field | Description |
|-------|-------------|
| `tags` | Tag keys, dropdown from the asset |
| `mode` | `All` / `Any` / `None` / `Single` |
| `switcher` | `UIStateSwitcher` with `SwitcherState.Off` / `On` states |

### `ContentTagObjectHandler`

| Field | Description |
|-------|-------------|
| `tags`, `mode` | Same as above |
| `targets` | Objects the component drives; never its own object |
| `invert` | Show the targets when the condition is not met |

---

## Usage

### Project setup

1. Enable the package toggle in `Tools → Vortex → Configs → SDK Settings`.
2. `Create → Vortex → Settings → Content Tags`, put the asset into `Resources`.
3. Declare the tags: key plus a note on why the tag exists.
4. Create the edition bundles and tick the tags enabled in each.
5. Pick `activeBundle`.

### Marking up content

```csharp
// Any system can ask directly — not only UI.
if (ContentBus.IsActive("store_links"))
    BuildStoreSection();
```

On a scene: `ContentTagGateHandler` where the state is configured by a switcher; `ContentTagObjectHandler` where the section simply must not be there.

### Before building an edition

1. Set the required `activeBundle` in the asset.
2. Open `Tools → Vortex → Content Tags` and press **Обновить** (Refresh).
3. Check the edition contents and the "declared but unused" and "used but undeclared" sections — the latter means a typo in a component or a deleted declaration.
4. **Find** on a holder row: an open scene selects the object, a prefab opens in prefab mode with the object selected, a closed scene pings the asset.

---

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| `ContentSettings` not found in `Resources` | Error, the active set is empty, everything marked up is hidden |
| Several `ContentSettings` in `Resources` | Error, the first one is used |
| `activeBundle` is empty | Error, the set is empty |
| `activeBundle` points at a deleted bundle | Error, the set is empty |
| A bundle tag is not declared | Error during validation, it never enters the set |
| Duplicate tag or bundle key | Error with the index |
| Request with an undeclared tag | `false` + Error once per key |
| Empty tag list on a component | Error, the content is hidden |
| Empty target list on `ContentTagObjectHandler` | Error, nothing is switched |
| The component's own object among the targets | Error, the target is skipped |
| Re-entering Play Mode without Domain Reload | The bus state resets and the configuration is read again |
| `Asset Serialization` is not `Force Text` | The window warns that closed scenes were not scanned |
| Scanning cancelled | The previous index is kept |

---

## File structure

```
ContentTagsSystem/
├── Bus/             # ContentBus — public entry point
├── Model/           # ContentTag, ContentBundle, TagMatchMode, ContentTagsCatalog
├── Settings/        # ContentSettings
├── Handlers/        # ContentTagGateHandler, ContentTagObjectHandler
├── DefineSettings/  # package toggle in SdkSettings
└── Editor/          # ContentTagsIndex, ContentTagsWindow
```

# UIBuilder

**Namespace:** `Vortex.Unity.UI.UIBuilder`
**Assembly:** `ru.vortex.unity.ui.uibuilder` (Editor only)

## Purpose

An editor layout tool: in one action it creates a ready UI layer from a primitive prefab under the selected object — a `UIComponent` container, the primitive instance inside, collected parts, and the required `Set*Component`s with their links.

Features:
- A primitive catalog per folder (including subfolders), picked from a dropdown grouped by subfolder
- Hierarchy context menu: `Vortex Primitives/Create Text`, `Vortex Primitives/Create Button`
- Window parameters depend on the selected primitive: a section is shown only if the primitive has the matching part
- Module settings on the `Project Settings → Vortex/UIBuilder` page; the file lives outside `Assets` and never ships in a build
- One Undo entry for the whole creation; a build error rolls back what was created
- New element kinds and sections can be added without touching the core

Out of scope:
- The primitives themselves and their structure (project prefabs)
- Runtime behaviour of `UIComponent` and `Set*Component` (`UIComponents`, `Components`)

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `ru.vortex.unity.ui.misc` | `UIComponent` and its parts (`UIComponentText/Graphic/Button`) |
| `ui.vortex.unity.components` | `SetTextComponent`, `SetSpriteComponent`, `SetActionComponent` |
| `ru.vortex.unity.localization` | `[LocalizationKey]` — key selector in the locale section |
| `ru.vortex.unity.editortools` | `SearchablePopup` — primitive dropdown |
| Odin Inspector | Drawing settings and sections (`PropertyTree`, `[FolderPath]`, `[ValueDropdown]`) |

---

## Architecture

```
UIBuilder/
├── UIBuilderController.cs          # Core: module discovery, menu item → window, layer build
├── UIBuilderSettings.cs            # ScriptableSingleton in ProjectSettings/, sync with modules
├── UIBuilderSettingsProvider.cs    # Project Settings → Vortex/UIBuilder page
├── UIBuilderCreateWindow.cs        # Creation parameters window
├── UIBuilderCatalog.cs             # Prefab catalog of a folder (with subfolders)
├── Base/
│   ├── UIBuilderModule.cs          # Element kind module (+ generic UIBuilderModule<TSettings>)
│   ├── UIBuilderModuleSettings.cs  # Common module settings
│   └── UIBuilderSection.cs         # Creation parameters section
├── Modules/
│   ├── TextModule.cs               # Text + TextModuleSettings
│   └── ButtonModule.cs             # Button + ButtonModuleSettings
└── Sections/
    ├── LocaleSection.cs            # UIComponentText → SetTextComponent
    ├── IconSection.cs              # UIComponentGraphic → SetSpriteComponent
    └── ActionSection.cs            # UIComponentButton → SetActionComponent
```

### Module (`UIBuilderModule`)

An element kind: its own settings (`SettingsType`), title (`Title`), section set (`CreateSections()`) and build step (`Apply`). Stateless; requires a parameterless constructor. The core discovers non-abstract subclasses via `TypeCache`. A module declares its own menu item — a static `[MenuItem]` that calls `UIBuilderController.Open<TModule>`.

| Module | Menu | Sections | Default layer name |
|--------|------|----------|--------------------|
| `TextModule` | `Vortex Primitives/Create Text` | Locale | `Text` |
| `ButtonModule` | `Vortex Primitives/Create Button` | Locale, Icon, Action | `Button` |

### Settings (`UIBuilderSettings`, `UIBuilderModuleSettings`)

`UIBuilderSettings` is a `ScriptableSingleton` stored in `ProjectSettings/VortexUIBuilderSettings.asset`: outside `Assets`, never in a build, kept in VCS. It holds one `UIBuilderModuleSettings` per module (`[SerializeReference]`).

Common module settings fields:

| Field | Type | Description |
|-------|------|-------------|
| `folder` | `string` (`[FolderPath]`) | Primitive folder, scanned with subfolders. Empty — module not configured |
| `defaultPrefab` | `GameObject` (`[ValueDropdown]`) | Primitive preselected when the window opens. Picked from the folder catalog |
| `defaultLayerName` | `string` | Name of the created layer. The subclass sets the default via its constructor |
| `defaultSize` | `Vector2` | Size of the created layer (`sizeDelta`), `240 × 80` by default |

**Sync** (`Sync`) runs when the settings page opens and when settings of a missing module are requested:
- adds settings for modules that have none;
- removes entries whose class is not found, settings of modules that no longer exist, and duplicates (the first one is kept);
- every removal is logged, changes are saved to disk.

### Section (`UIBuilderSection`)

A shared piece of creation parameters bound to a `UIComponent` part type (`PartType`). Section fields (`[SerializeField]`) are drawn by Odin. Instances are recreated on every primitive selection and are not saved.

| Section | Part | Fields | Adds |
|---------|------|--------|------|
| `LocaleSection` | `UIComponentText` | `localeKey` (`[LocalizationKey]`), `useLocalization = false` | `SetTextComponent` with the key, if the key is set |
| `IconSection` | `UIComponentGraphic` | `icon` | `SetSpriteComponent` with the sprite, if the sprite is set |
| `ActionSection` | `UIComponentButton` | `addAction = true` | `SetActionComponent`, if checked |

`AddLinked<T>` adds the component and explicitly sets `uiComponent` and `position = -1` (all parts of the type); component-specific fields are written through `SerializedObject`.

### Layer build

```
<active selected object>
└── <layer name>        RectTransform (sizeDelta = defaultSize), UIComponent, Set*Component
    └── <primitive instance>   prefab link kept, unchanged
```

1. Undo group.
2. The layer is created **inactive** — `SetTextComponent` (`[ExecuteInEditMode]`) must not enable before its `UIComponent` link is set.
3. Primitive instance via `PrefabUtility.InstantiatePrefab`.
4. Parts are collected by the private editor method `UIComponent.Init` via reflection (the `UIComponents` package is not modified).
5. `module.Apply`: sections that have parts of their type after collection.
6. The layer is activated, the Undo group collapsed, the created layer selected.

---

## Usage

1. `Project Settings → Vortex/UIBuilder` — set the primitive folder for each module, optionally the default prefab, name and size.
2. Right-click an object in the Hierarchy → `Vortex Primitives/Create Text` or `Create Button`.
3. In the window pick a primitive, the layer name, fill in the sections → «Создать» (Create).

## Extension

A new element kind is three classes in any editor assembly referencing `ru.vortex.unity.ui.uibuilder`:

```csharp
[Serializable]
public sealed class ImageModuleSettings : UIBuilderModuleSettings
{
    public ImageModuleSettings() : base("Image") { }
}

public sealed class ImageModule : UIBuilderModule<ImageModuleSettings>
{
    private const string MenuPath = "GameObject/Vortex Primitives/Create Image";

    public override string Title => "Image";

    public override IEnumerable<UIBuilderSection> CreateSections()
    {
        yield return new IconSection();
    }

    [MenuItem(MenuPath, true)]
    private static bool OpenValidate() => UIBuilderController.CanOpen();

    [MenuItem(MenuPath, false, 3)]
    private static void Open(MenuCommand command) => UIBuilderController.Open<ImageModule>(command);
}
```

A custom section is a `UIBuilderSection` subclass with `PartType`, `Title` and `Apply`; the module lists it in `CreateSections()`. For a non-standard build, override `UIBuilderModule.Apply`.

---

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| `folder` is empty | The menu item opens the `Vortex/UIBuilder` page, warning in the log |
| `folder` does not exist | Window: "folder not found", creation disabled; `defaultPrefab` — empty list |
| Folder has no prefabs | Window: "no prefabs in folder", creation disabled |
| `defaultPrefab` empty or outside the catalog | No primitive selected, Create disabled until one is picked |
| No active selected object | Menu item disabled |
| Several objects selected | Created only under the active one; repeated context-menu calls are skipped |
| Parent without `RectTransform` | Layer is created, warning in the log |
| Section shown, but parts belong to a nested `UIComponent` of the primitive | Section skipped, warning in the log |
| `UIComponent.Init` not found (renamed) | `MissingMethodException` logged, creation rolled back |
| A `Set*Component` field not found | `InvalidOperationException` naming the field, creation rolled back |
| Domain reload with the window open | The window closes |
| Module removed from code | Its settings are removed on the next sync, logged |
| Field added to settings after the file was saved | May load as the type default (e.g. `defaultSize = 0 × 0`) — set it manually |

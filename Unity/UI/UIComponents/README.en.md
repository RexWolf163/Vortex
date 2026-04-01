# UIComponents

**Namespace:** `Vortex.Unity.UI.UIComponents`
**Assembly:** `ru.vortex.unity.ui.misc`

## Purpose

Modular UI component system. `UIComponent` (MonoBehaviour) manages arrays of typed `UIComponentPart`, providing a unified API for working with texts, buttons, graphics, and states.

Capabilities:
- Bulk and targeted UI updates via `PutData()` / `SetText()` / `SetSprite()` / `SetAction()` / `SetSwitcher()`
- Support for Text, TextMeshPro, TextMeshProUGUI, Button, AdvancedButton, SpriteRenderer, Image, UIStateSwitcher
- Position-based part addressing for multi-part components
- `[UIComponentLink]` attribute for type-safe position selection in Inspector
- Optional text localization (enabled by default)

Out of scope:
- Data display logic (layer 3/4)
- Interface lifecycle management (`UIProviderSystem`)

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Unity.UI.StateSwitcher` | `UIStateSwitcher` — integration via `UIComponentSwitcher` |
| `Vortex.Unity.EditorTools` | `[AutoLink]` — component auto-binding |
| `Vortex.Core.LocalizationSystem` | `StringExt.Translate()` — text localization |
| Odin Inspector | `[ShowInInspector]`, `[Button]`, `[TitleGroup]` |
| TextMeshPro | TMP components |

---

## Architecture

```
UIComponents/
├── UIComponent.cs                  # Central orchestrator (partial)
├── UIComponentExtEditor.cs         # Editor: Init(), Test(), GetLinks()
├── UIComponentData.cs              # Data struct for PutData()
├── Parts/
│   ├── UIComponentPart.cs          # Abstract part base
│   ├── UIComponentText.cs          # Text, TMP, TMPUGUI
│   ├── UIComponentButton.cs        # Button, AdvancedButton
│   ├── UIComponentGraphic.cs       # SpriteRenderer, Image (+ Texture2D→Sprite)
│   └── UIComponentSwitcher.cs      # UIStateSwitcher
├── Attributes/
│   └── UIComponentLinkAttribute.cs # Type-safe part position
└── Editor/
    └── UIComponentLinkAttributeDrawer.cs  # Slider + validation
```

### UIComponent

Partial MonoBehaviour. Stores four part arrays:

| Array | Part Type | Purpose |
|-------|----------|---------|
| `uiComponentTexts[]` | `UIComponentText` | Text elements |
| `uiComponentButtons[]` | `UIComponentButton` | Buttons |
| `uiComponentGraphics[]` | `UIComponentGraphic` | Graphics |
| `uiComponentSwitchers[]` | `UIComponentSwitcher` | States |

### Init() — part auto-discovery

The `Init` button in Inspector runs recursive `GetComponentsInChildren` for all four part types. When nested `UIComponent` containers exist in the hierarchy — first recursively calls `Init()` on each child `UIComponent`, then **excludes** all parts already owned by child containers. Each part belongs to exactly one `UIComponent`.

After collecting parts, `Init()` populates `_testData` with current values (texts, sprites, switcher states) for debugging via the `Test` button.

### UIComponentPart (abstract)

Base class for all parts. In Editor, automatically fills RectTransform to container size (unless `onlyNativeSize` is checked).

### Implementations

**UIComponentText** — supports Text (legacy), TextMeshPro, TextMeshProUGUI. Auto-discovers components in Editor (`[OnInspectorInit]`).

**UIComponentButton** — supports Button and AdvancedButton. Tracks `_currentAction`, removes old listener before setting new. Cleanup in `OnDestroy`.

**UIComponentGraphic** — supports SpriteRenderer and Image. Accepts both `Sprite` and `Texture2D` (auto-converts to Sprite). Editor-time type validation: only `SpriteRenderer` and `Image` are accepted.

**UIComponentSwitcher** — bridge to `UIStateSwitcher`. Accepts `int` or `Enum` for state switching.

---

## API

```csharp
// Bulk data application
component.PutData(new UIComponentData {
    texts = new[] { "Title", "Subtitle" },
    sprites = new[] { icon },
    actions = new[] { OnClick }
});

// Direct access
component.SetText("Title");
component.SetText("Subtitle", 1);       // by position
component.SetSprite(icon);
component.SetSprite(texture2D);          // auto-conversion
component.SetAction(OnClick);
component.SetAction(OnClick, 2);         // by position
component.SetSwitcher(SwitcherState.On);
```

### UIComponentData (struct)

| Field | Type | Description |
|-------|------|-------------|
| `texts` | `string[]` | Texts for each `UIComponentText` |
| `actions` | `UnityAction[]` | Callbacks for each `UIComponentButton` |
| `sprites` | `Sprite[]` | Sprites for each `UIComponentGraphic` |
| `enumValues` | `int[]` | States for each `UIComponentSwitcher` |

### UIComponentLinkAttribute

Type-safe part position selection in Inspector. Renders a slider 0..N with target part name:

```csharp
[SerializeField, UIComponentLink(typeof(UIComponentText), "uiComponent")]
private int position = -1;  // -1 = default, 0..N = specific part
```

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `SetText("x", pos)` without `UIComponentText` or out of range | `Debug.LogError`, return |
| `SetText("x")` when `uiComponentTexts == null` | `NullReferenceException` |
| `SetAction(null)` | Current listener removed |
| `PutData()` with array shorter than part count | Texts/buttons/graphics: missing entries are nullified (empty string / null / null). Switchers: processing breaks early |
| `SetSprite(Texture2D)` | `Sprite` created via `Sprite.Create()` |
| `position` out of range | `Debug.LogError` + return (positional methods), `IndexOutOfRangeException` (direct array access) |
| `useLocalization = true` (default) | Texts passed through `StringExt.Translate()` |

### UIComponentLinkAttribute

| Situation | Behavior |
|-----------|----------|
| `position = -1` | Drawer shows warning "applies to all components" |
| `position` out of range | Drawer shows error |
| `position` in range | Drawer shows disabled ObjectField with target GameObject |

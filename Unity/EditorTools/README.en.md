# EditorTools

Attributes and Odin drawers for Unity Inspector customization in Vortex projects. Built entirely on top of Sirenix Odin Inspector — the native `PropertyDrawer` / MultiDrawer pipeline is not used.

## Purpose

- Declarative inspector customization via attributes on serialized fields.
- Extending Odin with Vortex-specific constructs (type filters, auto-linking, value-popup selectors, etc.).
- Shared infrastructure (theme, reflection, collection rendering) reused across other Vortex editor packages.

## Architecture

### Assemblies

| Assembly | Contents | Constraints |
|----------|----------|-------------|
| `ru.vortex.unity.editortools` | Attributes (runtime) + editor utilities (`Elements/`, `EditorSettings/`, `InspectorHandler`) | — |
| `ru.vortex.unity.editortools.sirenix` | Odin drawers (`SirenixOdinDrawers/`) | `defineConstraints: ["ODIN_INSPECTOR"]` |

### Folder layout

```
EditorTools/
├── Attributes/                  # Runtime attributes
├── SirenixOdinDrawers/          # Odin drawers (Editor-only)
├── DataModelSystem/             # [DataModel] — runtime-object expansion
├── Elements/                    # DrawingUtility, SearchablePopup
├── EditorSettings/              # ToolsSettings, ThemeColors, DefaultColors
└── InspectorHandler.cs          # SerializedProperty utilities (IsPropertyNullable, GetPropertyValue)
```

### Conditional compilation

- Attributes — no guards, available at runtime (inherit from `PropertyAttribute` / `Attribute`).
- Drawers (`SirenixOdinDrawers/`) — `#if UNITY_EDITOR` + the asmdef-level `ODIN_INSPECTOR` define constraint.
- Editor utilities (`Elements/`, `InspectorHandler`, `EditorSettings/`) — `#if UNITY_EDITOR`.
- `PropertyFoldoutGroupAttribute` — derives from Odin's `FoldoutGroupAttribute` under `#if ODIN_INSPECTOR`, falls back to `Attribute` otherwise.

## Attributes

### `[AutoLink]`

Auto-links a `UnityEngine.Object` field when null. The drawer takes `Property.SerializationRoot` (typically a MonoBehaviour) and calls `GetComponent(fieldType)` on its GameObject. Honors `[ClassFilter]` if it is also applied to the same field.

```csharp
[SerializeField, AutoLink] private Animator animator;
[SerializeField, AutoLink, ClassFilter(typeof(IInteractable))]
private MonoBehaviour interactable;
```

### `[ClassFilter(params Type[] requiredTypes)]`

Validates the value of a `UnityEngine.Object` field against a list of types (classes or interfaces). If the assigned object is not assignable to any `RequiredTypes`, the field is cleared with a console warning.

```csharp
[SerializeField, ClassFilter(typeof(IDamageable), typeof(IHealable))]
private MonoBehaviour target;
```

### `[ClassLabel(string groupName = "$ToString")]`

Odin processor: wraps all members of a class/struct in a `FoldoutGroup` with header `groupName`. Supports `$Method` for dynamic values.

```csharp
[Serializable, ClassLabel("$GetTitle")]
public class WeaponSlot
{
    public string id;
    public int damage;
    private string GetTitle() => $"{id} ({damage} dmg)";
}
```

### `[ToggleButton(string labelsMethod = null, string colorsMethod = null, bool isSingleButton = false)]`

Replaces the field with a horizontal toggle-button strip. Supported types: `bool`, `int`, `byte`, `enum`.

- `labelsMethod` — method returning `Dictionary<int, string>` (key = field value, value = button text).
- `colorsMethod` — method returning `Dictionary<int, Color>`.
- `isSingleButton` — renders a single button; clicking cycles through values.

Defaults:
- `bool` without `labelsMethod` — `Off` / `On` buttons with `SwitcherOffBg` / `SwitcherOnBg` colors.
- `enum` without `labelsMethod` — buttons by enum value names.
- `int` / `byte` without `labelsMethod` — error in inspector.

```csharp
[SerializeField, ToggleButton] private bool isActive;
[SerializeField, ToggleButton(nameof(GetLabels))] private int mode;

private Dictionary<int, string> GetLabels() => new()
{
    { 0, "Idle" }, { 1, "Run" }, { 2, "Attack" },
};
```

### `[ValueSelector(string methodName, Placeholder = "...")]`

Replaces the field with a `SearchablePopup` populated from a method. Supported return types:

- `string[]` / `List<string>` / `IEnumerable<string>` — key = value, written to a string field.
- `Dictionary<string, TValue>` — keys appear in the popup; selecting one writes the corresponding `TValue` into the field.

The method may be instance / static, public / private, parameterless.

```csharp
[SerializeField, ValueSelector("GetTags")] private string tag;
private string[] GetTags() => new[] { "Player", "Enemy", "NPC" };

[SerializeField, ValueSelector("GetTypes", Placeholder = "— Pick Type —")]
private string typeName;
private Dictionary<string, string> GetTypes() => /* FullName → AssemblyQualifiedName */ ;
```

### `[DateTimeDraw]` / `[TimeDraw]`

Renders a `long` (Unix timestamp / ticks) as an editable date or time.

| Attribute | Format | Use |
|-----------|--------|-----|
| `[DateTimeDraw]` | `dd.MM.yyyy HH:mm:ss` | Full date + time |
| `[TimeDraw]` | `hh:mm:ss` | Time only |

### `[TimerDraw]` / `[DateTimerDraw]`

Read-only variants for timer-like values.

| Attribute | Use |
|-----------|-----|
| `[TimerDraw]` | Read-only timer (duration) |
| `[DateTimerDraw]` | Read-only date (e.g. trigger moment) |

### `[PropertyFoldoutGroup(string groupName, ...)]`

Extension of Odin's `FoldoutGroupAttribute`: a foldout whose header renders one of the group's fields. The header field is selected by `PropertyName` (defaults to `groupName`); `Title` overrides the displayed title.

```csharp
[PropertyFoldoutGroup("settings"), SerializeField] private string settings;
[PropertyFoldoutGroup("settings"), SerializeField] private float volume;
[PropertyFoldoutGroup("settings"), SerializeField] private bool mute;
```

### `[DataModel]` + `[DataModelMethod]`

Expands a runtime object in the inspector: public properties (including inherited) are shown in a foldout group; properties with setters are editable via reflection. Methods marked with `[DataModelMethod]` are rendered as buttons.

```csharp
[SerializeField, DataModel] private PlayerStateModel state;

public class PlayerStateModel
{
    public int Hp { get; set; }
    public string Name { get; private set; }

    [DataModelMethod("Reset")]
    public void Reset() => Hp = 100;
}
```

## Odin drawers

| Drawer | Attribute | Base class |
|--------|-----------|------------|
| `AutoLinkAttributeDrawer` | `[AutoLink]` | `OdinAttributeDrawer<AutoLinkAttribute>` |
| `ClassFilterAttributeDrawer` | `[ClassFilter]` | `OdinAttributeDrawer<ClassFilterAttribute>` |
| `ToggleButtonAttributeDrawer` | `[ToggleButton]` | `OdinAttributeDrawer<ToggleButtonAttribute>` |
| `ValueSelectorAttributeDrawer` | `[ValueSelector]` | `OdinAttributeDrawer<ValueSelectorAttribute>` |
| `DateTimeAttributeDrawer` | `[DateTimeDraw]` | `OdinAttributeDrawer<DateTimeDrawAttribute, long>` |
| `TimeAttributeDrawer` | `[TimeDraw]` | `OdinAttributeDrawer<TimeDrawAttribute, long>` |
| `TimerAttributeDrawer` | `[TimerDraw]` | `OdinAttributeDrawer<TimerDrawAttribute, long>` |
| `DateTimerAttributeDrawer` | `[DateTimerDraw]` | `OdinAttributeDrawer<DateTimerDrawAttribute, long>` |
| `PropertyFoldoutGroupAttributeDrawer` | `[PropertyFoldoutGroup]` | `OdinGroupDrawer<PropertyFoldoutGroupAttribute>` |
| `ClassLabelAttributeProcessor` | `[ClassLabel]` | `OdinAttributeProcessor` |
| `DataModelDrawer` | `[DataModel]` | `OdinAttributeDrawer<DataModelAttribute>` |

### Conventions and key Odin idioms

- `OdinAttributeDrawer<TAttr>` without `TValue` — for attributes applicable to fields of any type. Value access: `Property.ValueEntry.WeakSmartValue`.
- `OdinAttributeDrawer<TAttr, TValue>` — when the value type is known (`long`, `string`, `UIStateSwitcher`, etc.). Access via `ValueEntry.SmartValue`.
- `ValueResolver<T>` / `ValueResolver.GetForString` — resolves literals, methods, properties, fields.
- `Property.Info.GetMemberInfo() as FieldInfo` — pulls the field's `FieldInfo`.
- `Property.Info.TypeOfValue` — declared field type.
- `Property.SerializationRoot.ValueEntry.WeakSmartValue` — root object (MonoBehaviour / SO).
- `Property.Tree.UnitySerializedObject?.FindProperty(Property.UnityPropertyPath)` — bridge to Unity's `SerializedProperty`, required for drawers that mutate values via `serializedObject.ApplyModifiedProperties`.
- `CallNextDrawer(label)` — chain to the next drawer.
- Messaging: `SirenixEditorGUI.ErrorMessageBox` / `InfoMessageBox`.

## Utilities

### `Elements/DrawingUtility`

GUI primitives over `EditorGUI`:
- `DrawSelector(Rect, SerializedProperty, keys, values, currentIndex, placeholder)` — popup selector backed by `SearchablePopupWindow`
- `MakeInfoBox(Rect, text, hasError, icon)` / `CalcInfoBoxHeight(text, width)` — info/error blocks with rich-text
- `DrawBoxBorder(Rect, color, c2, raise, ...)` — 1px frame

### `Elements/SearchablePopup` / `SearchablePopupWindow`

Popup with built-in search and `/`-based grouping. Used by `ValueSelectorAttributeDrawer` and `DrawSelector`.

### `InspectorHandler`

`SerializedProperty` helpers: `IsPropertyNullable(property)` (true for String/ObjectReference), `GetPropertyValue(property)` (boxed primitive value).

### `EditorSettings/ToolsSettings`

`ScriptableObject` with two `ThemeColors` (light / pro). `ThemeColors` holds a `Dictionary<DefaultColors, Color>` — a centralized palette for drawers. Access:

```csharp
ToolsSettings.GetBgColor(DefaultColors.SwitcherOnBg);
ToolsSettings.GetLineColor(DefaultColors.TextColor);
```

## Dependencies

- Odin Inspector (Sirenix) — required for all drawers in `SirenixOdinDrawers/`.
- Unity 2021.3+ (tested on Unity 2022.3 LTS).

## Edge cases

- `[ClassFilter]` on a field whose type is not a `UnityEngine.Object` — the drawer shows an ErrorMessageBox and skips the value untouched.
- `[AutoLink]` without a MonoBehaviour `SerializationRoot` (e.g. on a ScriptableObject) — linking does not run; the drawer silently passes through.
- `[ToggleButton]` on `int` / `byte` without `labelsMethod` — ErrorMessageBox.
- `[ValueSelector]` returning `null` / an empty collection — ErrorMessageBox below the field; the field remains editable through the default drawer.
- `[DateTimeDraw]` and similar on non-`long` fields — Odin does not activate the drawer (TValue mismatch).

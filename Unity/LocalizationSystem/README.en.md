# LocalizationSystem (Unity)

**Namespace:** `Vortex.Unity.LocalizationSystem`, `Vortex.Unity.Components.Misc.LocalizationSystem`
**Assemblies:** `ru.vortex.unity.localization`, `ui.vortex.unity.components`

---

## Purpose

Unity driver implementation for the localization system and components for binding translations to UI.

Capabilities:

- Translation loading from Google Sheets (TSV format)
- Data storage in ScriptableObject (`LocalizationPreset`)
- Selected language persistence via `PlayerPrefs`
- Async index loading via `IProcess` / `Loader`
- Inspector attributes: localization key picker, language picker
- Components: text binding, sprite binding, action binding, language switching

Out of scope:

- Translation lookup logic (Core `Localization`)
- Extension methods `Translate()` / `TryTranslate()` (Core `StringExt`)

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.LocalizationSystem` | `Localization`, `IDriver`, `StringExt` |
| `Vortex.Core.LoaderSystem` | `Loader.Register()`, `Loader.RunAlone()` |
| `Vortex.Core.AppSystem` | `App.OnStart`, `AppStates` |
| `Vortex.Unity.UI` | `UIComponent`, `UIStateSwitcher` |
| `Vortex.Unity.EditorTools` | `MultiDrawer`, `AutoLink`, `DrawingUtility` |
| `Cysharp.Threading.Tasks` | `UniTask` |

---

## Architecture

### Driver

```
LocalizationDriver (Singleton<T>, IDriver, IProcess)
  ├── partial: main       → Init, SetLanguage, GetLanguages, GetDefaultLanguage
  ├── partial: Loading    → Register, RunAsync, WaitingFor (IProcess)
  └── partial: Editor     → EditorRegister, LoadLocalizationData, RefreshIndex
```

- `Singleton<LocalizationDriver>` — single instance per application
- Implements `IDriver` (Core contract) and `IProcess` (loading via Loader)
- `WaitingFor() → null` — no dependencies, loads early

### Data

```
LocalizationPreset (ScriptableObject)
  ├── localeDoc: string           ← Google Sheets document ID
  ├── sheets: string[]            ← Sheet GIDs
  ├── langs: string[]             ← registered languages (HideInInspector)
  └── localeData: LocalePreset[]  ← translation array (HideInInspector)

LocalePreset (struct)
  ├── Key: string
  └── Texts: LanguageData[]

LanguageData (struct)
  ├── Language: string
  └── Text: string
```

### Google Sheets format

```
KEY         | English      | Russian      | ...
MENU_START  | Start        | Начать       |
MENU_EXIT   | Exit         | Выход        |
```

- First row — header: column 0 = key, columns 1..N = languages
- Keys converted to `UPPER` on load
- Export format: TSV (`export?format=tsv&gid=...`)
- Multiple sheets supported

### Loading (runtime)

1. `RuntimeInitializeOnLoadMethod` → `Register()` → loads `LocalizationPreset` from Resources
2. `Loader.Register(Instance)` → queued for loading
3. `RunAsync()` → iterates `localeData`, fills index for `GetCurrentLanguage()`
4. `UniTask.Yield()` every 20 entries — main thread relief
5. `TimeController.Call(CallOnInit)` → ready notification

### Language change (runtime)

1. `Localization.SetCurrentLanguage(lang)` → `Driver.SetLanguage(lang)`
2. Driver saves to `PlayerPrefs("AppLanguage")`
3. `Loader.RunAlone(this)` → index reload
4. `OnLocalizationChanged` → UI updates

---

## Contract

### Presets

| Field | Description |
|-------|-------------|
| `localeDoc` | Google Sheets document ID |
| `sheets` | Sheet GIDs for loading |
| `langs` | Auto-filled — languages from TSV headers |
| `localeData` | Auto-filled — `LocalePreset` array |

### Attributes

| Attribute | Field type | Description |
|-----------|------------|-------------|
| `[LocalizationKey]` | `string` | Key dropdown from index + translation preview. Odin drawer |
| `[Language]` | `string` | Language selector from `GetLanguages()`. MultiDrawer |

### Guarantees

- Languages validated against `SystemLanguage` enum
- `GetDefaultLanguage()`: `PlayerPrefs` → `Application.systemLanguage` → first in list
- Index fully reloaded on language change

### Limitations

| Limitation | Reason |
|------------|--------|
| `LocalizationPreset` in `Resources/Localization/` | Loaded via `Resources.LoadAll` |
| `SetLanguage` is `async void` | Fire-and-forget on language change |
| Languages bound to `SystemLanguage` | Validation via `Enum.TryParse` |
| Keys are `ToUpper()` | Case-insensitive lookup |

---

## Components

### SetTextComponent

Binds localized text to a `UIComponent`.

| Field | Description |
|-------|-------------|
| `key` | Localization key (`[LocalizationKey]` — dropdown) |
| `useLocalization` | `true` — `key.Translate()`, `false` — key as-is |
| `uiComponent` | Target `UIComponent` (`[AutoLink]`) |
| `position` | `UIComponentText` index (-1 = first) |

Subscribes to `OnLocalizationChanged`, `App.OnStart`, `Localization.OnInit`. Works in `ExecuteInEditMode`.

### SetSpriteComponent

Binds a sprite to a `UIComponent`.

| Field | Description |
|-------|-------------|
| `sprite` | `Sprite` to set |
| `uiComponent` | Target `UIComponent` |
| `position` | `UIComponentGraphic` index (-1 = first) |

### SetActionComponent

Binds a `UnityEvent` to a `UIComponent` button.

| Field | Description |
|-------|-------------|
| `events` | `UnityEvent` — invoked on click |
| `uiComponent` | Target `UIComponent` (`[AutoLink]`) |
| `position` | `UIComponentButton` index (-1 = first) |

Clears action (`SetAction(null)`) on `OnDisable`.

### SetLocaleHandler

Language switch button.

| Field | Description |
|-------|-------------|
| `uiComponent` | `UIComponent` — button + text |
| `language` | Target language (`[Language]` — selector) |
| `useSwitch` | Show `SwitcherState.On/Off` for current language |

`Run()` → `Localization.SetCurrentLanguage(language)`. Button text — translated language name.

---

## Editor tools

### Menu

| Item | Description |
|------|-------------|
| `Vortex/Localization/Load data` | Load data from Google Sheets into `LocalizationPreset` |
| `Vortex/Localization/Update index` | Rebuild index from current preset |
| `Vortex/Localization/Set Default Locale` | Reset to system/saved language |
| `Vortex/Localization/Set Next Locale` | Cycle to next language |

### LocalizationPreset Inspector

- `Load Data` button — loads from Google Sheets
- `Debug` dropdown — preview translation by key
- InfoBox — list of registered languages
- `Check System Language` button — logs current `Application.systemLanguage`

### Asset auto-creation

`EditorRegister` (InitializeOnLoadMethod) automatically creates `Resources/Localization/LocalizationData.asset` if missing.

---

## Usage

### Setup

1. Create Google Sheets with translations (format: KEY | English | Russian | ...)
2. Enter document ID and sheet GIDs into `LocalizationPreset`
3. Click `Load Data` in Inspector
4. Ensure `LocalizationPreset` is in `Resources/Localization/`

### Bind text to UI

1. Add `UIComponent` to a GameObject
2. Add `SetTextComponent` to the same GameObject
3. Select key via `[LocalizationKey]` dropdown
4. Enable `useLocalization` for translation

### Language switch button

1. Add `UIComponent` (with button and text)
2. Add `SetLocaleHandler`
3. Select language via `[Language]` selector
4. Enable `useSwitch` for visual highlight of current language

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| Google Sheets unavailable during `Load Data` | `LogError`, sheet skipped |
| Language from TSV not found in `SystemLanguage` | `LogError`, language skipped |
| `PlayerPrefs` contains language not in list | Fallback to first language in list |
| `LocalizationPreset` not found in Resources | `LogError`, driver not registered |
| Duplicate key across sheets | `AddNew` logs warning |
| `SetTextComponent` without `UIComponent` | `LogError` in `OnEnable` |
| `SetLanguage` during loading | `Loader.RunAlone` triggers index reload |

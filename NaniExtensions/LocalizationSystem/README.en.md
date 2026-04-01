# LocalizationSystem (NaniExtensions)

Localization driver for Naninovel-based projects.

## Purpose

Replaces the standard `LocalizationDriver` (Unity) with a driver integrated with Naninovel. Extends the localization system with language channels and bridges Vortex localization with Naninovel `ILocalizationManager`.

- Translation loading from Naninovel file structure
- Three localization channels: UI, Dialogue, Voice — with independent language selection
- Language synchronization with Naninovel (`ILocalizationManager`, `VoiceLoader`)
- Per-channel language preference persistence via `PlayerPrefs`
- UI component for language selection with channel filtering

Out of scope: Naninovel localization file format (see Naninovel documentation), translation lookup logic (Core `Localization`).

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.LocalizationSystem` | `Localization`, `IDriver`, `StringExt` |
| `Vortex.Core.LoaderSystem` | `Loader.Register()`, `Loader.RunAlone()` |
| `Vortex.Core.AppSystem` | `App.OnStart` |
| `Vortex.Unity.UI.Misc` | `DropDownComponent` |
| `Naninovel` | `ILocalizationManager`, `LocalizationConfiguration`, `LocalizableResourceLoader` |
| `Vortex.NaniExtensions.Core` | `NaniWrapper` |
| `Cysharp.Threading.Tasks` | `UniTask` |

---

## Architecture

### Driver

```
LocalizationDriver (Singleton, IDriver, IChanneledDriver, IProcess)
├── LocalizationDriver.cs              — Init, SetLanguage, GetLanguages, channels
├── LocalizationDriverExtLoading.cs    — RuntimeInitialize, RunAsync (IProcess)
└── Editor/LocalizationDriverExtEditor.cs — EditorRegister, menu, RefreshIndex
```

Implements `IDriver` (Core contract) and `IChanneledDriver` (per-channel languages). Replaces the standard Unity driver via `Localization.SetDriver()`.

Driver events:

| Event | Description |
|-------|-------------|
| `OnLocalizationChanged` | Fires after language change (SetLanguage, SetChannelLanguage for UI) |
| `OnInit` | Fires after asynchronous data loading completes (`RunAsync`) |

### Channels

```
LocaleChannels
├── UI = 0        — interface language (Vortex index)
├── Dialogue = 1  — dialogue language (Naninovel ILocalizationManager)
└── Voice = 2     — voice language (Naninovel VoiceLoader)
```

Only the UI channel reloads the Vortex index on language change. Dialogue and Voice save preference to `PlayerPrefs` and pass the setting to Naninovel.

Persistence: `PlayerPrefs("AppLanguage")` — shared, `PlayerPrefs("AppLanguage{channel}")` — per-channel.

### Core Extension

`LocalizationExtNani.cs` — partial on `Localization` (assembly ref `ru.vortex.localization.ext`):

| Method | Description |
|--------|-------------|
| `GetCurrentVoiceLanguage()` | Voice language. Lazy: channel PlayerPrefs → default → fallback |
| `GetCurrentDialogueLanguage()` | Dialogue language. Lazy: channel PlayerPrefs → default → fallback |
| `SetCurrentVoiceLanguage(string)` | Persistence via `IChanneledDriver.SetChannelLanguage` |
| `SetCurrentDialogueLanguage(string)` | Persistence via `IChanneledDriver.SetChannelLanguage` |

Channel access via `ChDriver` — casts `IDriver` to `IChanneledDriver`. If the driver doesn't support channels, setters only save the local value.

### NaniVortexLocaleConnector

Static bridge. On `App.OnStart` subscribes to `OnLocalizationChanged` and performs initial synchronization:

- `SetNaniDialogueLocale()` → `l10n.SelectLocale(dialogueLanguage)` + calls `SetNaniVoiceLocale()`
- `SetNaniVoiceLocale()` → `voiceLoader.OverrideLocale = voiceLanguage`
- Both methods call `StateManager.SaveGlobal()`
- On `AppStates.Stopping` — auto-unsubscribe, methods abort

Changing dialogue language always updates voice as well via internal `SetNaniVoiceLocale()` call.

---

## Data

### LocalizationPreset

`ScriptableObject` in `Resources/Localization/`.

| Field | Description |
|-------|-------------|
| `path` | Path to Naninovel localization folders (relative to `Assets/`) |
| `files` | Base TextAssets with keys (`key:value` format) |
| `languages` | TextAsset with language list (`key:Name` format) |
| `defaultLanguage` | Default language (`[Language]` selector) |
| `langs` | Auto-populated — full language names |
| `langsKeys` | Auto-populated — language keys |
| `localeData` | Auto-populated — `LocalePreset` array |

### LocalePreset

| Field | Type | Description |
|-------|------|-------------|
| `Key` | `string` | Translation key |
| `Texts` | `IReadOnlyList<LanguageData>` | Translations by language |

`SetLangData(LanguageData)` — internal, updates existing translation or adds new one.

### File Structure

```
Assets/
├── Resources/Localization/
│   └── LocalizationDataNaninovell.asset   ← LocalizationPreset
└── {path}/                                ← Naninovel localization folders
    ├── en/
    │   ├── file1.txt                      ← translations (key:value)
    │   └── file2.txt
    ├── ru/
    │   ├── file1.txt
    │   └── file2.txt
    └── ...
```

File names in language folders must match the names of base `files[]`.

### Parsing Logic (Editor)

Two-pass parsing for each language file:
1. **First pass** — from base file (`files[]`): fills keys with translation from the language folder
2. **Second pass** — from language folder file: overwrites translations, validates keys

Recursive folder traversal: if a directory is not a language key (not found in `languages`), the parser descends deeper.

---

## Components

### NaniLocaleHandler

UI language selection component by channel. Works through `DropDownComponent`.

| Field | Description |
|-------|-------------|
| `whiteList` | Available language filter (`[ValueDropdown]` from `LocalizationConfiguration`) |
| `dropdown` | `DropDownComponent` for selection |
| `mode` | `LocaleChannels` — UI, Dialogue, or Voice |

Language source: `ILocalizationManager.AvailableLocales`, filtered through `whiteList`.

Dropdown labels: `lang.ToUpper().Translate()` — language key is translated via Vortex localization.

On selection:
- UI → `Localization.SetCurrentLanguage()`
- Dialogue → `Localization.SetCurrentDialogueLanguage()`
- Voice → `Localization.SetCurrentVoiceLanguage()`

Subscribes to `OnLocalizationChanged` (`OnEnable`/`OnDisable`) for dropdown synchronization.

---

## Editor Tools

| Menu Item | Description |
|-----------|-------------|
| `Vortex/Localization/(Nani) Load data` | Parse Naninovel files → populate `LocalizationPreset` |

Inspector `LocalizationPreset`:
- Drag-and-drop `folder` for quick `path` assignment
- `Debug` dropdown — preview translation by key
- `Check System Language` button

Menu item is only available after successful driver initialization (`_isSet` validation).

Auto-creates asset on `InitializeOnLoadMethod` if `LocalizationPreset` is missing from Resources.

---

## Usage

### Setup

1. Ensure Naninovel localization folder structure is configured
2. Open `LocalizationPreset` in `Resources/Localization/`
3. Set `path` (or drag folder), assign `files` and `languages`
4. Select `defaultLanguage`
5. `Vortex/Localization/(Nani) Load data`

### Language Settings UI

1. Add `NaniLocaleHandler` to a GameObject
2. Assign `DropDownComponent`
3. Select `mode` (UI / Dialogue / Voice)
4. Configure `whiteList` — available languages for this channel

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `SetLanguage` with same language (matches PlayerPrefs) | Early return, index not reloaded |
| `SetChannelLanguage` for Dialogue/Voice | PlayerPrefs save only, no index reload |
| `SetChannelLanguage` for UI (channel 0) | Index reload + `OnLocalizationChanged` |
| Channel language not set | Lazy init: channel PlayerPrefs → default PlayerPrefs → system → fallback |
| `LocalizationPreset` not found | `LogError`, driver not registered |
| Language folder file missing from `files[]` | Silently skipped |
| Translation key missing in language folder | `LogError` with key and language |
| Key not found in index during parsing | `LogError` "Wrong key" |
| Empty translation | `LogError`, key skipped |
| Language not in list, translation missing | Fallback to first available translation (`Texts[0]`) |
| Repeated `Load Data` during loading | Blocked by `_run` flag |
| `App.OnStart` without Naninovel Engine | Exception in `Engine.GetServiceOrErr` |
| `AppStates.Stopping` | Connector unsubscribes, synchronization stops |

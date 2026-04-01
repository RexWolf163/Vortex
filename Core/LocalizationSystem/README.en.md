# LocalizationSystem (Core)

**Namespace:** `Vortex.Core.LocalizationSystem.Bus`, `Vortex.Core.LocalizationSystem`
**Assembly:** `ru.vortex.localization`
**Platform:** .NET Standard 2.1+

---

## Purpose

Localization system core. Stores the current language and a translation index (key → text), provides API for translation access.

Capabilities:

- Current application language storage and switching
- Translation access by string key
- String extension methods: `Translate()`, `TryTranslate()`
- `OnLocalizationChanged` event on language change
- Editor API: language list, key list, locale switching

Out of scope:

- Data loading from source (Google Sheets, files) — driver responsibility
- Language preference persistence — driver responsibility
- Localization UI components

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `ISystemDriver` |
| `Vortex.Core.Extensions` | `StringExt.IsNullOrWhitespace()`, `DictionaryExt.AddNew()` |

---

## Architecture

```
Localization (SystemController<Localization, IDriver>)
  ├── index: Dictionary<string, string>     ← key → translation
  ├── CurrentLanguage: string               ← current language (lazy)
  ├── GetTranslate(key) → string            ← index lookup
  └── OnLocalizationChanged                 ← language change event

IDriver (interface)
  ├── GetDefaultLanguage() → string
  ├── SetLanguage(string)
  ├── SetIndex(Dictionary<string, string>)
  ├── GetLanguages() → string[]
  └── OnLocalizationChanged (event)

StringExt (extension methods)
  ├── "KEY".Translate()      → index[key] or "##!key!##"
  └── "KEY".TryTranslate()   → index[KEY] or original string
```

### Translation index

- `Dictionary<string, string>` — created in `Localization`, passed to driver via `SetIndex()`
- Driver fills the dictionary on load and on language change
- Core is unaware of storage format — only reads the index

### IDriver

| Method | Description |
|--------|-------------|
| `GetDefaultLanguage()` | Default language (system or saved) |
| `SetLanguage(string)` | Set language. Driver reloads data and fires `OnLocalizationChanged` |
| `SetIndex(Dictionary<string, string>)` | Binds core index to driver |
| `GetLanguages()` | List of available languages |
| `OnLocalizationChanged` | Event — fired by driver after language change |

---

## Contract

### Input

- Driver connects via `Localization.SetDriver()`
- Driver fills the index with translations for the current language

### Output

- `GetTranslate(key)` — translation or `"##!key!##"` if key missing
- `HasTranslate(key)` — key existence check
- `GetCurrentLanguage()` — current language
- `OnLocalizationChanged` — subscriber notification

### API

| Method | Description |
|--------|-------------|
| `GetCurrentLanguage()` | Current language. Lazy-initialized via `Driver.GetDefaultLanguage()` |
| `SetCurrentLanguage(string)` | Set language. Delegates to driver |
| `GetTranslate(string key)` | Translation by key. `"##!key!##"` if not found |
| `HasTranslate(string key)` | `true` if key exists in index |

### Extension methods (StringExt)

| Method | Description |
|--------|-------------|
| `"KEY".Translate()` | `Localization.GetTranslate(key)`. Empty string → `""` |
| `"KEY".TryTranslate()` | If translation exists for `KEY.ToUpper()` — returns translation, otherwise original string |

### Editor API (partial Localization)

| Method | Description |
|--------|-------------|
| `GetLanguages()` | `List<string>` of available languages |
| `GetLocalizationKeys()` | `List<string>` of all keys in index |
| `SetDefaultLocale()` | Reset to default language (menu `Vortex/Localization/Set Default Locale`) |
| `SetNextLocale()` | Cycle to next language (menu `Vortex/Localization/Set Next Locale`) |

### Event

| Event | Timing |
|-------|--------|
| `OnLocalizationChanged` | After language change (proxies driver event) |

### Limitations

| Limitation | Reason |
|------------|--------|
| One language at a time | Single index per application |
| Keys case-insensitive only in `TryTranslate` | `TryTranslate` calls `ToUpper()`, `Translate` does not |
| `"##!key!##"` on missing key | Visual debug marker |
| Index fully rebuilt on language change | Driver clears and refills |

---

## Usage

### Translate a string

```csharp
// Strict translation — "##!key!##" if key missing
string text = "MENU_START".Translate();

// Soft translation — original string if key missing
string label = "Settings".TryTranslate();
```

### Change language

```csharp
Localization.SetCurrentLanguage("Russian");
```

### Subscribe to language change

```csharp
Localization.OnLocalizationChanged += RefreshUI;
```

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| Key not found in index | `GetTranslate` → `"##!key!##"` |
| Empty/null string in `Translate()` | `""` |
| `TryTranslate` — key without translation | Returns original string unchanged |
| `GetCurrentLanguage()` — language not set | Lazy-initialized via `Driver.GetDefaultLanguage()` |
| Driver not connected | `Driver` access → `NullReferenceException` |

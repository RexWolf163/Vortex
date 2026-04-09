# AudioLocalizationSystem

**Namespace:** `Vortex.Sdk.AudioLocalizationSystem`
**Assembly:** `ru.vortex.sdk.localization.audio`

## Purpose

Maps localization text keys to voiced audio clips. Each key stores a "language → sound asset" map; playback automatically selects the clip for the current language.

Capabilities:
- Mapping localization keys to sound assets per language
- `SoundClipFixed` caching — recreated only on language change
- Playback through a dedicated audio channel with automatic stop of the previous clip
- Batched indexing during loading (`IProcess`, `UniTask.Yield`)

Out of scope:
- Audio channel management (handled by `AudioController`)
- Current language resolution (handled by `Localization`)
- Sound asset storage and loading (handled by `Database` + `Sound`)

## Dependencies

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`
- `Vortex.Core.System.ProcessInfo` — `IProcess`, `ProcessData`
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`
- `Vortex.Core.AudioSystem` — `AudioController`
- `Vortex.Core.LocalizationSystem` — `Localization.GetCurrentVoiceLanguage()`
- `Vortex.Core.LoaderSystem` — `Loader.Register`
- `Vortex.Core.Extensions` — `ActionExt`, `ReactiveValues`

### Unity
- `Vortex.Unity.AudioSystem` — `Sound`, `SoundClipFixed`
- `Vortex.Unity.DatabaseSystem` — `RecordPreset<T>`, `DbRecordAttribute`
- `Vortex.Unity.LocalizationSystem` — `LanguageAttribute`
- `Vortex.Unity.EditorTools` — `ClassFilter`, `AutoLink`, `ClassLabel`
- `Cysharp.Threading.Tasks` — `UniTask`
- `Sirenix.Utilities` — string extensions (Odin Inspector)

## Architecture

```
AudioLocalizationController (Singleton, IProcess)
├── Index: Dictionary<string, AudioLocaleData>   ← TextGuid → data
├── PlayForText(string)                          ← play by key
└── RunAsync()                                   ← indexing on load

AudioLocaleData (Record)
├── TextGuid: string                             ← localization key
├── Voices: Dictionary<string, string>           ← language → sound GUID
├── GetLocale() → Sound                          ← raw preset for current language
└── GetSoundClip() → SoundClipFixed              ← cached clip for playback

AudioLocaleDataPreset (RecordPreset<AudioLocaleData>)
├── textGuid [LocalizationKey]                   ← bound to localization string
└── voices: List<LangGroup>                      ← language + sound asset pairs

AudioLocaleHandler (MonoBehaviour)
└── IDataStorage → StringData → PlayForText()    ← auto-playback on text change
```

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `AudioLocalizationController` | `Singleton<T>`, `IProcess` | Indexing and voice playback by key |
| `AudioLocaleData` | `Record` | Mapping data: key → languages → sounds, `SoundClipFixed` cache |
| `AudioLocaleDataPreset` | `RecordPreset<AudioLocaleData>` | ScriptableObject preset for editor configuration |
| `AudioLocaleHandler` | `MonoBehaviour` | Auto-playback on `StringData` change from `IDataStorage` |

## Contract

### Input
- `Database` — `AudioLocaleData` records loaded before `RunAsync`
- `Localization.GetCurrentVoiceLanguage()` — current voice language
- `IDataStorage` + `StringData` — text key source for `AudioLocaleHandler`

### Output
- `AudioLocalizationController.PlayForText(string)` — play voice for a text key
- `AudioLocaleData.GetLocale()` — get `Sound` for the current language
- `AudioLocaleData.GetSoundClip()` — get cached `SoundClipFixed`

### Guarantees
- `PlayForText` stops the previous clip on the channel before playing a new one
- `SoundClipFixed` is recreated only on language change — repeated calls return the cache
- `RunAsync` batches indexing at 50 records per frame
- `WaitingFor() → Database` — loading starts only after the database is ready

### Constraints
- One voice clip at a time (the channel is fully cleared)
- Odin Inspector required (`Sirenix.Utilities`)
- `AudioLocaleData` is not saved (`GetDataForSave() → null`)

## Usage

### Creating a Preset

1. `Create → Database → AudioLocaleData`
2. Set `textGuid` — the localization string key
3. Fill `voices` — pairs of "language → sound asset (`Sound`)"

### Playback from Code

```csharp
// By text key
AudioLocalizationController.PlayForText("quest_intro_text_guid");
```

### Playback via Handler

1. Add `AudioLocaleHandler` to a GameObject
2. Set `source` — a component with `IDataStorage` providing `StringData`
3. When `StringData.Value` changes, voice playback triggers automatically

### Direct Data Access

```csharp
var data = Database.GetRecord<AudioLocaleData>(guid);

// Raw Sound preset
Sound sound = data.GetLocale();

// Ready-to-play clip (cached)
SoundClipFixed clip = data.GetSoundClip();
```

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `PlayForText` with an unregistered key | Ignored (early return) |
| No voice for the current language | `GetLocale()` → `null`, `GetSoundClip()` → `null`, no sound played |
| Language change between calls | `SoundClipFixed` cache is recreated on the next access |
| Repeated call without language change | Returns cached `SoundClipFixed` |
| `GetLocale()` without language change | Returns fresh `Sound` from DB, cache untouched |
| Duplicate `language` in preset | `ToDictionary` throws `ArgumentException` on load |
| `StringData.Value` == null/empty | `AudioLocaleHandler` ignores (checks `IsNullOrWhitespace`) |
| Loading cancellation (`CancellationToken`) | Indexing stops, `Index` may be incomplete |

# AudioLocalizationSystemV2

**Namespace:** `Vortex.Sdk.AudioLocalizationSystemV2`
**Assembly:** `ru.vortex.sdk.localization.audio.v2`

Successor of `AudioLocalizationSystem` (V1, marked `[Obsolete]`). The difference: voice clips are unloadable — they are the record's own assets behind `AssetHandle<AudioClip>` rather than `Sound` records in the database, and stay in memory only while playing.

## Purpose

Maps localization text keys to voiced audio clips. Each key stores a "language → sound asset" map; playback automatically selects the clip for the current language.

Capabilities:
- Mapping localization keys to sound assets per language
- The clip is loaded for the duration of playback and released when it finishes (`AudioSampleWrapper.OnFinished`)
- Clip storage is chosen in the preset: direct reference (`DirectAssetHandle`) or lazy Addressables loading (`AddressableAssetHandle`, with an `AssetCache` group)
- Playback through a dedicated audio channel with automatic stop of the previous clip
- Batched indexing during loading (`IProcess`, `UniTask.Yield`)

Out of scope:
- Audio channel management (handled by `AudioController`)
- Current language resolution (handled by `Localization`)
- Asset load/cache mechanics (handled by `AssetHandle<T>` and its implementations; for Addressables — `AssetCache`)
## Choosing the version (V1 / V2)

The version is selected by the `audioLocalizationV2` toggle in `SdkSettings` (partial — `DefineSettings/SdkSettings.AudioLocalization.cs`). The toggle drives the `USING_VORTEX_AUDIO_LOCALIZATION_V2` define symbol:

| What | Off (V1) | On (V2) |
|---|---|---|
| Assembly `ru.vortex.sdk.localization.audio` (V1) | compiled | stripped (`defineConstraints: !USING_VORTEX_AUDIO_LOCALIZATION_V2`) |
| Assembly `ru.vortex.sdk.localization.audio.v2` | stripped (`defineConstraints: USING_VORTEX_AUDIO_LOCALIZATION_V2`) | compiled |
| `LocalizationExt` (channel extensions of `Localization`) | V1 copy | V2 copy |

`LocalizationExt` — `LocaleChannels`, `IChanneledDriver`, `StringExtChannel` and the `Localization` partial — is merged into the localization assembly via asmref, so the package's `defineConstraints` don't apply to it. Both packages hold a copy, each guarded by `#if` on the same symbol — exactly one is active. The consumer (`NaniExtensions/LocalizationSystem`: `LocalizationDriver : IChanneledDriver`, `NaniVortexLocaleConnector`, `NaniLocaleHandler`) needs no changes — the types exist with either version. Keep the copies identical: a change to the channel extensions goes into both.

## Dependencies

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`
- `Vortex.Core.System.ProcessInfo` — `IProcess`, `ProcessData`
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`
- `Vortex.Core.AudioSystem` — `AudioController.PlaySoundWithControl`, `AudioSampleWrapper`
- `Vortex.Core.AssetHandleSystem` — `AssetHandle<T>`
- `Vortex.Core.LocalizationSystem` — `Localization.GetCurrentChannelLanguage(LocaleChannels.Voice)`
- `Vortex.Core.LoaderSystem` — `Loader.Register`
- `Vortex.Core.Extensions` — `ActionExt`, `ReactiveValues`

### Unity
- `Vortex.Unity.AudioSystem` — `SoundClipFixed`, `AudioChannelNameAttribute`
- `Vortex.Unity.DatabaseSystem` — `RecordPreset<T>`
- `Vortex.Unity.LocalizationSystem` — `LanguageAttribute`
- `Vortex.Unity.EditorTools` — `ClassFilter`, `AutoLink`, `ClassLabel`
- `Cysharp.Threading.Tasks` — `UniTask`
- `Sirenix.Utilities` — string extensions (Odin Inspector)

The package does not use `AssetHandle<T>` implementations directly (`DirectAssetHandle` in `Vortex.Unity.AssetHandleSystem`, `AddressableAssetHandle` in `Vortex.Unity.AssetCacheSystem`) — the designer picks one in the preset.

## Usage

### Creating a Preset

1. `Create → Database → AudioLocaleDataV2`
2. Set `textGuid` — the localization string key
3. Set `channel` — the voice line audio channel
4. Fill `voices` — "language → clip handle" pairs: pick the handle implementation (`DirectAssetHandle` / `AddressableAssetHandle`) and the clip; for addressable — the cache group
5. Check the error block above `voices`: the inspector reports an empty key, an empty channel, an empty or duplicate language, an unset handle and a handle without a clip (for `DirectAssetHandle` / `AddressableAssetHandle`). The check is recomputed at most once per second

### Playback from Code

```csharp
// By text key
AudioLocalizationController.PlayForText("quest_intro_text_guid");
```

### Playback via Handler

1. Add `AudioLocaleHandler` to a GameObject
2. Set `source` — a component with `IDataStorage` providing `StringData`
3. When `StringData.Value` changes, voice playback triggers automatically. An already voiced value is not replayed until it changes: re-enabling the component (hiding/showing UI) or relinking the storage doesn't restart the line. A change to an empty value also counts — after it the same line is voiced again

### Direct Data Access

```csharp
var data = Database.GetRecord<AudioLocaleData>(guid);

// Clip handle for the current language. Load/Release are the holder's job: the record's handle is shared,
// and AudioLocalizationController uses the same one.
AssetHandle<AudioClip> handle = data.GetLocale();
```

### Editor tool «Bulk rough toolkit»

The `AudioLocaleDataPreset` inspector exposes a foldout group `Bulk rough toolkit` (Russian label: «Грубый инструментарий массовой замены»). It bundles **Editor-only** helpers for the common patterns of rebuilding voice references. Every action calls `EditorUtility.SetDirty` — changes get into the commit.

Clips are looked up by exact `AudioClip` file name in the project (`AssetDatabase`, case-insensitive). A handle is retargeted keeping its implementation: `DirectAssetHandle` gets a new clip reference, `AddressableAssetHandle` gets a new asset GUID in its `AssetReference` (the cache group is kept). A clip that isn't addressable yet must be marked addressable by hand.

| Button | What it does | Why useful |
|---|---|---|
| `Update Voices Array` | For each `voice`, takes the handle's current clip and checks its name contains a dot. Then looks for a clip named `"{TextGuid}.{voice.language}"` and retargets the handle to it. | After renaming the preset's `textGuid` — rewire voice references in bulk to the new key |
| `Замена первой части ключа` (Replace first key segment) | Reads the `charName` field; for every `voice`, looks for a clip named `"{charName}.{oldSuffix}"` where `oldSuffix` is the remainder of the old clip name after the first dot, and retargets the handle. Also rewires the preset's `textGuid` to the localization key `"{charName}.{Name}"` (resolved via `Localization.GetLocalizationKeys()`, case-insensitive). | Cloning a preset for another character: change the prefix, voices automatically point to that character's recordings |
| `Скопировать первую запись под другой язык` (Copy first entry to another language) | Reads the `_newLanguage` field. Uses the first `voice` as a template, replaces the substring `.{templateLang}` with `.{newLanguage}` inside its clip name, looks up the matching clip and appends a new `voice` entry for `_newLanguage` with a handle of the same implementation. If an entry for that language already exists, the action is skipped; if the clip isn't found — `InvalidOperationException`. | Adding a new language: first locale is done; stub the rest with one click |

**Naming convention** the toolkit exploits:
- The clip file name looks like `Character.{anything}.{lang}.{index}` or similar — language sits in one of the dot-separated positions.
- `textGuid` looks like `Character.LineKey`.
- `voice.language` matches the locale code from `Localization`.

When the convention is broken, the toolkit **silently skips** the entry (it checks for a dot in the name, that the target clip exists, etc.) — nothing crashes, but nothing useful happens either. The exception is copying to a language: a missing clip throws. This is by contract: a «rough toolkit» is a best-effort helper, and the result needs a manual sanity check.

## Architecture

```
AudioLocalizationController (Singleton, IProcess)
├── Index: Dictionary<string, AudioLocaleData>   ← TextGuid → data
├── PlayForText(string)                          ← load + play by key
└── RunAsync()                                   ← indexing on load

AudioLocaleData (Record)
├── TextGuid: string                             ← localization key
├── Voices: Dictionary<string, AssetHandle<AudioClip>>  ← language → clip handle
├── Channel: string                              ← voice line audio channel
└── GetLocale() → AssetHandle<AudioClip>         ← handle for the current language

AudioLocaleDataPreset (RecordPreset<AudioLocaleData>)
├── textGuid [LocalizationKey]                   ← bound to localization string
├── channel [AudioChannelName]                   ← audio channel
└── voices: List<LangGroup>                      ← language + [SerializeReference] clip handle pairs

AudioLocaleHandler (MonoBehaviour)
└── IDataStorage → StringData → PlayForText()    ← auto-playback on text change
```

### `PlayForText(text)` flow

```
1. Index[text] → data; missing — exit
2. data.GetLocale() → handle; no voice for the language — exit
3. handle is already loading (repeat request for the same line) — exit
4. channel data.Channel not found — LogError, exit
5. AudioController.StopAllSounds(channel)   ← the playing line finishes → its OnFinished releases the clip
6. cancel the previous line's load (CancellationToken)
7. await handle.LoadAsync(ct)
   ├── cancelled → exit (ownership dropped by the cache)
   ├── load error → LogError, exit
   └── empty clip → LogError, handle.Release(), exit
8. PlaySoundWithControl(new SoundClipFixed(clip, false, channel)) → wrapper
   └── null → LogError, handle.Release()
9. wrapper.OnFinished += handle.Release     ← natural end / preemption / channel stop
```

### Components

| Class | Type | Purpose |
|-------|------|---------|
| `AudioLocalizationController` | `Singleton<T>`, `IProcess` | Indexing, loading and voice playback by key, clip release on finish |
| `AudioLocaleData` | `Record` | Mapping data: key → languages → clip handles, channel |
| `AudioLocaleDataPreset` | `RecordPreset<AudioLocaleData>` | ScriptableObject preset for editor configuration |
| `AudioLocaleHandler` | `MonoBehaviour` | Auto-playback on `StringData` change from `IDataStorage` |

## Contract

### Input
- `Database` — `AudioLocaleData` records loaded before `RunAsync`
- `Localization.GetCurrentChannelLanguage(LocaleChannels.Voice)` — current voice language
- `IDataStorage` + `StringData` — text key source for `AudioLocaleHandler`

### Output
- `AudioLocalizationController.PlayForText(string)` — play voice for a text key
- `AudioLocaleData.GetLocale()` — get the clip handle for the current language

### Guarantees
- `PlayForText` stops the previous clip on the channel before playing a new one
- The clip is released (`AssetHandle.Release`) on any finish of the sound: natural end, preemption by the next line, channel stop; and also when playback fails to start
- A line preempted while loading never plays
- `RunAsync` batches indexing at 50 records per frame
- `WaitingFor() → Database` — loading starts only after the database is ready

### Constraints
- One voice clip at a time (the channel is fully cleared)
- Start delay: with `AddressableAssetHandle` the voice starts after the clip loads asynchronously (immediately on a HIT from `AssetCache` survivors)
- Odin Inspector required (`Sirenix.Utilities`)
- `AudioLocaleData` is not saved (`GetDataForSave() → null`)

### Unloading and cache group

The package only calls the handle's `LoadAsync`/`Release`. What "unload" means is up to the implementation:
- `DirectAssetHandle` — no-op: the clip lives with the preset (V1 behaviour);
- `AddressableAssetHandle` — `AssetCache.Release` in the group selected in the handle's `group` field. The actual unload follows that group's survivor capacity (0 — right after `Release`). It's worth creating a dedicated group for voice lines in `AssetCacheSettings`, so the stream of lines doesn't evict other assets' survivors.

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `PlayForText` with an unregistered key | Ignored (early return) |
| No voice for the current language | `GetLocale()` → `null`, no sound played |
| Line channel not set / not found | `Debug.LogError`, no sound played |
| Repeat `PlayForText` for the same line while it loads | Ignored — the line plays once loading completes |
| Repeat `PlayForText` for the same line while it plays | The playing one stops (clip released), the clip is loaded again (for addressable — a HIT while still in survivors) |
| New line while the previous one is loading | The previous load is cancelled; it never plays |
| Line A requested again while its preempted load is still in flight (A → B → A) | A new load cycle. The cancelled load removes only its own hold in `AssetCache`, the clip stays held by the playing line |
| Clip load error | `Debug.LogError`, no sound played |
| Language change between calls | The next call takes the handle of the new language |
| Duplicate `language` in preset | Validation error in the inspector; on load `ToDictionary` throws `ArgumentException` |
| Voice handle not set for a language | Validation error in the inspector; `Debug.LogError` during indexing; no sound for that language |
| Handle without a clip (`DirectAssetHandle` with an empty `asset`) | Validation error in the inspector; `Debug.LogError` on playback, the handle is released |
| Playback failed to start (`PlaySoundWithControl` → `null`) | `Debug.LogError`, the handle is released |
| `StringData.Value` == null/empty | `AudioLocaleHandler` ignores (checks `IsNullOrWhitespace`) |
| `AudioLocaleHandler` re-enabled / storage relinked with the same value | Not replayed |
| `AudioLocaleHandler` received a value before indexing completed | `PlayForText` ignores it, but the value counts as voiced — it plays only after the next change |
| Loading cancellation (`CancellationToken`) | Indexing stops, `Index` may be incomplete |

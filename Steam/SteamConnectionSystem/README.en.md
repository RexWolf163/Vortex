# SteamConnectionSystem

**Namespace:** `Vortex.Steam.SteamConnectionSystem`
**Assembly:** `ru.vortex.steam.connection`
**Platform:** Unity 2021.3+, Steamworks.NET
**Conditional compilation:** code behind `#if USING_STEAM`, assembly has no `defineConstraints`

---

## Purpose

Connection to the Steamworks API. Initializes the Steam client, exposes global connection state and current user data (profile, friends list) through the static `SteamBus` bus.

Capabilities:

- `SteamBus` — static bus: `SteamEnabled`, `IsInitialized`, `IsLoaded`, `User`, events `OnCallServices`/`OnLoaded`
- `SteamManager` — MonoBehaviour singleton: `SteamAPI.Init()`, `RunCallbacks()`, `Shutdown()`
- `SteamUserData` / `SteamUserShortData` — user and friends data models
- `SteamConnectionSettings` — ScriptableObject configuration: `steamAppId`, `isEnabled`, `isTestBuild`
- `DefineSymbolManager` — automatic `USING_STEAM` symbol management in `PlayerSettings`
- `Settings` — settings asset loading/creation, `steam_appid.txt` synchronization

Out of scope:

- Achievements — `SteamAchievements` package
- Multiplayer, matchmaking, lobbies
- Purchases, DLC, inventory

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Steamworks.NET` | `SteamAPI`, `SteamUser`, `SteamFriends`, `CSteamID` |
| `Vortex.Unity.EditorTools` | `[OnChanged]`, `[ToggleButton]` for Inspector |

---

## Architecture

```
SteamBus (static)
  ├── SteamEnabled: const bool          ← #if USING_STEAM: true, else false
  ├── IsInitialized: bool               ← SteamAPI.Init() succeeded
  ├── IsLoaded: bool                    ← user data loaded
  ├── User: SteamUserData               ← profile + friends
  ├── OnCallServices: Action            ← signal for subscribers to load their data
  └── OnLoaded: Action

SteamManager : MonoBehaviour (singleton)
  ├── [RuntimeInitializeOnLoadMethod] Init()
  │   └── new GameObject("SteamManager").AddComponent<SteamManager>()
  ├── Awake()
  │   ├── RestartAppIfNecessary(AppId) → Application.Quit() if not via Steam
  │   ├── SteamAPI.Init() → m_bInitialized
  │   ├── Callback<UserStatsReceived_t>.Create()
  │   ├── SteamBus.IsInitialized = true
  │   └── LoadStatsInBus()
  │       ├── SteamBus.LoadServices() → OnCallServices
  │       ├── SteamBus.User.Init(steamId)
  │       └── SteamBus.IsLoaded = true
  ├── Update() → SteamAPI.RunCallbacks()
  └── OnDestroy() → SteamAPI.Shutdown()

SteamUserData : SteamUserShortData
  ├── SteamID: CSteamID
  ├── Name: string
  ├── Friends: Dictionary<CSteamID, SteamUserShortData>
  ├── OnUpdated: Action
  └── Init(steamId) → loads profile and friends list

SteamUserShortData
  ├── SteamID: CSteamID
  ├── Name: string
  └── GetShortData(steamId) → factory

SteamConnectionSettings : ScriptableObject
  ├── steamAppId: uint (default 480)
  ├── isEnabled: bool                   ← [OnChanged] → DefineSymbolManager.Refresh()
  ├── isTestBuild: bool                 ← skips RestartAppIfNecessary
  └── OnAppUdChanged() → File.WriteAllText("steam_appid.txt")

DefineSymbolManager (Editor-only)
  ├── [InitializeOnLoadMethod] Refresh()
  └── ApplyDefineSymbol(bool, "USING_STEAM") → PlayerSettings

Settings (internal)
  ├── GetSettings() → Resources.LoadAll / CreateAsset
  └── [InitializeOnLoadMethod] CheckSteamAppId() → steam_appid.txt sync
```

### Initialization Order

1. `DefineSymbolManager.Refresh()` — on Editor load, sets/removes `USING_STEAM`
2. `Settings.CheckSteamAppId()` — synchronizes `steam_appid.txt` with the settings asset
3. `SteamManager.Init()` — `RuntimeInitializeOnLoadMethod`, creates GameObject
4. `SteamManager.Awake()` — `SteamAPI.Init()`, loads user data
5. `SteamBus.LoadServices()` — `OnCallServices` for subscribers (e.g., `AchievementsController`)

### Conditional Compilation

Assembly `ru.vortex.steam.connection` has no `defineConstraints` — it always compiles. All Steamworks code is inside `#if USING_STEAM`. Without the symbol, `SteamBus.SteamEnabled = false` and `IsInitialized` cannot become `true`. This allows other assemblies to reference `SteamBus` without depending on Steamworks.

### Safe Mode Without Steam

With `isTestBuild = true`, `RestartAppIfNecessary()` is skipped — the application does not restart through the Steam client. Used for debugging from the editor.

---

## Contract

### Input

- `SteamConnectionSettings` in `Resources/Editor/` — `steamAppId`, `isEnabled`, `isTestBuild`
- Steamworks.NET added to the project
- Steam client running on the machine

### Output

- `SteamBus.IsInitialized` — Steam API initialized
- `SteamBus.IsLoaded` — user data loaded
- `SteamBus.User` — profile, name, friends list
- `SteamBus.OnCallServices` — moment for dependent services to load

### API

| Method / Property | Description |
|-------------------|-------------|
| `SteamBus.SteamEnabled` | `const bool` — Steam connected at compilation level |
| `SteamBus.IsInitialized` | `SteamAPI.Init()` succeeded |
| `SteamBus.IsLoaded` | User data loaded |
| `SteamBus.User` | `SteamUserData` — current user |
| `SteamBus.User.Friends` | `Dictionary<CSteamID, SteamUserShortData>` |
| `SteamBus.User.OnUpdated` | Event on stats update (`UserStatsReceived_t`) |
| `SteamBus.OnCallServices` | Event for dependent service initialization |

### Constraints

| Constraint | Reason |
|------------|--------|
| Requires running Steam client | `SteamAPI.Init()` returns `false` without client |
| `steam_appid.txt` must match settings | Automatic sync on Editor load |
| `DontDestroyOnLoad` on `SteamManager` | Single instance for entire lifecycle |
| Steamworks calls forbidden in `OnDestroy` | `SteamAPI.Shutdown()` may have been called already |
| Platforms: Windows, Linux, macOS | `DefineSymbolManager` filters by standalone platforms |

---

## Usage

### Project Setup

1. Add Steamworks.NET to the project
2. Open `SteamConnectionSettings` in `Resources/Editor/` (created automatically)
3. Set `steamAppId` to your application ID
4. Enable `isEnabled` — the `USING_STEAM` symbol is added automatically
5. For debugging without Steam client, enable `isTestBuild`

### Reading User Data

```csharp
if (SteamBus.IsLoaded)
{
    var name = SteamBus.User.Name;
    var friends = SteamBus.User.Friends;
}
```

### Subscribing to Service Loading

```csharp
// Connect to Steam initialization moment
SteamBus.OnCallServices += LoadMyService;

// Subscribe to stats updates
SteamBus.User.OnUpdated += OnStatsReceived;
```

### Conditional Compilation in Game Code

```csharp
#if USING_STEAM
    SteamBus.User.UnlockAchievement("ACH_001");
#endif

// Or via const:
if (SteamBus.SteamEnabled)
    DoSteamStuff();
```

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| Steam client not running | `SteamAPI.Init()` → `false`, `IsInitialized` stays `false` |
| `steam_api.dll` not found | `DllNotFoundException`, `Application.Quit()` |
| `isTestBuild = false`, launched outside Steam | `RestartAppIfNecessary()` → `true`, restart via Steam client |
| `isTestBuild = true` | `RestartAppIfNecessary()` skipped |
| Duplicate `SteamManager` initialization | `Debug.LogError`, `Destroy(gameObject)` |
| `USING_STEAM` not defined | `SteamBus.SteamEnabled = false`, `IsInitialized` always `false` |
| Duplicate friend in list | `TryAdd` → `Debug.LogError("There is broken data")` |
| Domain Reload disabled | `InitOnPlayMode()` resets static fields |
| `steamAppId = 0` | Automatically replaced with `480` on Editor load |

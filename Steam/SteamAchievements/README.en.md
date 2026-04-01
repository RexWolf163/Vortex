# SteamAchievements

**Namespace:** `Vortex.Steam.SteamAchievements`
**Assembly:** `ru.vortex.steam.achievements`
**Platform:** Unity 2021.3+, Steamworks.NET
**Conditional compilation:** `defineConstraints: ["USING_STEAM"]` — assembly only compiles when the symbol is present

---

## Purpose

Steam achievements management. Loads the achievement index from `SteamUserStats`, provides an API for unlocking and reading via extension methods on `SteamUserData`.

Capabilities:

- `AchievementsController` — internal static controller: loads achievement index on Steam initialization
- `AchievementsExtensions` — extension methods on `SteamUserData`: `UnlockAchievement()`, `GetAchievement()`, `GetAllAchievementsID()`
- `Achievement` — data model: ID, Name, Description, IsUnlocked, IsHidden
- `AchievementsManager` — Editor-only MonoBehaviour: displays and toggles achievements in Inspector
- `AchievementHandler` — Editor-only element model for Inspector with `[ToggleButton]` and `[ClassLabel]`

Out of scope:

- Steam connection, API initialization — `SteamConnectionSystem` package
- Steam statistics (leaderboards, stats) — not implemented
- Runtime achievement UI — implemented in the project layer

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Steamworks.NET` | `SteamUserStats` — reading/writing achievements |
| `Vortex.Steam.SteamConnectionSystem` | `SteamBus` — connection state, `SteamUserData` — extension method anchor |
| `Vortex.Unity.AppSystem` | `TimeController.Accumulate()` — `StoreStats()` batching |
| `Vortex.Unity.EditorTools` | `[VortexCollection]`, `[ClassLabel]`, `[InfoBubble]`, `[ToggleButton]`, `[OnChanged]`, `[LabelText]` |

---

## Architecture

```
AchievementsController (internal static)
  ├── _achievementsIndex: Dictionary<string, Achievement>
  ├── [RuntimeInitializeOnLoadMethod] Run()
  │   ├── SteamBus.IsInitialized → LoadAllAchievements()
  │   └── otherwise → SteamBus.OnCallServices += LoadAllAchievements
  └── LoadAllAchievements()
      ├── SteamUserStats.GetNumAchievements()
      └── foreach → Achievement { ID, Name, Description, IsUnlocked, IsHidden }

AchievementsExtensions (static, extension on SteamUserData)
  ├── UnlockAchievement(id)
  │   ├── SteamUserStats.SetAchievement(id)
  │   └── TimeController.Accumulate(() => StoreStats())
  ├── GetAchievement(id) → Achievement
  ├── GetAllAchievementsID() → string[]
  ├── ClearAchievement(id)          ← Editor-only
  └── ResetAllAchievements()        ← Editor-only

Achievement
  ├── ID: string
  ├── Name: string
  ├── Description: string
  ├── IsUnlocked: bool
  └── IsHidden: bool

AchievementsManager : MonoBehaviour (Editor-only singleton)
  ├── index: AchievementHandler[]    ← [VortexCollection]
  ├── [RuntimeInitializeOnLoadMethod] Init() → creates test GameObject
  ├── Start() → SteamBus.User.OnUpdated += Refresh
  └── Refresh() → populates index from SteamBus

AchievementHandler (Editor-only, Serializable)
  ├── isUnlocked: bool               ← [ToggleButton] + [OnChanged("UpdateAchievements")]
  ├── Id, Name, Description
  ├── Label() → [ClassLabel]
  ├── Info() → [InfoBubble]
  └── UpdateAchievements() → Unlock/Clear
```

### Index Loading

1. `AchievementsController.Run()` is called via `RuntimeInitializeOnLoadMethod`
2. If `SteamBus.IsInitialized` — loads immediately, otherwise — subscribes to `OnCallServices`
3. `LoadAllAchievements()` iterates `SteamUserStats.GetNumAchievements()`, fills `_achievementsIndex`
4. For each achievement: ID, name, desc, unlocked, hidden are read

### StoreStats Batching

`UnlockAchievement()` calls `SteamUserStats.SetAchievement()` immediately, but `StoreStats()` is deferred via `TimeController.Accumulate()`. When multiple achievements are unlocked in the same frame, `StoreStats()` is called once.

### Editor Tool

`AchievementsManager` is created automatically (`RuntimeInitializeOnLoadMethod`) only in Editor with `USING_STEAM`. Displays an `AchievementHandler` array in Inspector with toggle buttons for unlocking/clearing achievements. Subscribes to `SteamBus.User.OnUpdated` for state updates.

---

## Contract

### Input

- `SteamBus.IsInitialized = true` — Steam API initialized
- Achievements configured in Steamworks (App Admin → Achievements)
- `SteamUserStats.RequestCurrentStats()` called (implicitly via `SteamManager`)

### Output

- `Achievement` models with current state (`IsUnlocked`)
- Unlocking via `SteamBus.User.UnlockAchievement(id)`
- All IDs via `SteamBus.User.GetAllAchievementsID()`

### API

| Method | Description |
|--------|-------------|
| `SteamBus.User.UnlockAchievement(id)` | Unlock an achievement |
| `SteamBus.User.GetAchievement(id)` | Get `Achievement` by ID or `null` |
| `SteamBus.User.GetAllAchievementsID()` | All achievement IDs in the project |
| `AchievementsExtensions.ClearAchievement(id)` | Clear an achievement (Editor-only) |
| `AchievementsExtensions.ResetAllAchievements()` | Reset all achievements (Editor-only) |

### Constraints

| Constraint | Reason |
|------------|--------|
| `defineConstraints: ["USING_STEAM"]` | Assembly does not compile without the symbol |
| `UnlockAchievement` requires `SteamBus.IsLoaded` | User data must be loaded |
| `ClearAchievement` / `ResetAllAchievements` — Editor-only | Testing only |
| `AchievementsManager` — Editor-only | Not included in builds |
| Does not use Database bus | Architecture independent of the Vortex bus |

---

## Usage

### Unlocking an Achievement

```csharp
#if USING_STEAM
if (SteamBus.IsLoaded)
    SteamBus.User.UnlockAchievement("ACH_WIN_FIRST_BATTLE");
#endif
```

### Checking State

```csharp
#if USING_STEAM
var ach = SteamBus.User.GetAchievement("ACH_WIN_FIRST_BATTLE");
if (ach != null && !ach.IsUnlocked)
    ShowAchievementHint();
#endif
```

### Getting All Achievements

```csharp
#if USING_STEAM
var ids = SteamBus.User.GetAllAchievementsID();
foreach (var id in ids)
{
    var ach = SteamBus.User.GetAchievement(id);
    Debug.Log($"{ach.Name}: {(ach.IsUnlocked ? "Unlocked" : "Locked")}");
}
#endif
```

### Testing in Editor

1. Enter Play Mode with `isEnabled` and `isTestBuild` enabled in `SteamConnectionSettings`
2. Find the `AchievementsManager [TEST]` object in Hierarchy
3. In Inspector, use toggle buttons to unlock/clear achievements

---

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| `SteamBus.IsLoaded = false` when calling `UnlockAchievement` | No-op, early return |
| Non-existent `achievementID` | `GetAchievement()` → `null`, `Debug.LogError` |
| Multiple `UnlockAchievement` in one frame | `SetAchievement()` for each, `StoreStats()` once (batching) |
| Steam not configured (no achievements in Steamworks) | `GetNumAchievements()` → 0, empty index |
| `USING_STEAM` not defined | Assembly does not compile (`defineConstraints`) |
| `AchievementsController.Run()` before `SteamBus.IsInitialized` | Subscribes to `OnCallServices`, loading deferred |
| Repeated `UnlockAchievement` on already unlocked | `SetAchievement()` — idempotent in Steamworks |

# SteamExtensions.Quests

**Namespace:** `Vortex.Steam.SteamExtensions.Quests`
**Assembly:** `ru.vortex.steam.ext.quests`
**Platform:** Unity 2021.3+, Steamworks.NET, Vortex Quests
**Conditional compilation:** `defineConstraints: ["USING_VORTEX_QUESTS"]` — the assembly compiles only when the quest system is present. Steam access is behind `#if USING_STEAM` in code: without the symbol the step still exists but runs as a no-op.

---

## Purpose

Cross-bridge **Vortex Quests ↔ Steam Achievements**: quest logic steps (`QuestLogic`) that unlock a Steam achievement by its ID when executed.

The package is deliberately separate from both the quest core and the Steam package so neither pulls a dependency on the other. It lives under `Vortex/Steam/SteamExtensions` (Steam-domain extensions targeting specific Vortex systems).

Features:

- `ActivateAchievementsLogic` — a `QuestLogic` that unlocks `achievementId` when the step runs. Without `USING_STEAM` — `return false`.
- `IncrementAchievementsLogic` — a draft (whole file commented out), not part of the build. Intended as an incremental-progress variant — that requires a different Steam API (`IndicateAchievementProgress`); in its current form it just duplicates unlock.

Out of scope:

- Steam init, connection state — `SteamConnectionSystem`
- Achievement index loading, `UnlockAchievement` itself — `SteamAchievements`
- Quest orchestration (start/interrupt/save) — `ru.vortex.sdk.game.quests`

---

## Dependencies

| Dependency | Purpose |
|-----------|---------|
| `ru.vortex.sdk.game.quests` | `QuestLogic` — quest logic step base (`USING_VORTEX_QUESTS`) |
| `ru.vortex.steam.achievements` | `SteamUserData.UnlockAchievement()` (`USING_STEAM`) |
| `ru.vortex.steam.connection` | `SteamBus.User` — extension-method anchor (`USING_STEAM`) |
| `UniTask` | async `Run` |

> References to the Steam assemblies need no symbol in the asmdef itself: without `USING_STEAM` they are not built, the references are ignored, and calls to them are hidden behind `#if USING_STEAM`.

---

## Architecture

```
ActivateAchievementsLogic : QuestLogic          [Serializable]
  ├── name: string            — editor label (GetEditorLabel)
  ├── achievementId: string   — Steam achievement ID
  ├── Run(CancellationToken) → UniTask<bool>
  │   ├── #if USING_STEAM  → SteamBus.User.UnlockAchievement(achievementId); return true
  │   └── #else            → return false
  └── GetEditorLabel() → "Unlock SteamAchievement: {name}"

IncrementAchievementsLogic — commented out (draft, out of build)
```

### Flow

1. The `ActivateAchievementsLogic` step sits in a quest's `QuestLogic[]` chain and reaches execution.
2. With `USING_STEAM`, `SteamBus.User.UnlockAchievement(achievementId)` is called — all guards (`SteamBus.IsLoaded`, ID present in index, `StoreStats` batching) live inside the extension method.
3. The step returns `true` (fire-and-forget: the quest is not blocked by a bad ID). Without `USING_STEAM` — `false`.

---

## Contract

### Input

- `USING_VORTEX_QUESTS` defined (otherwise the package does not compile)
- Quest asset contains `ActivateAchievementsLogic` in `QuestLogic[]` with a set `achievementId`
- For an actual unlock: `USING_STEAM`, `SteamBus.IsLoaded == true`, achievement configured in Steamworks

### Output

- Steam achievement unlocked (`StoreStats` batched — deferred)
- Step returns `true` (Steam) / `false` (no Steam)

### API

| Type | Description |
|------|-------------|
| `ActivateAchievementsLogic` | Serializable `QuestLogic`; added to a quest via `[SerializeReference] QuestLogic[]` |

### Limitations

| Limitation | Reason |
|------------|--------|
| `defineConstraints: ["USING_VORTEX_QUESTS"]` | `QuestLogic` unavailable without the quest system |
| No-op without `USING_STEAM` (`return false`) | Steam assemblies not built; call hidden by `#if` |
| Does not validate `achievementId` | The guard (`GetAchievement != null`) is inside `UnlockAchievement`; the step is always `true` |

---

## Usage

1. In a quest asset add the `Activate Achievements Logic` step to the logic array.
2. Fill `name` (label) and `achievementId` (Steamworks achievement ID).

```
QuestLogic[]:
  - RunNaniScript ...
  - ActivateAchievementsLogic { name: "First Win", achievementId: "ACH_WIN_FIRST" }
  - ...
```

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| `USING_STEAM` undefined | `Run` → `false`, achievement untouched |
| Non-existent `achievementId` | `UnlockAchievement` logs `not found`, step still `true` |
| `SteamBus.IsLoaded == false` | `UnlockAchievement` early-returns (no-op), step `true` |
| Repeat call for an unlocked one | Idempotent (Steamworks) |

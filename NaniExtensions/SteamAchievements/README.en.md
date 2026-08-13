# NaniExtensions.SteamAchievements

**Namespace:** `Vortex.NaniExtensions.SteamAchievements`
**Assembly:** `ru.vortex.nani.steam.achievements`
**Platform:** Unity 2021.3+, Naninovel (`com.elringus.naninovel`), Steamworks.NET
**Conditional compilation:** `defineConstraints: ["USING_NANINOVELL"]` — the assembly compiles only with Naninovel. Steam access is behind `#if USING_STEAM` in code: without the symbol the command still exists but runs as a no-op.

---

## Purpose

Cross-bridge **Naninovel ↔ Steam Achievements**: a Naninovel command that unlocks a Steam achievement by a string key straight from the script.

The package is separate from both the Steam core and the game layer: the Naninovel dependency is not pulled into `Vortex/Steam`, and the Steam dependency is not pulled into game scripts. It lives under `Vortex/NaniExtensions` (the layer bridging Vortex systems to Naninovel).

Features:

- `UnlockAchievementCommand` (`@UnlockSteamAchievement <key>`) — unlocks a Steam achievement by ID. Without `USING_STEAM` — no-op (warning). Empty key — error, step skipped.

Out of scope:

- Steam init, connection state — `SteamConnectionSystem`
- Achievement index loading, `UnlockAchievement` itself — `SteamAchievements`
- Script playback/parsing — Naninovel

---

## Dependencies

| Dependency | Purpose |
|-----------|---------|
| `Elringus.Naninovel.Runtime` | `Command`, `StringParameter`, `UniTask`, parameter attributes |
| `ru.vortex.steam.achievements` | `SteamUserData.UnlockAchievement()` (`USING_STEAM`) |
| `ru.vortex.steam.connection` | `SteamBus.User` — extension-method anchor (`USING_STEAM`) |

> `UniTask` comes transitively via Naninovel — no separate reference needed. References to the Steam assemblies need no symbol in the asmdef: without `USING_STEAM` they are not built, the references are ignored, and calls are hidden behind `#if USING_STEAM`.

---

## Architecture

```
UnlockAchievementCommand : Command      [CommandAlias("UnlockSteamAchievement"), Preserve, Serializable]
  ├── Key: StringParameter    — nameless required parameter (Steam achievement ID)
  └── Execute(AsyncToken) → UniTask
      ├── empty key    → Debug.LogError, skip
      ├── #if USING_STEAM  → SteamBus.User.UnlockAchievement(key)
      └── #else            → Debug.LogWarning (no-op)
```

### Why `#if` instead of a `USING_STEAM` constraint

A Naninovel command must **always exist** (otherwise Naninovel fails when parsing the `@UnlockSteamAchievement` line). So the package is constrained on `USING_NANINOVELL` only (no Naninovel — no commands, no point), and the Steam dependency is hidden behind `#if USING_STEAM`, giving a safe no-op in non-Steam builds. This is a deliberate difference from the `SteamExtensions.Quests` / quest-logic pattern, which is data-referenced and can safely be absent.

---

## Contract

### Input

- `USING_NANINOVELL` defined (otherwise the package does not exist)
- Script line: `@UnlockSteamAchievement <achievementID>`
- For an actual unlock: `USING_STEAM`, `SteamBus.IsLoaded == true`, achievement configured in Steamworks

### Output

- Steam achievement unlocked; the command is non-blocking (immediate `UniTask.CompletedTask`)

### API

| Command | Description |
|---------|-------------|
| `@UnlockSteamAchievement <key>` | Unlock the achievement with ID `<key>` (nameless required parameter) |

### Limitations

| Limitation | Reason |
|------------|--------|
| `defineConstraints: ["USING_NANINOVELL"]` | No `Command` without Naninovel |
| No-op without `USING_STEAM` (warning) | Steam assemblies not built; call hidden by `#if` |
| Does not validate `key` against the index | Guards (`IsLoaded`, `not found`) live inside `UnlockAchievement` |

---

## Usage

```
; in a nani script
@UnlockSteamAchievement ACH_WIN_FIRST_BATTLE
```

`ACH_WIN_FIRST_BATTLE` is a Steam achievement **ID** from Steamworks (App Admin → Achievements).

---

## Edge cases

| Situation | Behavior |
|-----------|----------|
| Empty key | `Debug.LogError`, command skipped |
| `USING_STEAM` undefined | `Debug.LogWarning`, no-op |
| Non-existent key | `UnlockAchievement` logs `Achievement not found` |
| `SteamBus.IsLoaded == false` | `UnlockAchievement` early-returns (no-op) |
| Repeat call for an unlocked one | Idempotent (Steamworks) |

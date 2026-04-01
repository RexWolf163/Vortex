# Vortex SDK

**Namespace:** `Vortex.Sdk.*`
**Layer:** 3 (AppSDK)
**Packages:** 3 (GameCore, Quests, MiniGamesSystem)

---

## What is SDK

SDK is the third layer of Vortex architecture. If Core defines "how an application works" and Unity adapts it to the engine, then SDK answers the question **"what is a game"**.

This is where concepts absent from both Core and Unity appear: game sessions, winning and losing, quests, mini-games. These are not abstractions — they are concrete domain logic shared across a family of projects.

SDK relies on both lower layers. It uses `Singleton` and `ComplexModel` from Core, `Timer` and `MonoBehaviourSingleton` from Unity. But neither Core nor Unity knows anything about SDK — the dependency is strictly unidirectional.

---

## Why a Separate Layer

Game session logic could have been implemented directly in each project. But then every new project would start by copying the same `GameController`, the same states, the same pause-on-focus-loss behavior. SDK eliminates this duplication.

At the same time, SDK does not bind to a specific game. `GameController` manages states but has no idea what a "level" or "character" is. `QuestController` runs quests, but each quest's logic is defined in the project. `MiniGamesSystem` provides a mini-game lifecycle but does not dictate how it looks.

SDK is a contract: "in our projects, a game works like this." Everything unique to a specific product lives above, in the AppLocale layer.

---

## Architectural Role

### GameController as the Central Bus

`GameController` is the heart of SDK. It is a static singleton controller that owns `GameModel` — a composite model of all game data.

Any package can register its data in `GameModel` via the `IGameData` marker interface. Quests store `QuestModels` there, mini-games store `MiniGamesStatisticsData`. Access is through `GameController.Get<T>()`. This extends the Database bus pattern to the game session level.

### States as Language

Six `GameStates` — `Off`, `Play`, `Win`, `Fail`, `Paused`, `Loading` — permeate the entire SDK. UI display conditions (`GameStateCondition`), state switchers (`GameStateHandler`), mini-games (mirrored `MiniGameStates`) — everything is bound to this enumeration.

State changes only through the controller. UI observes but does not decide.

### Extensibility via Partial Classes

`GameController` and `QuestController` are partial classes. Packages extend them without modifying the main file. `QuestControllerExtEditor` adds editor logic, `QuestControllerExtIndex` adds index queries. The same principle as `SettingsModel` in Core.

---

## Contents

| Package | Assembly | Purpose |
|---------|----------|---------|
| **GameCore** | `ru.vortex.sdk.game.core` | Game session: states, pause, serialization, reactive data model |
| **Quests** | `ru.vortex.sdk.game.quests` | Quest system: conditions, async logic execution, autorun |
| **MiniGamesSystem** | `ru.vortex.sdk.minigames` + concrete games | Mini-game framework: Hub → Controller → Data → View |

---

## Layer Boundaries

SDK **defines**:
- What a game session is and how it is managed
- Quest lifecycle — from start conditions to completion
- Mini-game lifecycle — from configuration to statistics
- Extension contract via `IGameData` and partial classes

SDK **does not define**:
- Specific rules and mechanics of a project
- Quest content and logic
- Visual design and UI
- Resource and scene management (that is the Unity layer)

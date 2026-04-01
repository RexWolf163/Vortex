# Components

**Namespace:** `Vortex.Unity.Components`
**Assembly:** `ui.vortex.unity.components`

## Purpose

A set of ready-made MonoBehaviour components for common tasks: binding data to UI, scene management, declarative lifecycle callbacks, persistent containers.

Capabilities:
- Declarative binding of text, sprites, and actions to `UIComponent` via Inspector
- Language switching with active language visual indication
- Async scene loading and unloading with dropdown selection
- Lifecycle events (`Awake`, `OnDestroy`, `OnEnable`, `OnDisable`) via `UnityEvent` without code
- Persistent container with duplication protection across scene changes

Out of scope:
- Application loading logic (see `LoaderSystem/`)
- Business logic and controllers

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.AppSystem` | `App.GetState()`, `App.OnStart`, `AppStates` |
| `Vortex.Core.LocalizationSystem` | `Localization`, `Translate()`, `SetCurrentLanguage()` |
| `Vortex.Unity.UI.UIComponents` | `UIComponent`, `UIComponentText`, `UIComponentButton`, `UIComponentGraphic` |
| `Vortex.Unity.LocalizationSystem` | `[LocalizationKey]`, `[Language]` — key selection attributes |
| `Vortex.Unity.EditorTools` | `[AutoLink]`, `[UIComponentLink]` |
| Odin Inspector | `[Button]`, `[TitleGroup]`, `[ValueDropdown]` |

---

## Architecture

```
Components/
├── LoaderSystem/              # Launch and loading visualization (separate README)
├── Misc/
│   ├── LocalizationSystem/    # Data binding to UIComponent
│   │   ├── SetTextComponent       # Text (with localization)
│   │   ├── SetSpriteComponent     # Sprite
│   │   ├── SetActionComponent     # Action (UnityEvent → button)
│   │   └── SetLocaleHandler       # Language switcher
│   ├── MBHandlers/
│   │   └── MonoBehaviourEventsHandler  # Declarative lifecycle events
│   └── NotDestroyableSystemContainer   # DontDestroyOnLoad with duplication guard
└── SceneControllers/          # Scene loading/unloading
    ├── SceneHandler               # Abstract base
    ├── LoadSceneHandler           # Single/Additive loading
    └── UnloadSceneHandler         # Unloading
```

---

## Misc/LocalizationSystem — UIComponent Data Binding

Four components for declarative `UIComponent` configuration via Inspector. All support `position` — the target part index within `UIComponent` (`-1` uses the default part).

### SetTextComponent

Sets fixed or localized text. `[ExecuteInEditMode]` — text is visible in the editor without running.

| Field | Type | Description |
|-------|------|-------------|
| `key` | `string` | Localization key (`[LocalizationKey]` — dropdown) |
| `useLocalization` | `bool` | `true` — `key.Translate()`, `false` — raw string |
| `position` | `int` | `UIComponentText` index (-1 = default) |

Subscribes to `Localization.OnLocalizationChanged`, `Localization.OnInit`, `App.OnStart`. Updates automatically on language change.

### SetSpriteComponent

Sets a fixed sprite. One-time assignment on `OnEnable`.

| Field | Type | Description |
|-------|------|-------------|
| `sprite` | `Sprite` | Target sprite |
| `position` | `int` | `UIComponentGraphic` index (-1 = default) |

### SetActionComponent

Binds `UnityEvent` to a `UIComponent` button. On `OnDisable`, removes the action (sets `null`).

| Field | Type | Description |
|-------|------|-------------|
| `events` | `UnityEvent` | Click callbacks |
| `position` | `int` | `UIComponentButton` index (-1 = default) |

### SetLocaleHandler

Language switch button. On click, calls `Localization.SetCurrentLanguage()`. Displays the localized language name and an optional switcher indicator for the active language.

| Field | Type | Description |
|-------|------|-------------|
| `language` | `string` | Language code (`[Language]` — dropdown) |
| `useSwitch` | `bool` | Show `SwitcherState.On/Off` for active language |

---

## MonoBehaviourEventsHandler

Declarative MonoBehaviour lifecycle event binding via `UnityEvent` in Inspector.

| Field | Invoked in |
|-------|-----------|
| `onAwake` | `Awake()` |
| `onDestroy` | `OnDestroy()` |
| `onEnable` | `OnEnable()` |
| `onDisable` | `OnDisable()` |

Allows designers to configure lifecycle reactions without writing scripts.

---

## NotDestroyableSystemContainer

Persistent container (`DontDestroyOnLoad`) with key-based duplication protection.

| Field | Type | Description |
|-------|------|-------------|
| `key` | `string` | Unique container identifier |

On `Awake`, searches for all `NotDestroyableSystemContainer` instances in the scene. If another with the same `key` is found — destroys itself. Otherwise — calls `DontDestroyOnLoad`. Protects against duplicates when launching from an arbitrary scene in the editor.

---

## SceneControllers

Components for scene management via Inspector.

### SceneHandler (abstract)

Base class. The `sceneName` field with dropdown selection from Build Settings (`@DropDawnHandler.GetScenes()`). Abstract `Run()` method with `[Button]` for Inspector testing.

### LoadSceneHandler

Async scene loading. The `additiveMode` field switches between `LoadSceneMode.Single` and `LoadSceneMode.Additive`.

### UnloadSceneHandler

Async scene unloading by name.

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `SetTextComponent` / `SetActionComponent` without `UIComponent` | `LogError`, component does not function |
| `SetTextComponent` before `Localization` initialization | Empty string; updates on `Localization.OnInit` |
| `SetLocaleHandler` — current language matches `language` | Switcher = `On`, repeated `SetCurrentLanguage` call is harmless |
| `NotDestroyableSystemContainer` with empty `key` | `LogError`, but `DontDestroyOnLoad` is still called |
| Two `NotDestroyableSystemContainer` with same `key` | The second destroys itself in `Awake` |
| `position = -1` in Set components | Default part is used (first in the array) |

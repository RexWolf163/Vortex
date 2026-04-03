# Vortex Unity

**Namespace:** `Vortex.Unity.*`
**Platform:** Unity 2021.3+
**Files:** ~315 (.cs), 21 subsystems

---

## What the Unity Layer Is For

Core describes "what" — contracts, buses, models. The Unity layer answers "how" — implementing those contracts with engine tools: loading assets, rendering interfaces, playing audio, saving to disk.

But that's only half the job. The second — and more important — half is providing tools for **assembling behavior without code**.

### Philosophy: Tuning Without Code

The ideal workflow in Vortex looks like this: the programmer creates atomic components, the designer assembles behavior from them in the Inspector. No new C# for routine tasks — opening a window, playing a sound, switching states, loading a scene, launching a logic chain.

This isn't an abstract goal but a practical design principle. Every Handler in the Unity layer is a MonoBehaviour with one specific responsibility, configured via Inspector fields. `CallUIHandler` — opens UI by GUID from a dropdown. `AudioHandler` — plays a sound from a preset. `LoadSceneHandler` — loads a scene. `InputActionHandler` — routes input to UnityEvent. `LogicChainStarter` — launches a logic chain from a ScriptableObject.

None of these components require inheritance or writing code to use. They are **atomic** — each does exactly one thing. They are **composable** — complex behavior emerges from a set of simple components on a single GameObject. They are **configurable** — all parameters are available in the Inspector through typed dropdowns, toggle buttons, and conditional visibility.

### Interface as Container, Not Logic

UI in Vortex is treated as a container without its own decision-making logic. `UserInterface` is a MonoBehaviour that knows how to show and hide itself (via `TweenerHub`), but doesn't know why. All logic is formed as a set of autonomous components:

- `UIComponent` manages texts, buttons, sprites — but doesn't decide what to show
- `UIStateSwitcher` switches visual states — but doesn't decide when to switch
- `CallUIHandler` opens windows — but doesn't decide if they can be opened
- Conditions (`UserInterfaceCondition`) determine show eligibility — but don't render UI

Each element handles its atomic function. Composing these elements on a scene forms presentation behavior. The programmer creates a new `StateItem` or `UserInterfaceCondition` type — the designer combines them in the Inspector.

---

## What Lives Here

### System Drivers

Each Core system built on `SystemController<T, TD>` gets a driver here — a MonoBehaviour singleton implementing the `ISystemDriver` interface:

| Driver | Core System | What It Does |
|--------|-------------|-------------|
| `SettingsDriver` | `Settings` | Loads `SettingsPreset` from Resources, copies via Reflection |
| `SaveSystemDriver` | `SaveController` | PlayerPrefs + XML + GZip compression |
| `MappedParametersDriver` | `ParameterMaps` | Loads parameter maps from Resources |
| `AudioDriver` | `AudioController` | AudioSource, mixing, preset handling |
| `LocalizationDriver` | `Localization` | Loads locales, switches languages |
| `LogDriver` | `Log` | Routes to `Debug.Log` / `Debug.LogError` |

Drivers register automatically via `RuntimeInitializeOnLoadMethod` and `InitializeOnLoadMethod`. No manual `Init()` calls needed — the system starts on its own.

### Presets (ScriptableObject)

Presets are ScriptableObjects that serve as Database record configurations. Through `RecordPreset<T>` they receive a GUID and register in the bus on load. Each preset is a data-driven configuration that can be changed without recompilation:

- `UserInterfacePreset` — UI type, show conditions
- `LogicChainPreset` — logic chain steps
- `SoundSamplePreset` / `MusicSamplePreset` — audio clips with parameters
- `ParametersMapStorage` — parameter maps
- `TweenPreset` — animation curves, duration, easing
- `SavePreset` — save slot structure
- `SettingsPreset` — abstract base type for extensible settings

### Handlers (Atomic Components)

Handlers are the core of the component approach. Each Handler is a MonoBehaviour with no abstract logic, just one concrete function:

**UI and Navigation:**
- `CallUIHandler` — open/close/toggle UI by GUID
- `CallUIClose` — close current UI
- `UIDragHandler` — window dragging with Canvas bounds

**Audio:**
- `AudioHandler` — play sound (via AudioSource or global AudioPlayer)
- `MusicHandler` — music management
- `AudioSwitcher` — audio state switching
- `AudioValueSlider` — slider bound to volume

**Input:**
- `InputActionHandler` — route InputAction to UnityEvent (onPressed, onReleased)
- `InputMapHandler` — switch input maps
- `KeyboardHandler` — keyboard input routing

**Scenes and Lifecycle:**
- `LoadSceneHandler` — load scene (Single/Additive)
- `UnloadSceneHandler` — unload scene
- `MonoBehaviourEventsHandler` — UnityEvent wrappers for Awake/OnDestroy/OnEnable/OnDisable

**Logic and Data:**
- `LogicChainStarter` — launch logic chain from preset
- `SetLocaleHandler` — change language
- `SetTextComponent` — auto-localize text in UIComponent
- `SetSpriteComponent` — localized sprite

### UI Subsystems

**UIComponent** — modular UI element controller. Manages arrays of specialized `UIComponentPart`:

| Part | Purpose |
|------|---------|
| `UIComponentText` | Text (Text, TMP, TMP UGUI) |
| `UIComponentButton` | Buttons (Button, AdvancedButton) |
| `UIComponentGraphic` | Graphics (SpriteRenderer, Image) |
| `UIComponentSwitcher` | States (UIStateSwitcher) |

Uniform API: `SetText()`, `SetAction()`, `SetSprite()`, `SetSwitcher()`, `PutData()`. UIComponent is the single entry point for a controller that wants to configure a view. The controller calls `PutData(UIComponentData)` — UIComponent distributes data across its Parts.

**UIStateSwitcher** — visual state machine. Each state (`StateData`) contains an array of `StateItem` — polymorphic actions:

| StateItem | What It Does |
|-----------|-------------|
| `GameObjectsSwitch` | Toggles objects on/off |
| `ColorsSwitch` | Swaps colors |
| `SpritesSwitch` | Swaps sprites |
| `AnimatorBoolSwitch` | Controls Animator bool |
| `AnimatorStateSwitch` | Switches Animator layer state |
| `TweenerHubSwitch` | Triggers TweenerHub animations |
| `EventFire` | Invokes UnityEvent |

StateItem is an extensible type: the programmer creates a subclass and it automatically appears in the Inspector dropdown. The designer combines StateItems within states without writing a single line of code.

**TweenerSystem** — UniTask-based animations:

- `TweenerHub` — orchestrator: `TweenLogic` array, `Forward()` / `Back()` / `Pulse()` methods
- `TweenLogic` — abstract animation: `ColorLogic`, `CanvasOpacityLogic`, `RectScaleLogic`, `FillImageLogic`, `PivotLogic`
- `TweenPreset` — ScriptableObject: curve, duration, on/off points
- `AsyncTween` — standalone fluent API: `.Set().SetEase().OnComplete().Run()` for any float/Vector/Color

### DatabaseSystem (Extensions)

- `DbRecordAttribute` — typed record dropdown in Inspector. Instead of string GUIDs — selection from a filtered list
- `DatabaseSettings` — Addressable labels configuration
- `AddressablesDriver` / `ResourcesDriver` — record loading from Addressables or Resources

### EditorTools (~60 files)

Inspector customization package. Provides 20 attributes that configure field display without writing Editor code:

- `[AutoLink]` — auto-bind component when null
- `[OnChanged("Method")]` — callback on change
- `[ValueSelector("Method")]` — SearchablePopup from method
- `[ToggleButton]` — bool as styled button
- `[ToggleBox("field")]` — conditional field grouping
- `[Show]` / `[Hide]` / `[ShowInPlay]` / `[HideInEditor]` — conditional visibility
- `[ClassLabel("$Method")]` — custom collection element header
- `[InfoBubble("text")]` — information block
- `[VortexCollection]` — collection rendering with drag & drop, fold, context menus

EditorTools works with both native Inspector and Odin Inspector (via `#if ODIN_INSPECTOR`).

### InputBusSystem

Input routing. `InputController` — static controller, `InputSubscriber` — MonoBehaviour for Input Action Map registration. `InputActionHandler` binds a specific action to UnityEvent — no code, via Inspector dropdown.

### Components/Misc

Utility components:

- `MonoBehaviourEventsHandler` — declarative UnityEvents for MonoBehaviour lifecycle
- `LoaderStarter` — entry point for `Loader.Run()` on scene
- `SetLocaleHandler` — language switching
- `SetTextComponent` / `SetSpriteComponent` / `SetActionComponent` — localized components

---

## How It All Works Together

A typical UI screen is assembled from ready-made components:

1. **UserInterface** on the root object — manages show/hide via TweenerHub
2. **UIComponent** on each element — provides a unified API for the controller
3. **UIStateSwitcher** for visual states — "active / inactive / selected"
4. **CallUIHandler** on a button — opens another window on press
5. **AudioHandler** alongside — plays sound on interaction
6. **SetTextComponent** on text fields — automatic localization
7. **MonoBehaviourEventsHandler** — additional lifecycle reactions

None of these connections require C#. Everything is configured through the Inspector, with typed dropdowns and conditional field visibility. New code appears only when a fundamentally new behavior type is needed — a new `StateItem`, a new `UserInterfaceCondition`, a new `TweenLogic`.

---

## Layer Boundaries

Unity **does**:
- Implements driver interfaces from Core (`ISystemDriver`, `IDriver`)
- Provides atomic components for assembling behavior in the Inspector
- Loads assets (Resources, Addressables)
- Manages MonoBehaviour lifecycle
- Renders and animates UI
- Handles input, audio, scenes

Unity **does not**:
- Contain domain logic for a specific project
- Make decisions for the controller
- Store data — that's in the Database bus (Core)
- Depend on AppSDK or AppLocale layers

Domain logic lives in the layers above. Unity is a universal toolkit, identical across all Vortex projects.

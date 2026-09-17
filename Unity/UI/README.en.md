# UI

The framework's UI layer. Everything visual: interface components, animations, state machine, object pool, buttons, and utilities.

This section does not manage interface lifecycles — that's `UIProviderSystem`'s job. Here are only the building blocks: what the interface is made of and how it moves, blinks, and switches.

## What's Inside

**UIComponents** — a modular system where a single `UIComponent` manages arrays of typed parts: text, button, graphic, switcher. A unified API (`PutData`, `SetText`, `SetSprite`, `SetAction`) instead of manual work with each component. Supports Text, TMP, Image, SpriteRenderer, Button, AdvancedButton.

**TweenerSystem** — UniTask-based animations. Two modes. Scene-bound — `TweenerHub` on a scene object with a `TweenLogic` array (color, opacity, scale, fill, pivot), Forward/Back/Pulse. Standalone — `AsyncTween` fluent API for one-off code-driven animations, with shortcuts for Move, Scale, Fade, Color. 16 easing types. `StateView<TEnum>` — a field-level state switcher on tweeners: a hub per enum value, with a state table and automatic hub creation in the inspector.

**StateSwitcher** — state machine. `UIStateSwitcher` switches named states, each containing a set of `StateItem`: toggle GameObjects, change color (animated or instant), swap sprites, trigger Animator, run TweenerHub, fire UnityEvent.

**PoolSystem** — pool with data keys. `Pool` creates, reuses, and deactivates `PoolItem` instances. Elements are never destroyed — they are disabled and returned to the queue.

**Misc** — utilities. `AdvancedButton` with four click modes and built-in scroll-drag protection via `IPointerClickHandler`. `CounterViewBase`/`CounterViewAdvanced` for counters with thresholds and pulse. `SliderView` with smooth movement. `DataStorage` as a universal container. `DropDown` — Pool-based dropdown list with sorting, deduplication, and scroll-positioning. `AutoRectSetter`, `EnableDelayForChild`, `ScrollRectResetHandler`.

**CursorSystem** — custom system cursor. Default sprite, separate LMB/RMB sprites, an array of hover variants per UI zone. Unified API through `MouseHoverListener` on UGUI objects plus the public `CursorController.OnHover/OnUnHover` for non-UGUI sources. Alt-tab protection out of the box (via Input System soft reset).

**RollbackSystem** — rolling back a screen's unsaved changes. `RollbackHandler` combines rollback sources (`RollbackSource` descendants), shows a change flag through `UIStateSwitcher` and drives two paths — "Save" and "Rollback"; disabling the screen means rollback. Reactive, no per-frame checks. A ready source covers dropdowns and sliders with a `RollbackControl` marker on the control.

**UIBuilder** — an editor layout tool. Right-click in the Hierarchy → `Vortex Primitives/Create Text` / `Create Button`: a layer with `UIComponent`, an instance of the chosen primitive and the required `Set*Component`s is created under the object. The primitive catalog is a folder set in `Project Settings → Vortex/UIBuilder`; window parameters depend on the primitive's parts. New element kinds and sections are added without touching the core.

## Hotkeys

Commands for selected objects in the scene or an open prefab. Assets in the Project window are not affected; undone in one step; new layers are selected.

| Shortcut | Menu | Action |
|----------|------|--------|
| `Alt+T` / `Ctrl+Alt+T` | `Tools/Vortex/UI/Add TweenerHub` / `… Layer` | `TweenerHub` on the object or as a separate layer — see `TweenerSystem/` |
| `Alt+S` / `Ctrl+Alt+S` | `Tools/Vortex/UI/Add UIStateSwitcher` / `… Layer` | `UIStateSwitcher` on the object or as a separate layer — see `StateSwitcher/` |
| `Alt+I` | `Tools/Vortex/UI/Add BackgroundLayer` | A child `Background` layer with an `Image` (color from `Project Settings → Vortex/UIBuilder`, black by default; Maskable off, Raycast Target on): first in the hierarchy, `RectTransform` stretched to the parent. Pressing again adds another layer — see `UIBuilder/` |

Menu items — `UIBuilder/Shortcuts/`; adding components and creating layers is shared code in `EditorTools/HierarchyTools/HierarchyLayers.cs`.

## Dependencies

UniTask, TextMeshPro, Odin Inspector. From the framework — `TimeController`, `ActionExt`, `IDataStorage`, `EditorTools`, `SettingsSystem` (for `CursorSystem`).

## Subsystem Documentation

Each subsystem is documented separately:

- `UIComponents/` — modular UI components
- `TweenerSystem/` — animations
- `StateSwitcher/` — state machine
- `PoolSystem/` — object pool
- `Misc/` — utility components
- `CursorSystem/` — custom cursor
- `RollbackSystem/` — screen change rollback
- `UIBuilder/` — creating UI layers from primitives (Editor)

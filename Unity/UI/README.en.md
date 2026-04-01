# UI

The framework's UI layer. Everything visual: interface components, animations, state machine, object pool, buttons, and utilities.

This section does not manage interface lifecycles — that's `UIProviderSystem`'s job. Here are only the building blocks: what the interface is made of and how it moves, blinks, and switches.

## What's Inside

**UIComponents** — a modular system where a single `UIComponent` manages arrays of typed parts: text, button, graphic, switcher. A unified API (`PutData`, `SetText`, `SetSprite`, `SetAction`) instead of manual work with each component. Supports Text, TMP, Image, SpriteRenderer, Button, AdvancedButton.

**TweenerSystem** — UniTask-based animations. Two modes. Scene-bound — `TweenerHub` on a scene object with a `TweenLogic` array (color, opacity, scale, fill, pivot), Forward/Back/Pulse. Standalone — `AsyncTween` fluent API for one-off code-driven animations, with shortcuts for Move, Scale, Fade, Color. 16 easing types.

**StateSwitcher** — state machine. `UIStateSwitcher` switches named states, each containing a set of `StateItem`: toggle GameObjects, change color (animated or instant), swap sprites, trigger Animator, run TweenerHub, fire UnityEvent.

**PoolSystem** — pool with data keys. `Pool` creates, reuses, and deactivates `PoolItem` instances. Elements are never destroyed — they are disabled and returned to the queue.

**Misc** — utilities. `AdvancedButton` with four click modes and visual states. `CounterView` for animated counters. `SliderView` with smooth movement. `DataStorage` as a universal container. `DropDown` — Pool-based dropdown list with sorting, deduplication, and scroll-positioning. `AutoRectSetter`, `EnableDelayForChild`, `ScrollRectResetHandler`.

## Dependencies

UniTask, TextMeshPro, Odin Inspector. From the framework — `TimeController`, `ActionExt`, `IDataStorage`, `EditorTools`.

## Subsystem Documentation

Each subsystem is documented separately:

- `UIComponents/` — modular UI components
- `TweenerSystem/` — animations
- `StateSwitcher/` — state machine
- `PoolSystem/` — object pool
- `Misc/` — utility components

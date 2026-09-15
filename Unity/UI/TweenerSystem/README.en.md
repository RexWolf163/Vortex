# TweenerSystem

**Namespace:** `Vortex.Unity.UI.TweenerSystem`
**Assembly:** `ru.vortex.unity.ui.misc`

## Purpose

UniTask-based animation system. Two modes: scene-bound (`TweenerHub` + `TweenLogic`) and standalone (`AsyncTween` fluent API). On top of the scene-bound mode — `StateView<TEnum>`: a state switcher where each enum value has its own `TweenerHub`.

Capabilities:
- Scene-bound animations: Forward / Back / Pulse with offset and switch-point support
- `StateView<TEnum>` — states on tweeners: the selected one goes Forward, the others Back; in the inspector — a state table and automatic hub creation (Sync)
- `Alt+T` / `Ctrl+Alt+T` hotkeys — add a `TweenerHub` to an object or as a separate layer
- 5 built-in TweenLogic types: color, CanvasGroup opacity, scale, fillAmount, pivot
- Standalone fluent API for one-off code-driven animations
- 16 easing types + AnimationCurve support
- Shortcuts for common operations (Move, Scale, Fade, Color, Slider)

Out of scope:
- Skeletal/sprite animations (Animator, DOTween)
- Physics animations

## Dependencies

| Dependency | Purpose |
|------------|---------|
| UniTask | Async animations (`CancellationToken`, `UniTask.Yield`) |
| `Vortex.Core.SettingsSystem` | `Settings.Data()` — debug flags |
| `Vortex.Unity.AppSystem` | `TimeController.Accumulate()` — call accumulation |
| `Vortex.Unity.EditorTools` | `[ClassLabel]` |
| Odin Inspector | `[ShowInInspector]`, `[MinValue]`, `[MaxValue]`; `OdinValueDrawer` — the `StateView` header |
| TextMeshPro | Support in `ColorLogic` |

---

## Architecture

```
TweenerSystem/
├── TweenerHub.cs               # Scene-bound controller (MonoBehaviour)
├── TweenLogic.cs               # Abstract animation base
├── TweenPreset.cs              # ScriptableObject: curve, duration, switch flags
├── StateView.cs                # StateView<TEnum> + StateViewBase: a TweenerHub per enum state
├── TweenLogics/
│   ├── ColorLogic.cs           # Color: Image, Text, TMP, SpriteRenderer
│   ├── CanvasOpacityLogic.cs   # CanvasGroup opacity + blocksRaycasts
│   ├── RectScaleLogic.cs       # RectTransform scale (Both/X/Y)
│   ├── FillImageLogic.cs       # Image fillAmount
│   └── PivotLogic.cs           # RectTransform pivot
├── UniTaskTweener/
│   ├── AsyncTween.cs           # Standalone fluent API
│   ├── AsyncTweenExtensions.cs # Shortcuts (Move, Scale, Fade...)
│   └── Easing.cs               # 16 easing types
├── Editor/
│   ├── StateViewDrawer.cs      # StateView state header, Sync button (Odin)
│   └── TweenerHubShortcut.cs   # Alt+T / Ctrl+Alt+T
└── Debug/
    ├── Model/SettingsModelExtAsyncTweener.cs
    └── Presets/DebugSettingsExtAsyncTweener.cs
```

---

## Scene-Bound Mode

### TweenerHub (MonoBehaviour)

Manages an array of `TweenLogic`. Attached to a scene GameObject.

```csharp
tweenerHub.Forward();       // play forward
tweenerHub.Back();          // play backward
tweenerHub.Pulse();         // toggle direction
tweenerHub.Forward(true);   // instant transition (skip)
```

Lifecycle:
- `Awake` — initializes all TweenLogic (`Init()`)
- `OnEnable` — resumes queued animations
- `OnDisable` — removed from `TimeController` queue
- `OnDestroy` — `DeInit()` on all TweenLogic

### TweenLogic (abstract, Serializable)

Base animation class. Subclasses implement `SetValue(float value)` for 0→1 interpolation.

| Field | Type | Description |
|-------|------|-------------|
| `preset` | `TweenPreset` | Curve, duration, switch flags |
| `offset` | `float` | Forward delay (seconds) |
| `offsetBack` | `float` | Back delay (seconds) |

Abstract methods:

| Method | Description |
|--------|-------------|
| `SetValue(float)` | Apply interpolated value |
| `SwitchOn()` | Activate visual element |
| `SwitchOff()` | Deactivate visual element |
| `OnStart()` | Animation start callback |
| `OnEnd()` | Animation end callback |

State: `_isForward`, `_progress` (0..1), `_cts` (CancellationTokenSource). Supports mid-animation direction change with elapsed time recalculation.

### TweenPreset (ScriptableObject)

| Field | Type | Description |
|-------|------|-------------|
| `curve` | `AnimationCurve` | Easing curve |
| `duration` | `float` | Duration (0..5 sec) |
| `offOnStartPoint` | `bool` | Deactivate element at point 0 |
| `offOnEndPoint` | `bool` | Deactivate element at point 1 |

### TweenLogic Implementations

| Class | Target | What It Animates |
|-------|--------|-----------------|
| `ColorLogic` | Image[], Text[], TMP[], SpriteRenderer[] | Color (Lerp start→end) |
| `CanvasOpacityLogic` | CanvasGroup[] | `alpha`; `blocksRaycasts` managed asymmetrically (start ≠ end) |
| `RectScaleLogic` | RectTransform[] | `localScale` (Both / X only / Y only mode) |
| `FillImageLogic` | Image[] | `fillAmount` |
| `PivotLogic` | RectTransform | `pivot` (Vector2 Lerp startPos→endPos) |

### StateView&lt;TEnum&gt; (field class)

A state switcher on tweeners: each enum value has its own `TweenerHub`. The selected state's hub goes `Forward`, the others go `Back`. An alternative to `UIStateSwitcher` when the states are animations.

```csharp
public enum GalleryState { Off, NaniCutscene, Image }

[SerializeField] private StateView<GalleryState> playView;

playView.Set(GalleryState.Image);       // Image — Forward, Off and NaniCutscene — Back
playView.Set(GalleryState.Off, true);   // no animation
var current = playView.State;
playView.Apply();                       // bring hubs to the current state
```

| Member | Description |
|--------|-------------|
| `Set(TEnum value, bool skip = false)` | Set the state: the selected hub goes `Forward`, the others `Back`; `skip` — no animation |
| `State` | Current state |
| `Apply(bool skip = false)` | Bring hubs to the current state — e.g. from the owner's `OnEnable` |

Design:

- A `[Serializable]` class — a field of any MonoBehaviour; `state` (current state) and `hubs` (the hubs) are serialized.
- Hubs follow the order of enum values (`Enum.GetValues`), not their numbers: enums with gaps (`A = 0, B = 5`) work.
- The shared part is the non-generic base `StateViewBase`: a single drawer serves a field with any enum.
- The field has no lifecycle. On enable the hubs return to their last position by themselves (initially `Back`); to bring them to the saved `state`, the owner calls `Apply()`.
- An empty array element is skipped.

Inspector (`StateViewDrawer`, Odin):

- above the field — a state table, as with `[StateSwitcher]`: index, caption (from the enum value's `Tooltip` / `LabelText`, otherwise its name), the assigned hub or `[None]`; the active state is highlighted;
- clicking a row switches the state with Undo; outside Play Mode the hubs switch instantly;
- **Sync** — fits the array length to the number of states and creates, for every empty state, a child layer of the owner `[{field}_{state}_Tween]` with a `TweenerHub`, like `Ctrl+Alt+T`. Layers are ordered as in the enum; everything is undone in one step. A prefab asset in the Project window is not synced — open the prefab.

### Hotkeys

For selected scene objects or objects of an open prefab (`Editor/TweenerHubShortcut.cs`):

| Shortcut | Menu | Action |
|----------|------|--------|
| `Alt+T` | `Tools/Vortex/UI/Add TweenerHub` | Add a `TweenerHub` to the object itself; skipped where one already exists |
| `Ctrl+Alt+T` | `Tools/Vortex/UI/Add TweenerHub Layer` | A child layer `[TweenerHub]` with a hub: first in the hierarchy, at the zero point, on a plain `Transform` (`RectTransform` is removed); new layers are selected |

Undone in one step. Layer creation is shared code in `UI/Shortcuts/ComponentShortcuts.cs`; the `StateView` Sync uses it too.

---

## Standalone Mode (AsyncTween)

**Namespace:** `Vortex.Unity.UI.TweenerSystem.UniTaskTweener`

Fluent API for one-off animations without scene dependency:

```csharp
new AsyncTween()
    .Set(() => transform.localScale, v => transform.localScale = v, Vector3.one, 0.3f)
    .SetEase(EaseType.OutBack)
    .OnComplete(() => Debug.Log("Done"))
    .OnUpdate(progress => { })
    .Run();
```

| Method | Description |
|--------|-------------|
| `Set(getter, setter, target, duration)` | Configuration (float, Vector2, Vector3, Color) |
| `SetEase(EaseType)` / `SetEase(AnimationCurve)` | Animation curve |
| `OnComplete(Action)` | Completion callback (not called on `Kill`) |
| `OnKill(Action)` | Callback on cancellation via `Kill` (not called on normal completion) |
| `OnUpdate(Action<float>)` | Per-frame callback (progress 0..1) |
| `SetToken(CancellationToken)` | External cancellation token |
| `Run()` | Start (returns self for chaining). Parameters reset after launch |
| `Kill()` | Cancel animation, invokes `OnKill` |

Properties: `Progress` (float 0..1), `IsPlaying` (bool).

### Shortcuts (AsyncTweenExtensions)

```csharp
new AsyncTween().SetLocalMove(transform, targetPos, 0.5f).Run();
new AsyncTween().SetMove(transform, worldPos, 0.5f).Run();
new AsyncTween().SetScale(transform, Vector3.zero, 0.2f).SetEase(EaseType.InBack).Run();
new AsyncTween().SetFade(canvasGroup, 0f, 0.3f).Run();
new AsyncTween().SetColor(graphic, Color.red, 0.4f).Run();
new AsyncTween().SetSlider(slider, 0.75f, 0.5f).Run();
new AsyncTween().SetSize(rectTransform, newSize, 0.3f).Run();
new AsyncTween().SetAnchoredMove(rectTransform, anchoredPos, 0.4f).Run();
new AsyncTween().SetPivot(rectTransform, newPivot, 0.3f).Run();
```

### Easing

16 types: Linear, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic, InBack, OutBack, InOutBack, InElastic, OutElastic, InOutElastic, InBounce, OutBounce, InOutBounce.

---

## Debug

`Settings.Data().AsyncTweenerDebugMode` — enabled in the `DebugSettings` asset (`asyncTweenerLogs` toggle).

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Direction change mid-animation | Correct elapsed time recalculation |
| `AsyncTween` with `duration ≤ 0` | Instant apply + `OnComplete` |
| `TweenerHub` on `OnDisable` | Removed from `TimeController` queue |
| `Forward(skip: true)` | Instant transition without animation |
| `Pulse()` during animation | Pulse queued after current tween |
| `CanvasOpacityLogic` Forward→Back | `blocksRaycasts` managed asymmetrically |
| `Kill()` on `AsyncTween` | Cancelled via `CancellationTokenSource`, `OnComplete` not called, `OnKill` invoked |
| `Run()` after `Set()` | Fluent chain parameters reset (`ResetParams`), but `OnKill` preserved until completion or next `Kill` |
| Re-`Run()` without `Set()` | Instant apply (`duration = 0`), `OnComplete` called |
| A `StateView` state has no hub | `Set` / `Apply` skip the empty element |
| A value removed from the `StateView` enum | Sync shortens the array; the removed state's layer stays in the hierarchy — delete it manually |
| Sync on a prefab asset in the Project window | Error logged: layers are created only in a scene or an open prefab |

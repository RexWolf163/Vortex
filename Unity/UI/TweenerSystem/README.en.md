# TweenerSystem

**Namespace:** `Vortex.Unity.UI.TweenerSystem`
**Assembly:** `ru.vortex.unity.ui.misc`

## Purpose

UniTask-based animation system. Two modes: scene-bound (`TweenerHub` + `TweenLogic`) and standalone (`AsyncTween` fluent API).

Capabilities:
- Scene-bound animations: Forward / Back / Pulse with offset and switch-point support
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
| `Vortex.Unity.EditorTools` | `[InfoBubble]`, `[ClassLabel]` |
| Odin Inspector | `[ShowInInspector]`, `[MinValue]`, `[MaxValue]` |
| TextMeshPro | Support in `ColorLogic` |

---

## Architecture

```
TweenerSystem/
├── TweenerHub.cs               # Scene-bound controller (MonoBehaviour)
├── TweenLogic.cs               # Abstract animation base
├── TweenPreset.cs              # ScriptableObject: curve, duration, switch flags
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
| `OnComplete(Action)` | Completion callback |
| `OnUpdate(Action<float>)` | Per-frame callback (progress 0..1) |
| `SetToken(CancellationToken)` | External cancellation token |
| `Run()` | Start (returns self for chaining) |
| `Kill()` | Cancel animation |

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
| `Kill()` on `AsyncTween` | Cancelled via `CancellationTokenSource`, `OnComplete` not called |

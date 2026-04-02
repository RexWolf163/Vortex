# TweenerSystem

**Namespace:** `Vortex.Unity.UI.TweenerSystem`
**Сборка:** `ru.vortex.unity.ui.misc`

## Назначение

Система анимаций на базе UniTask. Два режима: сценарный (`TweenerHub` + `TweenLogic`) и standalone (`AsyncTween` fluent API).

Возможности:
- Сценарные анимации: Forward / Back / Pulse с поддержкой offset и switch-точек
- 5 готовых TweenLogic: цвет, прозрачность CanvasGroup, масштаб, fillAmount, pivot
- Standalone fluent API для одноразовых анимаций из кода
- 16 типов easing + поддержка AnimationCurve
- Шорткаты для типовых операций (Move, Scale, Fade, Color, Slider)

Вне ответственности:
- Skeletal/sprite-анимации (Animator, DOTween)
- Анимации физики

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| UniTask | Асинхронные анимации (`CancellationToken`, `UniTask.Yield`) |
| `Vortex.Core.SettingsSystem` | `Settings.Data()` — debug-флаги |
| `Vortex.Unity.AppSystem` | `TimeController.Accumulate()` — аккумуляция вызовов |
| `Vortex.Unity.EditorTools` | `[InfoBubble]`, `[ClassLabel]` |
| Odin Inspector | `[ShowInInspector]`, `[MinValue]`, `[MaxValue]` |
| TextMeshPro | Поддержка в `ColorLogic` |

---

## Архитектура

```
TweenerSystem/
├── TweenerHub.cs               # Сценарный контроллер (MonoBehaviour)
├── TweenLogic.cs               # Абстрактная база анимации
├── TweenPreset.cs              # ScriptableObject: curve, duration, switch-флаги
├── TweenLogics/
│   ├── ColorLogic.cs           # Цвет: Image, Text, TMP, SpriteRenderer
│   ├── CanvasOpacityLogic.cs   # Прозрачность CanvasGroup + blocksRaycasts
│   ├── RectScaleLogic.cs       # Масштаб RectTransform (Both/X/Y)
│   ├── FillImageLogic.cs       # fillAmount Image
│   └── PivotLogic.cs           # pivot RectTransform
├── UniTaskTweener/
│   ├── AsyncTween.cs           # Standalone fluent API
│   ├── AsyncTweenExtensions.cs # Шорткаты (Move, Scale, Fade...)
│   └── Easing.cs               # 16 типов easing
└── Debug/
    ├── Model/SettingsModelExtAsyncTweener.cs
    └── Presets/DebugSettingsExtAsyncTweener.cs
```

---

## Сценарный режим

### TweenerHub (MonoBehaviour)

Управляет массивом `TweenLogic`. Привязывается к GameObject на сцене.

```csharp
tweenerHub.Forward();       // воспроизведение вперёд
tweenerHub.Back();          // воспроизведение назад
tweenerHub.Pulse();         // переключение направления
tweenerHub.Forward(true);   // мгновенный переход (skip)
```

Жизненный цикл:
- `Awake` — инициализация всех TweenLogic (`Init()`)
- `OnEnable` — возобновление отложенных анимаций
- `OnDisable` — снятие из очереди `TimeController`
- `OnDestroy` — `DeInit()` всех TweenLogic

### TweenLogic (abstract, Serializable)

Базовый класс анимации. Наследники реализуют `SetValue(float value)` для интерполяции 0→1.

| Поле | Тип | Описание |
|------|-----|----------|
| `preset` | `TweenPreset` | Кривая, длительность, switch-флаги |
| `offset` | `float` | Задержка Forward (секунды) |
| `offsetBack` | `float` | Задержка Back (секунды) |

Абстрактные методы:

| Метод | Описание |
|-------|----------|
| `SetValue(float)` | Применение интерполированного значения |
| `SwitchOn()` | Активация визуального элемента |
| `SwitchOff()` | Деактивация визуального элемента |
| `OnStart()` | Callback начала анимации |
| `OnEnd()` | Callback завершения анимации |

Состояние: `_isForward`, `_progress` (0..1), `_cts` (CancellationTokenSource). Поддерживает смену направления во время воспроизведения с пересчётом elapsed time.

### TweenPreset (ScriptableObject)

| Поле | Тип | Описание |
|------|-----|----------|
| `curve` | `AnimationCurve` | Кривая easing |
| `duration` | `float` | Длительность (0..5 сек) |
| `offOnStartPoint` | `bool` | Деактивировать элемент в точке 0 |
| `offOnEndPoint` | `bool` | Деактивировать элемент в точке 1 |

### Реализации TweenLogic

| Класс | Цель | Что анимирует |
|-------|------|--------------|
| `ColorLogic` | Image[], Text[], TMP[], SpriteRenderer[] | Цвет (Lerp start→end) |
| `CanvasOpacityLogic` | CanvasGroup[] | `alpha`; `blocksRaycasts` управляется асимметрично (start ≠ end) |
| `RectScaleLogic` | RectTransform[] | `localScale` (режим Both / X only / Y only) |
| `FillImageLogic` | Image[] | `fillAmount` |
| `PivotLogic` | RectTransform | `pivot` (Vector2 Lerp startPos→endPos) |

---

## Standalone режим (AsyncTween)

**Namespace:** `Vortex.Unity.UI.TweenerSystem.UniTaskTweener`

Fluent API для одноразовых анимаций без привязки к сцене:

```csharp
new AsyncTween()
    .Set(() => transform.localScale, v => transform.localScale = v, Vector3.one, 0.3f)
    .SetEase(EaseType.OutBack)
    .OnComplete(() => Debug.Log("Done"))
    .OnUpdate(progress => { })
    .Run();
```

| Метод | Описание |
|-------|----------|
| `Set(getter, setter, target, duration)` | Конфигурация (float, Vector2, Vector3, Color) |
| `SetEase(EaseType)` / `SetEase(AnimationCurve)` | Кривая анимации |
| `OnComplete(Action)` | Callback завершения (не вызывается при `Kill`) |
| `OnKill(Action)` | Callback при прерывании через `Kill` (не вызывается при нормальном завершении) |
| `OnUpdate(Action<float>)` | Callback каждого кадра (progress 0..1) |
| `SetToken(CancellationToken)` | Внешний токен отмены |
| `Run()` | Запуск (возвращает self для цепочки). Параметры сбрасываются после запуска |
| `Kill()` | Отмена анимации, вызывает `OnKill` |

Свойства: `Progress` (float 0..1), `IsPlaying` (bool).

### Шорткаты (AsyncTweenExtensions)

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

16 типов: Linear, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic, InBack, OutBack, InOutBack, InElastic, OutElastic, InOutElastic, InBounce, OutBounce, InOutBounce.

---

## Debug

`Settings.Data().AsyncTweenerDebugMode` — включается в ассете `DebugSettings` (toggle `asyncTweenerLogs`).

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Смена направления во время анимации | Корректный пересчёт elapsed time |
| `AsyncTween` с `duration ≤ 0` | Мгновенное применение + `OnComplete` |
| `TweenerHub` на `OnDisable` | Снимается из очереди `TimeController` |
| `Forward(skip: true)` | Мгновенный переход без анимации |
| `Pulse()` во время анимации | Очередь Pulse после текущего tween |
| `CanvasOpacityLogic` Forward→Back | `blocksRaycasts` управляется асимметрично |
| `Kill()` на `AsyncTween` | Отмена через `CancellationTokenSource`, `OnComplete` не вызывается, `OnKill` вызывается |
| `Run()` после `Set()` | Параметры fluent-цепочки сбрасываются (`ResetParams`), но `OnKill` сохраняется до завершения или следующего `Kill` |
| Повторный `Run()` без `Set()` | Мгновенное применение (`duration = 0`), `OnComplete` вызывается |

# TweenerSystem

**Namespace:** `Vortex.Unity.UI.TweenerSystem`
**Сборка:** `ru.vortex.unity.ui.misc`

## Назначение

Система анимаций на базе UniTask. Два режима: сценарный (`TweenerHub` + `TweenLogic`) и standalone (`AsyncTween` fluent API). Поверх сценарного — `StateView<TEnum>`: переключатель состояний, где каждое значение enum — свой `TweenerHub`.

Возможности:
- Сценарные анимации: Forward / Back / Pulse с поддержкой offset и switch-точек
- `StateView<TEnum>` — состояния на твинерах: выбранное — Forward, остальные — Back; в инспекторе — таблица состояний и автосоздание хабов (Sync)
- Горячие клавиши `Alt+T` / `Ctrl+Alt+T` — добавить `TweenerHub` на объект или отдельным слоем
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
| `Vortex.Unity.EditorTools` | `[ClassLabel]` |
| Odin Inspector | `[ShowInInspector]`, `[MinValue]`, `[MaxValue]`; `OdinValueDrawer` — шапка `StateView` |
| TextMeshPro | Поддержка в `ColorLogic` |

---

## Архитектура

```
TweenerSystem/
├── TweenerHub.cs               # Сценарный контроллер (MonoBehaviour)
├── TweenLogic.cs               # Абстрактная база анимации
├── TweenPreset.cs              # ScriptableObject: curve, duration, switch-флаги
├── StateView.cs                # StateView<TEnum> + StateViewBase: TweenerHub на каждое состояние enum
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
├── Editor/
│   ├── StateViewDrawer.cs      # Шапка состояний StateView, кнопка Sync (Odin)
│   ├── TweenerHubDrawer.cs     # Кнопки Back / Forward под полем TweenerHub (Odin)
│   └── TweenerHubShortcut.cs   # Alt+T / Ctrl+Alt+T
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

В инспекторе под любым полем `TweenerHub` — кнопки **Back** и **Forward** (`Editor/TweenerHubDrawer.cs`): проверить анимацию, не заходя на сам хаб. Вне Play Mode переключение мгновенное, в Play Mode — с анимацией; пока поле пустое, кнопки недоступны. Кнопки есть и у элементов массива хабов в `StateView`.

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

### StateView&lt;TEnum&gt; (поле-класс)

Переключатель состояний на твинерах: на каждое значение enum — свой `TweenerHub`. У выбранного состояния хаб в `Forward`, у остальных — в `Back`. Альтернатива `UIStateSwitcher`, когда состояния — это анимации.

```csharp
public enum GalleryState { Off, NaniCutscene, Image }

[SerializeField] private StateView<GalleryState> playView;

playView.Set(GalleryState.Image);       // Image — Forward, Off и NaniCutscene — Back
playView.Set(GalleryState.Off, true);   // без анимации
var current = playView.State;
playView.Apply();                       // привести хабы к текущему состоянию
```

| Член | Описание |
|------|----------|
| `Set(TEnum value, bool skip = false)` | Выставить состояние: хаб выбранного — `Forward`, остальные — `Back`; `skip` — без анимации |
| `State` | Текущее состояние |
| `Apply(bool skip = false)` | Привести хабы к текущему состоянию — например, из `OnEnable` владельца |

Устройство:

- `[Serializable]`-класс — поле любого MonoBehaviour; сериализуются `state` (текущее состояние) и `hubs` (хабы).
- Хабы идут по порядку значений enum (`Enum.GetValues`), а не по их числам: enum с пропусками (`A = 0, B = 5`) работает.
- Общая часть — неgeneric-основа `StateViewBase`: один drawer обслуживает поле с любым enum.
- Жизненного цикла у поля нет. При включении хабы сами возвращаются в последнее положение (изначально `Back`); чтобы привести их к сохранённому `state`, владелец вызывает `Apply()`.
- Пустой элемент массива пропускается.

Инспектор (`StateViewDrawer`, Odin):

- над полем — таблица состояний, как у `[StateSwitcher]`: номер, подпись (из `Tooltip` / `LabelText` значения enum, иначе имя), назначенный хаб или `[None]`; активное состояние подсвечено;
- клик по строке — переключение с Undo; вне Play Mode хабы переключаются мгновенно;
- **Sync** — подгоняет длину массива под число состояний и каждому пустому состоянию создаёт дочерний слой владельца `[{поле}_{состояние}_Tween]` с `TweenerHub`, как `Ctrl+Alt+T`. Слои выстраиваются в порядке enum, всё отменяется одним шагом Undo. Ассет префаба в Project не синхронизируется — префаб нужно открыть.

### Горячие клавиши

Для выделенных объектов сцены или открытого префаба (`Editor/TweenerHubShortcut.cs`):

| Сочетание | Меню | Действие |
|-----------|------|----------|
| `Alt+T` | `Tools/Vortex/UI/Add TweenerHub` | Добавить `TweenerHub` на сам объект; где он уже есть — пропуск |
| `Ctrl+Alt+T` | `Tools/Vortex/UI/Add TweenerHub Layer` | Дочерний слой `[TweenerHub]` с хабом: первым в иерархии, в нулевой точке, на обычном `Transform` (`RectTransform` снимается); новые слои выделяются |

Отмена — одним шагом Undo. Создание слоя — общий код `UI/Shortcuts/ComponentShortcuts.cs`; им же пользуется Sync у `StateView`.

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
| У состояния `StateView` нет хаба | `Set` / `Apply` пропускают пустой элемент |
| Значение удалено из enum `StateView` | Sync укорачивает массив; слой удалённого состояния остаётся в иерархии — удалить вручную |
| Sync на ассете префаба в Project | Ошибка в лог: слои создаются только в сцене или открытом префабе |

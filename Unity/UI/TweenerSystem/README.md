# TweenerSystem

Пакет UI-анимаций. Содержит две независимые системы: `TweenerHub` (декларативные двунаправленные анимации через инспектор) и `AsyncTween` (программные одноразовые анимации через Fluent API).

## Зависимости

- **Фреймворк:** `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController` (только TweenerHub)
- **Платформа:** UniTask (Cysharp), Odin Inspector (Sirenix), TextMeshPro, UnityEngine.UI

## Файловая структура

```
TweenerSystem/
├── DoTweenStarter.cs           # Пустой маркер (legacy)
├── TweenerHub.cs               # Система 1: MonoBehaviour-контроллер
├── TweenLogic.cs               # Система 1: абстрактная стратегия
├── TweenPreset.cs              # Система 1: ScriptableObject с параметрами
├── TweenLogics/
│   ├── CanvasOpacityLogic.cs   # Стратегия: CanvasGroup.alpha
│   ├── ColorLogic.cs           # Стратегия: цвет Image/Text/TMP
│   └── RectScaleLogic.cs       # Стратегия: RectTransform.localScale
└── UniTaskTweener/
    ├── AsyncTween.cs           # Система 2: Fluent API
    ├── AsyncTweenExtensions.cs # Система 2: extension-шорткаты
    └── Easing.cs               # Система 2: EaseType enum + 16 функций плавности
```

---
---

# Система 1: TweenerHub

Декларативная двунаправленная анимация UI-свойств, конфигурируемая через инспектор.

## Назначение

- Анимация свойств UI-элементов (alpha, цвет, масштаб) по кривой между двумя состояниями: начальное (0 / Back) и конечное (1 / Forward)
- Группировка нескольких анимаций в один хаб с синхронным запуском
- Мгновенное применение конечного состояния (skip) для инициализации и редактора
- Импульсная анимация (Forward → автоматический Back)

Система **не решает:** последовательные цепочки (sequence), анимацию по пути, задержку перед запуском (delay), повторения (loop), программную анимацию произвольных свойств из кода.

## Архитектура

```
TweenerHub (MonoBehaviour)
│   Управление lifecycle, публичный API
│   [SerializeReference] TweenLogic[]
│
├── TweenLogic (abstract, Serializable)
│   │   Машина состояний, UniTask-цикл, CTS lifecycle
│   │   [SerializeField] TweenPreset
│   │
│   ├── CanvasOpacityLogic    → CanvasGroup[].alpha
│   ├── ColorLogic            → Image[]/Text[]/TMP[].color
│   └── RectScaleLogic        → RectTransform[].localScale
│
└── TweenPreset (ScriptableObject)
    Кривая, длительность, флаги деактивации GameObject
```

`TweenerHub` работает только с абстракцией `TweenLogic` — конкретные стратегии подставляются через `[SerializeReference]` в инспекторе.

## Ключевые концепции

| Концепция | Описание |
|-----------|----------|
| **Двунаправленность** | Каждая анимация имеет два состояния: 0 (Back) и 1 (Forward). `AnimationCurve` из пресета читается от 0 к 1 при Forward, от 1 к 0 при Back. |
| **TweenPreset** | ScriptableObject: `curve` (AnimationCurve), `duration` (0–5 сек), `offOnStartPoint` / `offOnEndPoint` (деактивация GameObject в крайних точках). Один пресет переиспользуется между логиками. Создание: `Create > Tween Data Preset`. |
| **Skip** | Мгновенное применение конечного значения без анимации. Отменяет текущую анимацию. В редакторе вне Play Mode — всегда skip. |
| **Pulse** | Forward с автоматическим Back по завершении. Если хаб уже в Forward — сначала Back. |
| **Отложенный запуск** | При вызове без skip, запуск откладывается через `TimeController.Accumulate` до конца кадра. Если хаб отключен — запрос запоминается и выполнится при `OnEnable`. |
| **Реверс на лету** | При смене направления во время активной анимации пересчитывается `_startTween` — анимация разворачивается без перезапуска. |
| **Время через DateTime.UtcNow** | Анимация не зависит от `Time.timeScale`. Не останавливается при `Time.timeScale = 0`. |

## ⚠️ Критические требования

1. **TweenerHub не должен анимировать RectTransform на котором расположен.** Изменение собственного transform во время layout приведёт к рекурсии пересчёта.
2. **TweenPreset обязателен.** При `preset == null` — `NullReferenceException` в `Init()`.
3. **Целевые компоненты должны быть назначены.** Пустые массивы допустимы (no-op). `null`-элемент в массиве — `NullReferenceException`.
4. **Один TweenerHub — одна группа.** Все логики запускаются синхронно. Для независимого управления — отдельные хабы.

## Контракт

### Вход

| Параметр | Источник | Обязательность |
|----------|----------|----------------|
| `TweenPreset` | Инспектор, на каждую `TweenLogic` | Обязателен |
| Целевые компоненты | Инспектор (CanvasGroup[], Image[], RectTransform[] и т.п.) | Массив обязателен, может быть пустым |
| Управляющий вызов | `Forward()` / `Back()` / `Pulse()` из внешнего кода | — |

### Выход

Изменённые свойства целевых компонентов (alpha, color, localScale) и опционально `SetActive` на их GameObject. Возвращаемых значений нет.

### Гарантии

- `Awake` устанавливает все логики в состояние 0 (Back) синхронно
- `OnDestroy` отменяет все активные анимации и освобождает CancellationTokenSource
- `skip = true` применяет значение синхронно в текущем кадре
- Реверс во время анимации — плавный разворот без рывков
- Вне Play Mode все вызовы автоматически получают `skip = true`

### Ограничения

- Нет колбэка завершения анимации на уровне `TweenerHub`
- Нет delay, loop, sequence
- `offOnStartPoint` / `offOnEndPoint` оперируют `GameObject.SetActive` — затрагивают все компоненты на объекте

## API Reference

### TweenerHub

```csharp
public void Forward(bool skip = false)   // Анимация к состоянию 1
public void Back(bool skip = false)      // Анимация к состоянию 0
public void Pulse()                      // Forward → автоматический Back
```

### TweenPreset (ScriptableObject)

```csharp
public AnimationCurve curve       // Кривая прогресса 0→1
public float duration             // Длительность [0..5] сек
public bool offOnStartPoint       // SetActive(false) при достижении 0
public bool offOnEndPoint         // SetActive(false) при достижении 1
```

## Использование

### 1. Базовый сценарий

1. Добавить `TweenerHub` на GameObject
2. В массив `tweeners` добавить логику через `[SerializeReference]` (CanvasOpacityLogic / ColorLogic / RectScaleLogic)
3. Создать `TweenPreset` (`Create > Tween Data Preset`), настроить кривую и длительность
4. Назначить пресет и целевые компоненты

```csharp
[SerializeField] private TweenerHub tooltipHub;

public void ShowTooltip()  => tooltipHub.Forward();
public void HideTooltip()  => tooltipHub.Back();
public void FlashTooltip() => tooltipHub.Pulse();
```

> 💡 Для скрытия UI до первого показа — `offOnStartPoint = true` в пресете. Объект деактивируется в `Awake`.

### 2. Мгновенное применение

```csharp
tooltipHub.Forward(skip: true);
```

### 3. Создание собственной TweenLogic

```csharp
[Serializable]
public class FillAmountLogic : TweenLogic
{
    [SerializeField] private Image[] targets;

    protected override void SetValue(float value)
    {
        foreach (var img in targets)
            img.fillAmount = value;
    }

    protected override void SwitchOn()
    {
        foreach (var img in targets)
            img.gameObject.SetActive(true);
    }

    protected override void SwitchOff()
    {
        foreach (var img in targets)
            img.gameObject.SetActive(false);
    }
}
```

> 💡 Класс должен быть `[Serializable]`. `SetValue` получает значение 0..1 после применения кривой. `SwitchOn`/`SwitchOff` вызываются только при `offOnStartPoint`/`offOnEndPoint`.

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Повторный `Forward()` без `Back()` | Игнорируется (early return) |
| Повторный `Back()` без `Forward()` | Игнорируется (early return) |
| `Forward()` во время активного `Back()` | Анимация разворачивается на лету |
| `Forward(skip: true)` во время анимации | Текущая анимация отменяется, значение мгновенно = 1 |
| `Pulse()` в состоянии Forward | Сначала `Back()` |
| `Pulse()` во время Forward-анимации | Back начнётся по завершении Forward |
| Хаб отключен, вызван `Forward()` | Запоминается, выполнится при `OnEnable` |
| Хаб отключен, вызван `Forward(skip: true)` | Применяется мгновенно (skip не зависит от `isActiveAndEnabled`) |
| `preset == null` | `NullReferenceException` в `Init()` |
| `duration = 0` в пресете | Анимация завершается за один кадр |
| Пустой массив целей | No-op |
| `null` в массиве целей | `NullReferenceException` |

---
---

# Система 2: AsyncTween

Программная анимация произвольных свойств через Fluent API на UniTask.

## Назначение

- Анимация float, Vector2, Vector3, Color свойств любого объекта из кода
- Fluent-цепочка: настройка → параметры → запуск
- Отмена через `Kill()` или внешний `CancellationToken`

Система **не решает:** двунаправленные анимации (Forward/Back), декларативную настройку через инспектор, группировку анимаций, импульсы.

## Архитектура

```
AsyncTween
│   Fluent API, CTS lifecycle, цикл анимации
│
├── AsyncTweenExtensions (static)
│   Шорткаты: SetFade, SetMove, SetScale, SetColor...
│
├── Easing (static)
│   16 функций плавности
│
└── EaseType (enum)
    Linear, InQuad, OutQuad, ..., InOutBounce
```

Каждый экземпляр `AsyncTween` — один независимый поток анимации. Повторный `Run()` отменяет предыдущую анимацию того же экземпляра.

## Ключевые концепции

| Концепция | Описание |
|-----------|----------|
| **Fluent-цепочка** | `Set(...)` → `SetEase(...)` → `OnComplete(...)` → `Run()`. Параметры цепочки сбрасываются после `Run()` — экземпляр готов к повторной настройке. |
| **Getter/Setter** | Анимация читает текущее значение через getter в момент `Run()` и интерполирует к target. Начальное значение — то, что вернёт getter при старте. |
| **Время через Time.deltaTime** | В отличие от TweenerHub, AsyncTween использует `Time.deltaTime` — зависит от `Time.timeScale`. |
| **Kill** | `Kill()` отменяет анимацию. `OnComplete` не вызывается. `Kill(complete: true)` устанавливает `Progress = 1` (визуальное состояние не обновляется). |

## ⚠️ Критические требования

1. **`Set()` обязателен перед `Run()`.** Без вызова `Set()` — `Run()` будет no-op.
2. **Один экземпляр = один поток.** Повторный `Run()` отменяет предыдущую анимацию. Для параллельных анимаций — отдельные экземпляры.
3. **Ссылки на целевые объекты.** Если объект уничтожен во время анимации — `MissingReferenceException` в setter/getter.

## Контракт

### Вход

| Параметр | Метод | Обязательность |
|----------|-------|----------------|
| getter / setter / target / duration | `Set(...)` | Обязателен (один из перегрузок) |
| Функция плавности | `SetEase(...)` | По умолчанию `Linear` |
| Колбэк завершения | `OnComplete(...)` | Опционален |
| Колбэк покадровый | `OnUpdate(...)` | Опционален |
| Внешний токен | `SetToken(...)` | Опционален |

### Выход

Изменённое значение свойства через setter. `Progress` (0..1) доступен через свойство. `IsPlaying` — активна ли анимация.

### Гарантии

- При `duration ≤ 0` значение применяется мгновенно через setter, вызывается `OnComplete`
- При `Kill()` анимация останавливается, `OnComplete` не вызывается
- При отмене через внешний `CancellationToken` — `OnComplete` не вызывается
- Параметры цепочки сбрасываются после `Run()` — повторная настройка безопасна
- Начальное значение берётся из getter в момент `Run()`, а не в момент `Set()`

### Ограничения

- `Kill(complete: true)` обновляет только `Progress`, не вызывает setter — визуальное состояние остаётся на точке прерывания
- Нет встроенного delay, loop, sequence
- Нет привязки к lifecycle GameObject — ответственность за отмену при уничтожении лежит на вызывающем коде

## API Reference

### AsyncTween

```csharp
// Настройка анимации (вызвать один перед Run)
AsyncTween Set(Func<float> getter, Action<float> setter, float target, float duration)
AsyncTween Set(Func<Vector2> getter, Action<Vector2> setter, Vector2 target, float duration)
AsyncTween Set(Func<Vector3> getter, Action<Vector3> setter, Vector3 target, float duration)
AsyncTween Set(Func<Color> getter, Action<Color> setter, Color target, float duration)

// Параметры цепочки
AsyncTween SetEase(EaseType ease)
AsyncTween SetEase(AnimationCurve curve)
AsyncTween OnComplete(Action action)          // Не вызывается при Kill / Cancel
AsyncTween OnUpdate(Action<float> action)     // Аргумент — прогресс 0..1
AsyncTween SetToken(CancellationToken token)

// Запуск и остановка
AsyncTween Run()                              // duration ≤ 0 → мгновенное применение
void Kill(bool complete = false)              // complete=true → Progress = 1

// Состояние
float Progress { get; }
bool IsPlaying { get; }
```

### AsyncTweenExtensions

```csharp
AsyncTween SetLocalMove(Transform target, Vector3 to, float duration)
AsyncTween SetMove(Transform target, Vector3 to, float duration)
AsyncTween SetScale(Transform target, Vector3 to, float duration)
AsyncTween SetSlider(Slider slider, float to, float duration)
AsyncTween SetFade(CanvasGroup group, float to, float duration)
AsyncTween SetColor(Graphic graphic, Color to, float duration)
AsyncTween SetSize(RectTransform rect, Vector2 to, float duration)
AsyncTween SetAnchoredMove(RectTransform rect, Vector2 to, float duration)
AsyncTween SetPivot(RectTransform rect, Vector2 to, float duration)
```

### EaseType

```
Linear
InQuad / OutQuad / InOutQuad
InCubic / OutCubic / InOutCubic
InBack / OutBack / InOutBack
InElastic / OutElastic / InOutElastic
InBounce / OutBounce / InOutBounce
```

## Использование

### 1. Базовый сценарий

```csharp
private readonly AsyncTween _tween = new();

_tween
    .SetFade(canvasGroup, 0f, 0.3f)
    .SetEase(EaseType.OutCubic)
    .OnComplete(() => gameObject.SetActive(false))
    .Run();
```

### 2. Анимация произвольного свойства

```csharp
_tween
    .Set(
        () => slider.value,
        v  => slider.value = v,
        1f,
        0.5f
    )
    .SetEase(EaseType.InOutQuad)
    .Run();
```

### 3. Отмена через внешний токен

```csharp
var cts = new CancellationTokenSource();

_tween
    .SetLocalMove(transform, Vector3.zero, 0.5f)
    .SetToken(cts.Token)
    .Run();

// Отмена:
cts.Cancel(); // OnComplete не вызовется
```

### 4. Переиспользование экземпляра

```csharp
// Первая анимация
_tween.SetFade(group, 0f, 0.3f).SetEase(EaseType.OutQuad).Run();

// Позже — вторая (предыдущая отменяется автоматически)
_tween.SetFade(group, 1f, 0.2f).SetEase(EaseType.InQuad).Run();
```

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `Run()` без `Set()` | No-op: `animation == null` → `Kill()` + return |
| `duration ≤ 0` | Мгновенное применение target через setter, вызов `OnComplete` |
| Повторный `Run()` на том же экземпляре | Предыдущая анимация отменяется (`Kill()` внутри `ExecuteAsync`) |
| `Kill(complete: false)` | Анимация останавливается на текущем прогрессе |
| `Kill(complete: true)` | `Progress = 1`, setter не вызывается — визуальное состояние не обновляется |
| Целевой объект уничтожен во время анимации | `MissingReferenceException` в getter/setter |
| Внешний `CancellationToken` отменён | Анимация останавливается, `OnComplete` не вызывается |

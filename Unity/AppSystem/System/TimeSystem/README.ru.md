# TimeSystem

Диспетчер отложенных вызовов и источник времени приложения.

## Назначение

Централизованное управление отложенными вызовами, аккумуляция однотипных действий, управляемые таймеры.

- Отложенный вызов действий через заданный интервал
- Аккумуляция однотипных вызовов (batching)
- Управляемые таймеры с паузой и отменой
- Кеширование времени на кадр (`Date`, `Time`, `Timestamp`)
- Конвертация времени (Unix seconds, ticks → `DateTime`)

Вне ответственности: корутины, анимации, интерполяция (см. `TweenerSystem`).

## Зависимости

- `UnityEngine` — `MonoBehaviour`, `DontDestroyOnLoad`
- `Sirenix.OdinInspector` — отладочное отображение очередей

---

## TimeController

Центральный диспетчер отложенных вызовов. `MonoBehaviour`, создаётся автоматически через `[RuntimeInitializeOnLoadMethod]`.

### Архитектура

```
TimeController (MonoBehaviour, auto-create)
├── _queue           — Dictionary<object, QueuedAction>  (с владельцем, перезаписывается)
├── _anonymousQueue  — List<QueuedAction>                (без владельца, FIFO)
├── NextWaveQueue    — Dictionary<object, Action>         (Accumulate)
├── ReadyQueue       — List<Action>                       (буфер)
├── RemoveBuffer     — List<object>                       (буфер)
└── RemoveIndices    — List<int>                          (буфер)
```

Цикл обработки:

```
Update()      → TimeSync?.Invoke()
LateUpdate()  → SetTimeValue()
              → RunNextWave()          // Accumulate-батч
              → CheckQueue()           // каждые 0.1с (StepTime)
```

### Контракт

**Вход:**
- `Action` + опциональная задержка (`float stepSecs`) + опциональный владелец (`T owner where T : class`)

**Выход:**
- Вызов action по истечении задержки
- Кешированное время: `Date`, `Time`, `Timestamp`

**Гарантии:**
- Anonymous (без owner): FIFO порядок, не отменяется
- С owner: перезаписывает предыдущий вызов того же owner
- `Accumulate`: выполняется один раз за `LateUpdate`, сохраняется последний action
- Исключение в одном callback не блокирует остальные (`try/catch` + `Debug.LogError`)
- `_nextTimer` оптимизация: `CheckQueue` пропускается при отсутствии готовых к выполнению действий
- `Call(null, owner)` — удаляет pending-вызов owner из очереди

**Ограничения:**
- Гранулярность ~100мс (`StepTime`). При `stepSecs <= 0` проверка форсируется на текущем `LateUpdate`
- Owner ограничен `where T : class` — value-типы отсекаются на этапе компиляции

### Использование

#### Отложенные вызовы

```csharp
// Без владельца (FIFO, нельзя отменить)
TimeController.Call(() => Refresh());

// С задержкой, без владельца
TimeController.Call(() => Refresh(), 0.5f);

// С владельцем (перезаписывается, можно отменить)
TimeController.Call(() => Save(), this);
TimeController.Call(() => Save(), 2f, this);

// Отмена по владельцу
TimeController.RemoveCall(this);
```

> ⚠️ `Call` работает по wall-clock (`DateTime.UtcNow`) и **не знает про игровую паузу**:
> запланированный вызов сработает, даже если игра стоит на паузе или приложение в анфокусе
> (пауза проекта стейтовая, `Time.timeScale` не обнуляется). Для отложенных действий,
> которые должны «замерзать» вместе с игрой, используйте `Timer` —
> см. [Паттерн: паузируемое отложенное действие](#паттерн-паузируемое-отложенное-действие).

#### Аккумуляция

```csharp
// Множественные вызовы за кадр — выполнится только последний
TimeController.Accumulate(() => Sync(), this);
TimeController.Accumulate(() => Sync(), this);
// Sync() вызовется один раз в следующем LateUpdate
```

#### Время

```csharp
DateTime now    = TimeController.Date;        // UtcNow, кеш на кадр
double seconds  = TimeController.Time;         // секунды, точность 0.01
long unixMs     = TimeController.Timestamp;    // Unix milliseconds

DateTime local  = TimeController.DateFromSeconds(unixSec);
DateTime local  = TimeController.DateFromTicks(ticks);
```

#### Покадровые колбэки (FixedUpdate)

Помимо отложенных вызовов, `TimeController` ведёт реестр колбэков `FixUpdateIndex`, вызываемых каждый кадр на `FixedUpdate`:

```csharp
// Регистрация колбэка, вызываемого каждый FixedUpdate
TimeController.AddCallback(Tick);

// Снятие из реестра
TimeController.RemoveCallback(Tick);
```

Колбэки вызываются в `FixedUpdate` в порядке регистрации; исключение в одном колбэке изолируется (`try/catch` + `Debug.LogException`) и не блокирует остальные.

### Граничные случаи

- **StepTime (0.1с):** `CheckQueue` выполняется раз в ~100мс. При `stepSecs <= 0` форсируется проверка на текущем `LateUpdate`.
- **Буферы:** `ReadyQueue`, `RemoveBuffer`, `RemoveIndices` — статические, переиспользуемые, без GC-давления.
- **Timestamp при Date.Year <= 1:** возвращает `0` (защита от `DateTimeOffset` на неинициализированной дате).

---

## Timer

Управляемый таймер с поддержкой паузы. При создании автоматически регистрируется в `TimeController.Call` с `owner = this`.

### Архитектура

```
Timer (class)
├── End        — DateTime   (момент срабатывания, пересчитывается при Resume)
├── Duration   — TimeSpan   (полная длительность, неизменна)
├── Remains    — TimeSpan   (оставшееся, из DateTime.UtcNow; на паузе — зафиксированное)
├── IsComplete — bool       (true после срабатывания)
├── IsPaused   — bool       (true между SetPause и Resume)
└── → TimeController.Call(CallAction, seconds, this)
```

### Контракт

**Вход:**
- Длительность (`float` секунд, `TimeSpan`, или `DateTime` целевой момент) + callback `Action`

**Выход:**
- Вызов callback по истечении
- Состояние: `Remains`, `IsComplete`, `IsPaused`, `GetTimePassed()`

**Гарантии:**
- `SetPause`/`Resume` — no-op при `IsComplete`, повторной паузе или отсутствии паузы
- `Remains` вычисляется из `DateTime.UtcNow` (реальное время, не кеш кадра)
- Callback вызывается через `TimeController` — изоляция исключений

**Ограничения:**
- Метода отмены нет. Для отмены: `SetPause()` без `Resume()`
- Точность callback определяется `TimeController.StepTime` (~100мс)

### Использование

```csharp
// Создание
var timer = new Timer(5f, onComplete);
var timer = new Timer(TimeSpan.FromMinutes(1), onComplete);
var timer = new Timer(targetDateTime, onComplete);

// Состояние
TimeSpan left   = timer.Remains;
TimeSpan passed = timer.GetTimePassed();

// Пауза / возобновление
timer.SetPause();   // RemoveCall(this), фиксация Remains, IsPaused = true
timer.Resume();     // End = UtcNow + Remains, повторная регистрация в Call
```

Жизненный цикл:

```
new Timer(5f, cb)
  → End = UtcNow + 5s
  → TimeController.Call(CallAction, 5f, this)
  → ... 5 секунд ...
  → CallAction(): IsComplete = true, cb?.Invoke()
```

```
SetPause()
  → TimeController.RemoveCall(this)
  → _remains = End - UtcNow  (через чтение property до IsPaused = true)
  → IsPaused = true

Resume()
  → End = UtcNow + _remains
  → IsPaused = false
  → TimeController.Call(CallAction, (float)Remains.TotalSeconds, this)
```

### Граничные случаи

- **Уход в фон:** `DateTime.UtcNow` продолжает тикать, `LateUpdate` останавливается. `Remains` корректен после возврата; callback срабатывает на первом `CheckQueue`.
- **SetPause — порядок операций:** фиксирует `Remains` через чтение property до установки `IsPaused = true`. После `IsPaused = true` getter возвращает кешированное значение.

---

## Паттерн: паузируемое отложенное действие

`Timer` — не только «таймер с прогрессом», но и **pause-safe замена `TimeController.Call`**
для отложенных шагов геймплея и визуала.

### Выбор инструмента

| Сценарий | Инструмент |
|---|---|
| Отложить на конец кадра / батчинг | `Call` / `Accumulate` |
| Отложенный вызов, которому пауза безразлична (app-уровень, аналитика, звук вне геймплея) | `Call(action, delay, owner)` |
| Отложенный **геймплейный/визуальный** шаг, который должен замереть на паузе (резолв удара, смена фазы, возврат анимации, скрытие реплики) | `Timer` + пауза по стейту |
| Нужен прогресс/остаток времени (слайдеры, обратный отсчёт) | `Timer` (`Remains`, `GetTimePassed`) |

Корень различия: пауза проекта — **состояние** (`MiniGameStates.Paused`), а не `Time.timeScale = 0`.
Очередь `TimeController` тикает по wall-clock и на паузе продолжает срабатывать.
`Timer` умеет `SetPause`/`Resume` с заморозкой остатка — но **только если его явно дёргать
из обработчика смены состояния**. Созданный и забытый `Timer` ведёт себя как обычный `Call`.

### Канонический сниппет

```csharp
private Timer _actionTimer;

// Запуск отложенного шага (вместо TimeController.Call(cb, delay, this))
_actionTimer?.SetPause();                    // отмена предыдущего, если был
_actionTimer = new Timer(delay, Callback);   // конструктор сразу запускает отсчёт!

// Обработчик смены состояния игры (OnGameStateChanged / OnStateChanged)
private void OnStateChanged(MiniGameStates state)
{
    switch (state)
    {
        case MiniGameStates.Play:
            _actionTimer?.Resume();
            break;
        case MiniGameStates.Paused:
            _actionTimer?.SetPause();
            break;
    }
}

// Очистка (DeInit / OnDisable / Unbind)
_actionTimer?.SetPause();   // метода отмены нет — пауза без Resume и есть отмена
_actionTimer = null;
```

### Грабли

- **Конструктор `Timer` запускает отсчёт немедленно.** «Создать на паузе» нельзя —
  создавайте только из кода, который гарантированно выполняется в `Play`
  (обработчики событий геймплея), либо сразу же ставьте на паузу.
- **Один таймер — одно действие.** Для цепочек шагов достаточно одного поля,
  если шаги последовательные (новый шаг отменяет предыдущий). Для независимых
  параллельных дедлайнов — список и проверка по игровому времени (см. ниже).
- **Если есть «игровое время» (аудиотрек, тикающий счётчик) — оно лучше таймера.**
  Дедлайн, привязанный к треку, замерзает вместе с ним бесплатно:
  храните цель (`targetTime`) и сравнивайте в обработчике тика времени,
  который уже гардится на состоянии «игра идёт».

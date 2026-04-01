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

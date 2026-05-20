# System (Core)

**Namespace:** `Vortex.Core.System.Abstractions`, `Vortex.Core.System.Abstractions.Timers`, `Vortex.Core.System.Abstractions.SystemControllers`, `Vortex.Core.System.ProcessInfo`, `Vortex.Core.System.Enums`, `Vortex.Core.System`
**Сборка:** `ru.vortex.system`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Фундаментальный пакет абстракций фреймворка. Определяет базовые паттерны, используемые всеми остальными системами: синглтон, системный контроллер с драйверной архитектурой, интерфейс процесса для асинхронной загрузки и календарный таймер.

Возможности:

- `Singleton<T>` — generic-синглтон с lazy-инициализацией
- `SystemController<T, TD>` — контроллер с подключаемым драйвером и очередью инициализации
- `ISystemDriver` — интерфейс платформозависимого драйвера
- `DriversGenericList` — white-list допустимых пар контроллер → драйвер
- `IProcess` / `ProcessData` — интерфейс и данные асинхронного процесса для `Loader`
- `DateTimeTimer` — таймер на основе `DateTime`, работает offline
- `SystemModel` — базовый класс моделей данных
- `IDataSource` — контракт источника данных, отдающего ссылки на объекты-данные с событием замены ссылок (`OnUpdateLink`)
- `IDataStorage : IDataSource` — типизированный getter `GetData<T>()` поверх источника
- `AppStates` — перечисление состояний приложения

Вне ответственности:

- Конкретные реализации драйверов — Unity-слой
- Загрузчик процессов (`Loader`) — `LoaderSystem`
- Конкретные системные контроллеры — отдельные пакеты

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| UniTask | `UniTask`, `CancellationToken` (в `IProcess`) |

Пакет не зависит от других пакетов Vortex. Является корневой зависимостью для всего фреймворка.

---

## Архитектура

### Singleton\<T\>

```
Singleton<T> where T : Singleton<T>, new()
  ├── Instance → T                           ← lazy, new T() + OnInstantiate()
  ├── Dispose() → OnDispose(), _instance = null
  ├── OnInstantiate() (virtual)
  └── OnDispose() (virtual)
```

Generic-синглтон с lazy-инициализацией. При первом обращении к `Instance` создаётся экземпляр через `new T()` и вызывается `OnInstantiate()`. `Dispose()` — защищённый, вызывает `OnDispose()` и обнуляет экземпляр.

### SystemController\<T, TD\>

```
SystemController<T, TD> : Singleton<T>, ISystemController
  where T : SystemController<T, TD>, new()
  where TD : ISystemDriver

  ├── IsInit: bool (static)
  ├── OnInit: Action (static event)          ← очередь ожидания инициализации
  ├── Driver: TD (static, protected)
  ├── SetDriver(TD) → bool
  │    ├── WhiteList-валидация
  │    ├── Если драйвер уже есть → Disconnect → Destroy → Connect → Init
  │    └── Если слот пуст → Connect → Init
  ├── HasDriver() → bool
  ├── GetDriverType() → Type
  ├── OnDriverConnect() (abstract)
  ├── OnDriverDisconnect() (abstract)
  └── CallOnInit() → IsInit = true, flush InitQueue
```

Центральный паттерн фреймворка. Каждая система (Database, Settings, Log, и т.д.) наследуется от `SystemController`. Драйвер — платформозависимая реализация (`ISystemDriver`), подключаемая извне.

#### Жизненный цикл подключения драйвера

```
SetDriver(driver)
  ├── driver == null → false
  ├── WhiteList не содержит пару → false
  ├── Driver уже есть и != driver
  │    ├── Driver.OnInit -= CallOnInit
  │    ├── OnDriverDisconnect()
  │    ├── Driver.Destroy()
  │    ├── Driver = driver
  │    ├── OnDriverConnect()
  │    ├── Driver.OnInit += CallOnInit
  │    ├── Driver.Init()
  │    └── return false (замена)
  └── Driver пуст
       ├── Driver = driver
       ├── OnDriverConnect()
       ├── Driver.OnInit += CallOnInit
       ├── Driver.Init()
       └── return true (первичная установка)
```

#### OnInit — отложенная подписка

`OnInit` — custom event accessor. При подписке до инициализации (`IsInit == false`) callback добавляется в `InitQueue`. При подписке после инициализации — вызывается немедленно. `CallOnInit()` выполняет все накопленные callback'и и очищает очередь.

#### DriversGenericList — WhiteList

Автогенерированный файл. Содержит `Dictionary<string, string>` — пары `AssemblyQualifiedName` системного контроллера → `AssemblyQualifiedName` допустимого драйвера. `SetDriver` отклоняет драйверы, не присутствующие в списке.

`SystemController` загружает WhiteList через рефлексию (`Type.GetType("Vortex.Core.System.DriversGenericList")`) при первом вызове `SetDriver`, результат кешируется в статическом поле.

Специальное значение `"[off]"` вместо имени драйвера — система отключена, ни один драйвер не будет принят.

### ISystemDriver

```
ISystemDriver
  ├── OnInit: Action (event)
  ├── Init()                                 ← вызывается после SetDriver
  └── Destroy()                              ← вызывается при отключении
```

### IProcess / ProcessData

```
IProcess
  ├── GetProcessInfo() → ProcessData
  ├── RunAsync(CancellationToken) → UniTask
  └── WaitingFor() → Type[]                  ← зависимости (null = нет)

ProcessData
  ├── Name: string
  ├── Progress: int
  └── Size: int
```

Интерфейс для асинхронных процессов, регистрируемых в `Loader`. `WaitingFor()` возвращает массив типов контроллеров, которые должны завершить загрузку раньше. `ProcessData` — mutable (без инкапсуляции, для производительности).

### DateTimeTimer

```
DateTimeTimer
  ├── Start: DateTime
  ├── End: DateTime
  ├── Duration: TimeSpan
  ├── IsComplete() → End <= UtcNow
  ├── IsStarted() → Start <= UtcNow
  ├── GetTimeRemains() → TimeSpan            ← 0 если завершён, Duration если не начат
  ├── GetTimeLeft() → TimeSpan               ← Duration если завершён, 0 если не начат
  └── ToString()                             ← "DateTimeTimer from {Start} to {End} (duration: {Duration})"
```

Таймер на основе `DateTime.UtcNow`. Работает offline — не зависит от Update-цикла. Три конструктора: `(DateTime end)`, `(TimeSpan duration)`, `(DateTime start, DateTime end)`.

### Вспомогательные типы

| Тип | Назначение |
|-----|-----------|
| `SystemModel` | Абстрактный базовый класс моделей данных (пустой, для типизации) |
| `ISystemController` | Маркерный интерфейс системного контроллера |
| `IDataSource` | Контракт источника данных: событие `OnUpdateLink` (link-level — ссылки на выдаваемые данные пересозданы/замещены) |
| `IDataStorage : IDataSource` | Типизированный доступ к данным: `GetData<T>() where T : class` |
| `AppStates` | Enum: `None`, `Unfocused`, `WaitSettings`, `Starting`, `Running`, `Loading`, `Saving`, `Stopping` |

---

## Контракт

### Singleton\<T\>

| Гарантия | Описание |
|----------|----------|
| Единственный экземпляр | `_instance` создаётся один раз |
| Lazy-инициализация | До первого обращения к `Instance` экземпляр не существует |
| `Dispose` обнуляет | Повторное обращение к `Instance` создаст новый экземпляр |

### SystemController\<T, TD\>

| Гарантия | Описание |
|----------|----------|
| WhiteList-валидация | `SetDriver` отклоняет незарегистрированные драйверы |
| `OnInit` — safe subscribe | Подписка после инициализации — мгновенный вызов |
| Замена драйвера | Старый `Destroy()`, новый `Init()` |
| `IsInit` устанавливается однократно | После `CallOnInit` — `true` навсегда |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `Singleton<T>` — не потокобезопасен | Нет `lock` / `volatile` |
| `DriversGenericList` — автогенерация | Ручное редактирование будет перезаписано |
| `ProcessData` — public fields | Оптимизация, контроль на программисте |
| `DateTimeTimer` — без Pause/Resume | Только фиксированные Start/End |
| `IProcess.WaitingFor()` — типы контроллеров | Не экземпляры, а `Type[]` для топологической сортировки |
| `IDataSource.OnUpdateLink` — link-level | Сигнализирует только замену/пересоздание ссылок, выдаваемых через `IDataStorage.GetData<T>()`. НЕ сигнализирует об изменении внутреннего состояния уже выданных объектов и о добавлении новых без замены существующих |

---

## Использование

### Создание системного контроллера

```csharp
public interface IMyDriver : ISystemDriver
{
    string LoadConfig();
}

public class MySystem : SystemController<MySystem, IMyDriver>
{
    protected override void OnDriverConnect()
    {
        var config = Driver.LoadConfig();
    }

    protected override void OnDriverDisconnect() { }
}
```

### Использование DateTimeTimer

```csharp
var timer = new DateTimeTimer(TimeSpan.FromMinutes(30));

if (!timer.IsComplete())
{
    var remains = timer.GetTimeRemains();
    Console.WriteLine($"Осталось: {remains.Minutes}м {remains.Seconds}с");
}
```

### Отложенная подписка на систему

```csharp
Database.OnInit += () =>
{
    // Вызовется сразу, если Database уже инициализирована
    // Или отложится до инициализации
    var record = Database.GetRecord<MyRecord>("id");
};
```

### IProcess для Loader

```csharp
public class MyDriver : Singleton<MyDriver>, IMyDriver, IProcess
{
    public ProcessData GetProcessInfo() => _processData;

    public async UniTask RunAsync(CancellationToken ct)
    {
        _processData = new ProcessData("My System") { Size = 10, Progress = 0 };
        // загрузка ...
    }

    public Type[] WaitingFor() => new[] { typeof(Database) };
}
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `SetDriver(null)` | Возвращает `false`, драйвер не меняется |
| `SetDriver` с типом вне WhiteList | Возвращает `false` |
| `SetDriver` повторно с тем же экземпляром | `Driver.Equals(driver)` → пропуск замены, `Init()` вызывается |
| Подписка на `OnInit` после инициализации | Callback вызывается немедленно |
| `DateTimeTimer` с `end < start` | Отрицательный `Duration`, `IsComplete()` сразу `true` |
| `DateTimeTimer` — время до `Start` | `GetTimeRemains()` → `Duration`, `GetTimeLeft()` → `Zero` |
| `Dispose()` синглтона + повторный `Instance` | Новый экземпляр через `new T()` |
| `ProcessData.Progress > Size` | Нет валидации, ответственность на программисте |
| `WaitingFor()` → `null` | Процесс не имеет зависимостей |

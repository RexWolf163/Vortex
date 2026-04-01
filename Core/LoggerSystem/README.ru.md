# LoggerSystem (Core)

**Namespace:** `Vortex.Core.LoggerSystem.Bus`, `Vortex.Core.LoggerSystem.Model`
**Сборка:** `ru.vortex.logger`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Платформонезависимая шина логирования. Маршрутизирует сообщения с уровнем критичности через драйвер.

Возможности:

- Вывод сообщений трёх уровней: `Common`, `Warning`, `Error`
- Привязка источника сообщения (`Object source`)
- Подключаемый драйвер — платформа определяет способ вывода

Вне ответственности:

- Форматирование и вывод сообщений — драйвер
- Фильтрация по уровню — драйвер
- Запись в файл, отправка на сервер — драйвер

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `ISystemDriver` |

---

## Архитектура

```
Log (SystemController<Log, IDriver>)
  ├── Print(LogData)                  ← делегирует драйверу
  └── Print(LogLevel, string, Object) ← создаёт LogData и делегирует

LogData (struct)
  ├── Level: LogLevel
  ├── Message: string
  └── Source: Object

LogLevel (enum)
  ├── Common
  ├── Warning
  └── Error

IDriver : ISystemDriver
  └── Print(LogData)
```

### Log

Статический контроллер. Два перегрузки `Print` — прямая передача `LogData` или конструирование из параметров. Не содержит логики обработки — маршрутизация к драйверу.

### LogData

Неизменяемая структура. `Source` типа `System.Object` — может быть строкой (имя источника) или объектом (драйвер извлекает `GetType().Name`).

### LogLevel

Три уровня критичности. Драйвер самостоятельно определяет, как отображать каждый уровень.

---

## Контракт

### Вход

- Драйвер подключается через `Log.SetDriver()`
- Вызов `Log.Print()` с данными сообщения

### Выход

- Драйвер получает `LogData` и выполняет вывод

### API

| Метод | Описание |
|-------|----------|
| `Log.Print(LogData)` | Передача структуры лога драйверу |
| `Log.Print(LogLevel, string, Object)` | Создание `LogData` и передача драйверу |

### IDriver

| Метод | Описание |
|-------|----------|
| `Print(LogData)` | Вывод сообщения. Реализация определяет формат и канал |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Один драйвер одновременно | `SystemController` допускает один `IDriver` |
| Нет фильтрации по уровню в ядре | Ответственность драйвера |
| `Source` — `System.Object` | Гибкость: строка или типизированный объект |

---

## Использование

### Вывод сообщений

```csharp
// Через параметры
Log.Print(LogLevel.Common, "Initialized", this);
Log.Print(LogLevel.Warning, "Config not found, using defaults", this);
Log.Print(LogLevel.Error, "Connection failed", "NetworkManager");

// Через структуру
var data = new LogData(LogLevel.Error, "Critical failure", this);
Log.Print(data);
```

### Реализация драйвера

```csharp
public class MyDriver : IDriver
{
    public event Action OnInit;

    public void Print(LogData log)
    {
        Console.WriteLine($"[{log.Level}] [{log.Source}] {log.Message}");
    }

    public void Init() => OnInit?.Invoke();
    public void Destroy() { }
}
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Драйвер не подключён | `NullReferenceException` при вызове `Print` |
| `Source` — строка | Драйвер использует строку напрямую как имя источника |
| `Source` — объект | Драйвер вызывает `GetType().Name` для имени источника |
| `Source` — `null` | `NullReferenceException` при обращении к `Source` в драйвере |

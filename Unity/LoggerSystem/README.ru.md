# LoggerSystem (Unity)

**Namespace:** `Vortex.Unity.LoggerSystem`
**Сборка:** `ru.vortex.unity.logger`
**Платформа:** Unity 2021.3+

---

## Назначение

Unity-драйвер для `Log`. Маршрутизирует `LogData` в `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` с форматированием `[source] message`.

Возможности:

- Автоматическая регистрация драйвера (runtime и editor)
- Маппинг `LogLevel` → `Debug.Log*`
- Форматирование с именем источника в квадратных скобках

Вне ответственности:

- Определение уровней логирования — Core
- API логирования — Core (`Log.Print`)

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.LoggerSystem` | `Log`, `LogData`, `LogLevel`, `IDriver` |
| Unity Engine | `Debug.Log*`, `RuntimeInitializeOnLoadMethod` |
| Unity Editor | `InitializeOnLoad` (editor-расширение) |

---

## Архитектура

```
LogDriver : IDriver (partial)
  ├── [RuntimeInitializeOnLoadMethod] Run()     ← runtime-регистрация
  ├── Print(LogData)                             ← маппинг LogLevel → Debug.Log*
  ├── Init() / Destroy()                         ← ISystemDriver lifecycle
  └── (Editor partial)
       └── [InitializeOnLoad] static LogDriver() ← editor-регистрация
```

### Регистрация

| Контекст | Механизм | Момент |
|----------|----------|--------|
| Runtime | `[RuntimeInitializeOnLoadMethod]` | До первого кадра |
| Editor | `[InitializeOnLoad]` static constructor | При загрузке домена |

Editor-расширение выводит подтверждающее сообщение `Warning` при подключении.

### Маппинг уровней

| LogLevel | Unity-метод |
|----------|-------------|
| `Common` | `Debug.Log` |
| `Warning` | `Debug.LogWarning` |
| `Error` | `Debug.LogError` |

### Формат вывода

```
[SourceName] Message text
```

`Source` обрабатывается: если `string` — используется напрямую, иначе — `Source.GetType().Name`.

---

## Контракт

### Вход

- `LogData` от ядра через `IDriver.Print()`

### Выход

- Сообщение в Unity Console с соответствующим уровнем

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Нет фильтрации по уровню | Все сообщения выводятся; фильтрация средствами Unity Console |
| `Source = null` | `NullReferenceException` — драйвер обращается к `Source` без проверки |
| Нет записи в файл | Только `Debug.Log*`; файловое логирование требует отдельного драйвера |

---

## Использование

Драйвер регистрируется автоматически. Дополнительная настройка не требуется.

```csharp
// Вызов из любого места проекта
Log.Print(LogLevel.Common, "Player spawned", this);
Log.Print(LogLevel.Error, "Save file corrupted", "SaveSystem");
```

Результат в Unity Console:

```
[PlayerController] Player spawned
[SaveSystem] Save file corrupted    (красный)
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Editor mode | Драйвер регистрируется через `[InitializeOnLoad]`, логирование работает |
| Play mode | Драйвер регистрируется через `[RuntimeInitializeOnLoadMethod]` |
| Двойная регистрация | `Log.SetDriver()` возвращает `false`, повторного подключения не происходит |
| `Source` — `null` | `NullReferenceException` в `Print` |
| `Source` — строка `"MySystem"` | Вывод: `[MySystem] message` |
| `Source` — объект `MonoBehaviour` | Вывод: `[ClassName] message` |

# LoaderSystem (Core)

**Namespace:** `Vortex.Core.LoaderSystem.Bus`
**Сборка:** `ru.vortex.apploader`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Централизованная асинхронная оркестрация загрузки компонентов приложения.

Возможности:

- Регистрация компонентов (`IProcess`) до старта загрузки
- Автоматическое упорядочивание по зависимостям через `WaitingFor()`
- Прогресс загрузки: текущий шаг, размер очереди, данные модуля (`ProcessData`)
- Одиночная загрузка отдельного модуля через `RunAlone()`
- Отмена загрузки через `CancellationToken` при выходе из приложения
- Интеграция с жизненным циклом: `AppStates.Starting` → `AppStates.Running`

Вне ответственности:

- Логика инициализации самих компонентов (только оркестрация запуска)
- Параллельная загрузка независимых компонентов
- Откат состояния при отмене

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.AppSystem` | `App.SetState()`, `App.OnExit`, `AppStates` |
| `Vortex.Core.SettingsSystem` | `Settings.Data().DebugMode` — debug-логирование |
| `Vortex.Core.LoggerSystem` | `Log.Print()` — логирование процесса |
| `Vortex.Core.System.ProcessInfo` | `IProcess`, `ProcessData` |
| `Vortex.Core.System.Abstractions` | `ISystemController` — проверка `IsInit` для внешних контроллеров |
| `Cysharp.Threading.Tasks` | `UniTask` |

---

## Архитектура

Статический класс `Loader`. Единственная точка входа для управления загрузкой.

### Процесс загрузки

```
Register(IProcess) × N    ← до старта
        ↓
      Run()                ← внешний триггер (LoaderStarter)
        ↓
App.SetState(Starting)
        ↓
   ┌─ Loading loop ─────────────────────────────┐
   │  1. Найти процесс, чьи зависимости         │
   │     (WaitingFor) уже загружены              │
   │  2. Вызвать RunAsync(token)                 │
   │  3. Добавить в loaded, обновить прогресс    │
   │  4. Повторить пока очередь не пуста         │
   └─────────────────────────────────────────────┘
        ↓
OnComplete → App.SetState(Running)
```

### Разрешение порядка

Загрузчик на каждой итерации ищет первый `IProcess`, у которого все типы из `WaitingFor()` уже загружены. Проверка зависимости:

- Тип есть в `loaded` (уже загружен в текущей сессии) → OK
- Тип реализует `ISystemController` и его `IsInit == true` → OK (внешний контроллер, инициализирован вне Loader)
- Иначе → зависимость не удовлетворена

Если ни один процесс не может быть выбран — циклическая или некорректная зависимость. Логируется ошибка, вызывается `App.Exit()`.

### IProcess

| Метод | Описание |
|-------|----------|
| `WaitingFor()` | Массив `Type[]` — типы, которые должны быть загружены до данного процесса. `null` или пустой массив — без зависимостей |
| `RunAsync(CancellationToken)` | Асинхронная инициализация. Вызывается после удовлетворения зависимостей |
| `GetProcessInfo()` | `ProcessData` для индикации: имя, прогресс, размер. `null` → дефолтные значения |

### ProcessData

Открытые поля без инкапсуляции (оптимизация обращения):

| Поле | Тип | Описание |
|------|-----|----------|
| `Name` | `string` | Имя процесса (отображается в UI) |
| `Progress` | `int` | Текущий шаг внутри процесса |
| `Size` | `int` | Общее количество шагов |

---

## Контракт

### Вход

- `Loader.Register(IProcess)` — до `Run()`
- `Loader.Run()` — вызывается внешним триггером (один раз)

### Выход

- Все зарегистрированные `IProcess` вызваны через `RunAsync()` в корректном порядке
- `App.SetState(AppStates.Running)` после завершения
- События: `OnLoad` (начало), `OnComplete` (завершение)

### API

| Метод | Описание |
|-------|----------|
| `Register(IProcess)` | Регистрация модуля. Дубликат по типу — `Warning`, пропуск |
| `UnRegister<T>()` / `UnRegister(Type)` / `UnRegister(IProcess)` | Снятие с регистрации |
| `Run()` | Запуск полной загрузки. Защита от повторного вызова |
| `RunAlone(IProcess)` | Загрузка одного модуля вне общей очереди |
| `GetProgress()` | Текущий номер шага (1-based) |
| `GetSize()` | Общее количество модулей |
| `GetCurrentLoadingData()` | `ProcessData` текущего загружаемого модуля |

### События

| Событие | Момент |
|---------|--------|
| `OnLoad` | Перед началом цикла загрузки |
| `OnComplete` | После завершения всех модулей |

### Гарантии

- Загрузка выполняется один раз за сессию (`_isRunning`)
- Порядок полностью определяется `WaitingFor()`
- Циклические зависимости обнаруживаются и завершают приложение
- Каждый шаг логируется через `Log.Print`

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Строго последовательная загрузка | Нет параллелизма независимых модулей |
| Отмена не откатывает состояние | Уже загруженные модули остаются в памяти |
| `_isRunning` не сбрасывается | Повторный `Run()` невозможен без перезапуска |
| `CancellationTokenSource` — readonly static | Один токен на всю жизнь приложения |
| `Queue` — `Dictionary<Type, IProcess>` | Один экземпляр на тип |

---

## Использование

### Регистрация в драйвере (Layer 2)

```csharp
[RuntimeInitializeOnLoadMethod]
private static void Init()
{
    if (MyService.SetDriver(Instance))
        Loader.Register(Instance);
}
```

### Реализация IProcess

```csharp
public class AudioDriver : IProcess
{
    public Type[] WaitingFor() => new[] { typeof(DatabaseDriver), typeof(SettingsDriver) };

    public async UniTask RunAsync(CancellationToken token)
    {
        // Инициализация после Database и Settings
        await LoadAudioBank(token);
    }

    public ProcessData GetProcessInfo() => _processData;
}
```

### Одиночная загрузка

```csharp
await Loader.RunAlone(hotReloadModule);
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Циклическая зависимость (A → B → A) | `LogLevel.Error`, `App.Exit()` |
| `WaitingFor()` содержит незарегистрированный тип | `LogLevel.Error`; если тип — `ISystemController`, проверяется `IsInit` |
| `WaitingFor()` возвращает `null` | Трактуется как пустой массив — без зависимостей |
| Повторная регистрация того же типа | `LogLevel.Warning`, пропуск |
| `CancellationToken` отменён | Цикл прерывается без отката; `AppStates` остаётся `Starting` |
| Повторный `Run()` | Игнорируется |
| `GetProcessInfo()` возвращает `null` | Используются дефолтные `ProcessData` |

# AppSystem (Core)

Конечный автомат жизненного цикла приложения.

## Назначение

Управление состоянием приложения через статическую шину, событийная модель переходов, отложенная инициализация до готовности `Settings`.

- Конечный автомат состояний (`AppStates`)
- События переходов (`OnStateChanged`, `OnStarting`, `OnStart`, `OnExit`)
- Отложенная инициализация до готовности `Settings`
- Условное логирование переходов через debug-флаг
- Фиксация времени запуска

Вне ответственности: обработка фокуса/фона, завершение процесса, загрузка сцен, управление ресурсами, сохранение.

## Зависимости

- `Vortex.Core.SettingsSystem` — проверка готовности (`Settings.Data()`), debug-флаг (`AppStateDebugMode`)
- `Vortex.Core.LoggerSystem` — логирование переходов
- `Vortex.Core.System.Enums` — `AppStates`

## Архитектура

```
App (static partial bus)
├── App.cs          — AppModel, Init(), Exit()
└── AppExtEvents.cs — события, GetState(), SetState()

AppModel (sealed partial)
├── AppModel.cs        — _state, _startTime
└── AppModelExtTime.cs — GetStartTime()

SettingsModelExtDebug (partial SettingsModel)
└── AppStateDebugMode  — bool property
```

### Состояния

```
None → WaitSettings (если Settings не готов)
None → Starting → Running ⇄ Unfocused
                → Loading
                → Saving
       Running  → Stopping
```

| Состояние | Описание |
|-----------|----------|
| `None` | До первого обращения к `App` |
| `WaitSettings` | `Settings` не загружены, инициализация отложена |
| `Starting` | Запуск, загрузка систем |
| `Running` | Штатная работа |
| `Loading` | Загрузка данных |
| `Saving` | Сохранение данных |
| `Unfocused` | Приложение в фоне |
| `Stopping` | Завершение работы |

### Инициализация

Getter `App.Data` создаёт `AppModel` при первом обращении и вызывает `SetState(Starting)`. Если `Settings.Data() == null`, состояние переходит в `WaitSettings`. При повторном обращении к `Data` в состоянии `WaitSettings` инициализация повторяется.

### Переходы (SetState)

1. Дублирующее состояние — `return false`
2. `Settings.Data() == null` — переход в `WaitSettings`, `return false`
3. Логирование (при `AppStateDebugMode`)
4. Установка нового состояния, вызов `OnStateChanged`
5. `Starting|Unfocused → Running` — дополнительно `OnStart`
6. `→ Starting` — `OnStarting`
7. `→ Stopping` — `OnExit`

## Контракт

### Вход
- Готовность `Settings` (через `Settings.Data()`)

### Выход
- Текущее состояние: `App.GetState()`
- События: `OnStateChanged(AppStates)`, `OnStarting`, `OnStart`, `OnExit`
- Время запуска: `AppModel.GetStartTime()`

### Гарантии
- `SetState` идемпотентен — повторная установка того же состояния возвращает `false`
- `OnStart` вызывается только при переходе `Starting|Unfocused → Running`
- Инициализация откладывается до готовности `Settings`
- `_startTime` фиксируется как `DateTime.UtcNow` при создании `AppModel`

### Ограничения
- `SetState` обращается к `_data` напрямую — вызов до первого обращения к `Data` приведёт к NRE
- `AppModel` — `sealed`, `internal` конструктор — создание только внутри `App`

## Использование

### Подписка на состояния

```csharp
App.OnStateChanged += (AppStates newState) => { };
App.OnStarting += () => { };    // → Starting
App.OnStart += () => { };       // Starting|Unfocused → Running
App.OnExit += () => { };        // → Stopping
```

### Управление состоянием

```csharp
AppStates state = App.GetState();
bool changed = App.SetState(AppStates.Running);
App.Exit(); // → Stopping → OnExit
```

### Debug

Переходы логируются автоматически при активном debug-режиме. Флаг `AppStateDebugMode` — partial-расширение `SettingsModel`. Настройка: в ассете `DebugSettings` включить `DebugMode` (глобальный) и `appStates` (локальный toggle).

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `Settings` не готов при первом обращении | Состояние `WaitSettings`, повторная попытка при следующем обращении к `Data` |
| Повторный `SetState` с тем же значением | `return false`, события не вызываются |
| `OnStart` при возврате из `Unfocused` | Вызывается, если новое состояние `Running` |
| `SetState` до создания `AppModel` | NRE — `_data` ещё `null` |

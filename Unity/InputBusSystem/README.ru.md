# InputBusSystem

**Namespace:** `Vortex.Unity.InputBusSystem`
**Сборка:** `ru.vortex.unity.inputbus`

## Назначение

Управление вводом на основе Unity Input System с поддержкой карт ввода (Input Action Maps), подписки на экшены по принципу LIFO и прямой привязки клавиш.

Возможности:
- Централизованная маршрутизация сигналов ввода через статический контроллер
- Управление картами ввода с автоматической активацией/деактивацией по наличию подписчиков
- LIFO-маршрутизация — сигнал получает только последний зарегистрированный подписчик
- Гарантия парности `performed`/`canceled` — отмена доставляется только получателю нажатия
- Привязка произвольных клавиш и комбинаций к UI-событиям без прямого взаимодействия с Input System API

Вне ответственности:
- Ребиндинг клавиш в рантайме
- Визуальный редактор карт ввода (используется стандартный Unity Input Actions Asset)
- Аналоговый ввод (стики, триггеры) — только дискретные действия (Button)

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| Unity Input System | `InputSystem.actions`, `InputAction`, `InputActionMap` |
| `Vortex.Core.AppSystem` | `App.GetState()`, `App.OnStateChanged`, `App.OnExit` |
| `Vortex.Core.SettingsSystem` | `Settings.Data().DebugMode` — диагностическое логирование |
| `Vortex.Core.Extensions` | `AddNew`, `AddOnce` — хелперы коллекций |
| `Vortex.Unity.EditorTools` | `[ValueSelector]`, `[VortexCollection]`, `[InfoBubble]` |
| `Vortex.Unity.UI.Misc` | `AdvancedButton` — опциональная интеграция |

---

## Архитектура

```
InputBusSystem/
├── InputController.cs          # Статический контроллер: индексация, маршрутизация
├── InputSubscriber.cs          # Модель подписчика с equality по Owner
├── Handlers/
│   ├── InputActionHandler.cs   # Подписка на экшен через InputController
│   ├── InputMapHandler.cs      # Активация карты ввода
│   └── KeyboardHandler.cs      # Прямая привязка клавиш/комбинаций
└── Debug/
    ├── Model/SettingsModelExtInput.cs    # partial SettingsModel + InputDebugMode
    └── Presets/DebugSettingsExtInput.cs  # partial DebugSettings + toggle
```

### Поток данных

```
Unity Input System
    ↓ performed / canceled
InputController (маршрутизация по LIFO)
    ↓ callback
InputActionHandler / подписчик
    ↓ UnityEvent
UI / игровая логика
```

`KeyboardHandler` работает автономно — создаёт собственный `InputAction` в обход `InputController`.

---

## Ключевые концепции

### LIFO-маршрутизация

При нескольких подписчиках на один экшен `performed` доставляется только последнему (`subscribers[^1]`). Позволяет перехватывать ввод в модальных окнах и оверлеях без отписки предыдущих слоёв.

### Парность performed/canceled

Объект, получивший `performed`, запоминается в `CatchPerformed`. При `canceled` сигнал доставляется только ему. Если верхний подписчик сменился — `canceled` отбрасывается.

### Карты с подсчётом пользователей

Карта активируется при первом `AddMapUser` и деактивируется при уходе последнего пользователя (`RemoveMapUser`). Предотвращает конфликт между картами без ручного управления.

### Комбинации клавиш

`KeyboardHandler` поддерживает до 3 модификаторов через композитные биндинги Unity (`OneModifier`, `TwoModifiers`, `ThreeModifiers`). Последняя клавиша в массиве — активирующая, остальные — модификаторы.

### Отложенная подписка

Хэндлеры проверяют `App.GetState() < AppStates.Running` и откладывают регистрацию до готовности приложения через `App.OnStateChanged`.

---

## Контракт

### Вход
- Unity Input Actions Asset с определёнными картами и экшенами
- `AppStates.Running` — состояние, при котором хэндлеры регистрируются

### Выход
- `UnityEvent onPressed` / `onReleased` — в `InputActionHandler` и `KeyboardHandler`
- `AdvancedButton.Press()` / `Release()` — опциональная интеграция
- Активация/деактивация `InputActionMap` — в `InputMapHandler`

### Гарантии
- Один `performed` → максимум один `canceled` для того же подписчика
- Карта деактивируется только при нулевом количестве пользователей
- Подписчик не дублируется в списке (`AddOnce` с equality по `Owner`)
- Хэндлеры отписываются в `OnDisable` / `OnDestroy`

### Ограничения
- Только дискретный ввод (Button) — аналоговые значения не передаются
- `InputSubscriber.Equals` сравнивает по `Owner` — один объект не может иметь разные колбэки на один экшен
- `KeyboardHandler` поддерживает максимум 3 модификатора; 4+ игнорируются с предупреждением
- Нет механизма приоритетов — только порядок добавления (LIFO)

---

## API

### InputController (static)

```csharp
// Карты ввода
static string[] GetMaps()
static void AddMapUser(string mapId, object inputMapUser)
static void RemoveMapUser(string mapId, object inputMapUser)

// Экшены ввода
static string[] GetActions()
static void AddActionUser(string actionInputId, object actionInputUser,
    Action onPerformedCallback, Action onCanceledCallback)
static void RemoveActionUser(string actionInputId, object actionInputUser)
```

### InputSubscriber

```csharp
// Equality определяется по Owner
public readonly object Owner;
public Action OnPerformed { get; }
public Action OnCanceled { get; }
```

---

## Использование

### Подписка на экшен из кода

```csharp
InputController.AddActionUser("Jump", this, OnJumpPerformed, OnJumpCanceled);

// Отписка
InputController.RemoveActionUser("Jump", this);
```

`this` используется как ключ идентификации. Повторный `AddActionUser` с тем же объектом не создаст дубликат.

### Подписка через Inspector (InputActionHandler)

Компонент `InputActionHandler` на GameObject:
- `inputAction` — экшен из dropdown (заполняется из Input Actions Asset)
- `button` — опционально, ссылка на `AdvancedButton`
- `onPressed` / `onReleased` — Unity Events

### Активация карты (InputMapHandler)

Компонент `InputMapHandler` на GameObject:
- `inputMap` — карта из dropdown

Карта активна, пока хотя бы один `InputMapHandler` для неё включён в сцене.

### Прямая привязка клавиш (KeyboardHandler)

Компонент `KeyboardHandler` на GameObject:
- `buttonCode` — массив отдельных клавиш (любая вызовет срабатывание)
- `buttonsCombinations` — массив комбинаций (последняя клавиша — активирующая, остальные — модификаторы)
- `button`, `onPressed`, `onReleased` — аналогично `InputActionHandler`

---

## Критические требования

1. **Input Actions Asset должен быть назначен** — `InputController.Init()` читает `InputSystem.actions`. При отсутствии словари пусты, хэндлеры выбросят `KeyNotFoundException`.
2. **Имена экшенов и карт уникальны** — дубликат вызовет `ArgumentException` при инициализации.
3. **`KeyboardHandler` не проходит через `InputController`** — его экшены не участвуют в LIFO-маршрутизации.
4. **`KeyboardHandler` требует физической клавиатуры** — при `Keyboard.current == null` логирует предупреждение.

---

## Debug

Диагностическое логирование управляется через `Settings.Data().InputDebugMode`. Настраивается в ассете `DebugSettings` (toggle `InputDebugMode`).

При включении логируются: регистрация/отписка подписчиков, доставка `performed`/`canceled`, активация/деактивация карт.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Несуществующее имя экшена/карты | `KeyNotFoundException` |
| `RemoveActionUser` для незарегистрированного объекта | Тихий пропуск |
| Подписчик `performed` удалён до `canceled` | `canceled` отбрасывается |
| Нет подписчиков при `performed` | Тихий пропуск |
| `Keyboard.current == null` | Предупреждение; нажатия игнорируются |
| Комбинация с 4+ модификаторами | Предупреждение; комбинация пропущена |
| `OnEnable` до `AppStates.Running` | Подписка отложена до смены состояния |
| Input Actions Asset не назначен | Словари пусты; `KeyNotFoundException` при регистрации |

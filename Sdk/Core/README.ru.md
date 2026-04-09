# GameCore

**Namespace:** `Vortex.Sdk.Core.GameCore`
**Assembly:** `ru.vortex.sdk.game.core`

## Назначение

Центральная шина игровой сессии. Управляет состояниями игры (`Off`, `Play`, `Win`, `Fail`, `Paused`, `Loading`), хранит составную модель данных и предоставляет единый API для всех подсистем, работающих с игровым процессом.

Возможности:
- Управление жизненным циклом игры: запуск, пауза, выход
- Реализует `IReactiveData` — подписка через `OnUpdate` / `OnUpdateData`
- Составная модель `GameModel` — расширяемый контейнер через `IGameData`
- Автоматическая пауза при потере фокуса приложением
- Сохранение и загрузка через `ISaveable` / `SaveController`
- Сериализация / десериализация состояния (POCO-поля через `SerializeController`)
- Editor-режим: создание модели без запуска приложения

Вне ответственности:
- Конкретная игровая механика
- Визуальное представление
- Сохранение на диск (используется `SaveSystem` из Core)

## Зависимости

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`
- `Vortex.Core.Extensions.ReactiveValues` — `IReactiveData`
- `Vortex.Core.AppSystem.Bus` — `App`, `AppStates`
- `Vortex.Core.ComplexModelSystem` — `ComplexModel<T>`
- `Vortex.Core.SaveSystem` — `SaveController`, `ISaveable`

### Unity
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController.Accumulate`
- `Cysharp.Threading.Tasks` — `UniTask` (save/load)

## Архитектура

```
GameController (Singleton, IReactiveData, ISaveable, static API)
├── GameModel (ComplexModel<IGameData>)
│   ├── State: GameStates
│   └── Dictionary<Type, IGameData>   ← пакеты регистрируют свои данные
├── OnNewGame                          ← событие новой игры
├── OnGameStateChanged                 ← событие смены состояния
├── OnLoadGame                         ← событие завершения загрузки
├── OnUpdate / OnUpdateData            ← реактивная подписка (IReactiveData)
├── CallUpdateEvent()                  ← батчинг через TimeController.Accumulate
└── Serialize / Deserialize            ← JSON сериализация (POCO-поля)
```

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `GameController` | `Singleton<T>`, `IReactiveData`, `ISaveable`, partial, static | Шина управления игрой |
| `GameModel` | `ComplexModel<IGameData>` | Составная модель данных |
| `GameStates` | `enum` | Off, Play, Win, Fail, Paused, Loading |
| `GameStateHandler` | `MonoBehaviour` | `UIStateSwitcher` по состоянию игры |
| `GameStateCondition` | `UnityUserInterfaceCondition` | Условие показа UI по состоянию |
| `GameMenuHandler` | `MonoBehaviour` | Обработчик кнопок меню (NewGame, Pause, Exit) |

### Partial-расширения

`GameController` — partial-класс. Другие пакеты расширяют его, не модифицируя основной файл:
- `QuestControllerExtEditor` подписывается на `OnEditorGetData`
- Проект может добавить свои partial-расширения

## Контракт

### Вход
- `App.OnStateChanged` — реакция на глобальные состояния приложения
- `GameModel.IGameData` — маркер для регистрации данных в составной модели

### Выход
- `GameController.GetState()` — текущее состояние
- `GameController.Get<T>()` — доступ к зарегистрированным данным
- `GameController.OnGameStateChanged` — событие смены состояния
- `GameController.OnNewGame` — событие новой игры
- `GameController.OnLoadGame` — событие завершения загрузки
- `GameController.OnUpdate` — static подписка на обновление данных (проксирует `OnUpdateData`)
- `GameController.CallUpdateEvent()` — вызов `OnUpdateData` с батчингом через `TimeController.Accumulate`

### Гарантии
- `NewGame()` блокируется до вызова `ExitGame()` (lock-механизм)
- При `AppStates.Unfocused` — автоматическая пауза
- При `AppStates.Stopping` — очистка ресурсов
- Смена состояния на то же самое — игнорируется (нет лишних событий)

### Ограничения
- Один экземпляр `GameController` на приложение
- `ExitGame()` обязателен перед повторным `NewGame()`
- `_data` создаётся лениво — fail-fast при вызове `GetState()` до инициализации

## Использование

### Запуск и завершение игры

```csharp
GameController.NewGame();           // Off → Play, вызов OnNewGame
GameController.SetPause(true);      // Play → Paused
GameController.SetPause(false);     // Paused → Play
GameController.ExitGame();          // → Off, разблокировка NewGame
```

### Регистрация данных пакета

```csharp
public class MyPackageData : GameModel.IGameData
{
    public int Score { get; set; }
}

// В контроллере пакета:
var data = GameController.Get<MyPackageData>();
```

### Подписка на изменения данных

```csharp
// Рекомендуемый способ (static event)
GameController.OnUpdate += OnDataUpdated;
GameController.OnUpdate -= OnDataUpdated;

// Вызов обновления с батчингом (несколько вызовов за кадр схлопываются в один)
GameController.CallUpdateEvent();
```

### Подписка на состояния

```csharp
GameController.OnGameStateChanged += () =>
{
    var state = GameController.GetState();
    // ...
};
```

### Сохранение и загрузка

`GameController` реализует `ISaveable` и автоматически регистрируется в `SaveController`.

```
Загрузка: Off → Loading → Init() → Deserialize(POCO) → Play → OnLoadGame
```

- `Init()` создаёт структуру модели (все `IGameData`-реализации через `Activator.CreateInstance`)
- `Deserialize` загружает POCO-поля поверх существующих объектов (не пересоздаёт словарь)
- Не-POCO поля (события, ссылки) сохраняются от `Init()`
- `GameModel.BeforeDeserialization` создаёт backup `Index` на случай ошибки десериализации

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `NewGame()` без предшествующего `ExitGame()` | Вызов игнорируется (lock) |
| `SetPause(true)` при `State != Play` | Игнорируется |
| `SetPause(false)` при `State != Paused` | Игнорируется |
| `GetState()` до первой инициализации | NRE — fail-fast by design |
| `Get<T>()` для незарегистрированного типа | Возврат `null` из `ComplexModel` |
| Потеря фокуса (`Unfocused`) | Автоматический `SetPause(true)` |
| `Stopping` | `Dispose()` контроллера |
| Editor-режим (не Play Mode) | `GetData()` создаёт временную модель, вызывает `OnEditorGetData` |

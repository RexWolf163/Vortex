# GameCore

**Namespace:** `Vortex.Sdk.Core.GameCore`
**Assembly:** `ru.vortex.sdk.game.core`

## Назначение

Центральная шина игровой сессии. Управляет состояниями игры (`Off`, `Play`, `Win`, `Fail`, `Paused`, `Loading`), хранит составную модель данных и предоставляет единый API для всех подсистем, работающих с игровым процессом.

Возможности:
- Управление жизненным циклом игры: запуск, пауза, выход
- Реактивная модель данных с подпиской через `Subscribe` / `Unsubscribe`
- Составная модель `GameModel` — расширяемый контейнер через `IGameData`
- Автоматическая пауза при потере фокуса приложением
- Сериализация / десериализация состояния
- Editor-режим: создание модели без запуска приложения

Вне ответственности:
- Конкретная игровая механика
- Визуальное представление
- Сохранение на диск (используется `SaveSystem` из Core)

## Зависимости

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`, `IReactiveData`
- `Vortex.Core.System.Abstractions.ReactiveValues` — реактивные значения
- `Vortex.Core.AppSystem.Bus` — `App`, `AppStates`
- `Vortex.Core.ComplexModelSystem` — `ComplexModel<T>`

### Unity
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController.Accumulate`

## Архитектура

```
GameController (Singleton, static API)
├── GameModel (ComplexModel<IGameData>)
│   ├── State: GameStates
│   └── Dictionary<Type, IGameData>   ← пакеты регистрируют свои данные
├── OnNewGame                          ← событие новой игры
├── OnGameStateChanged                 ← событие смены состояния
├── Subscribe / Unsubscribe            ← реактивная подписка на данные
└── Serialize / Deserialize            ← JSON сериализация модели
```

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `GameController` | `Singleton<T>`, partial, static | Шина управления игрой |
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
- `GameController.Subscribe(Action)` — подписка на обновление данных

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
GameController.Subscribe(OnDataUpdated);
// ...
GameController.Unsubscribe(OnDataUpdated);
```

### Подписка на состояния

```csharp
GameController.OnGameStateChanged += () =>
{
    var state = GameController.GetState();
    // ...
};
```

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

# MiniGamesSystem

**Namespace:** `Vortex.Sdk.MiniGamesSystem.*`
**Assembly:** `ru.vortex.sdk.minigames` (фреймворк) + отдельные сборки для каждой игры

## Назначение

Фреймворк встраивания миниигр в основной проект. Предоставляет единый жизненный цикл, управление состояниями, статистику и шаблонизацию для создания новых миниигр.

Возможности:
- Единый жизненный цикл: инициализация → запуск → игровой процесс → завершение (победа/поражение)
- Управление состояниями (`Off`, `Play`, `Win`, `Fail`, `Paused`, `Loading`) с реактивным оповещением
- Автоматическая пауза при потере фокуса приложением
- Сбор статистики по запускам, победам и поражениям
- Подключение View через конфигурацию (префаб задаётся в ScriptableObject)
- Подмена контроллера через dropdown в инспекторе (direct DI через конфигурацию)
- Генерация бойлерплейта новой миниигры из `.vtp`-шаблона
- Шаблонные реализации: Gym, Painting, Puzzle

Вне ответственности:
- Игровая логика конкретной миниигры (реализуется в контроллере наследника)
- Загрузка/выгрузка сцен и ресурсов
- Сетевое взаимодействие
- Сохранение прогресса миниигр между сессиями (Save/Load заложен, но не задействован)

## Зависимости

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`, `IDataStorage`
- `Vortex.Core.AppSystem.Bus` — `App.OnStateChanged`
- `Vortex.Core.Extensions.LogicExtensions` — утилиты строк, сериализации

### Unity
- `Vortex.Unity.Extensions.Abstractions` — `MonoBehaviourSingleton`
- `Vortex.Unity.AppSystem.System.TimeSystem` — `Timer`, `TimeController`
- `Vortex.Unity.UI` — `UIStateSwitcher`, `UIComponent`, `Pool`, `DataStorage`
- `Vortex.Unity.EditorTools` — `ClassFilter`, `AutoLink`, `InfoBubble`

### SDK
- `Vortex.Sdk.Core.GameCore` — `GameController`, `GameModel.IGameData`, `GameStates`

### Внешние
- **Odin Inspector** — атрибуты инспектора (`InfoBox`, `ValueDropdown`, `GUIColor`)

## Архитектура

```
┌─────────────────────────────────────────────┐
│              Внешний код                     │
│     await XxxGameHub.Play(config)            │
│     hub.OnWin += ...                         │
└──────────┬──────────────────┬───────────────┘
           │                  │
           ▼                  ▼
┌────────────────────┐  ┌─────────────────────┐
│       Hub          │  │  MiniGamesController │
│ (MonoBehaviour-    │  │  (статистика,        │
│  Singleton)        │  │   регистрация)       │
│                    │  └─────────────────────┘
│ - хранит config    │
│ - создаёт Controller
│ - IDataStorage     │
│ - async Play()     │
└────────┬───────────┘
         │ создаёт (Activator)
         ▼
┌────────────────────┐
│    Controller      │
│ (Singleton, POCO)  │
│                    │
│ - владеет Data     │
│ - SetState()       │
│ - игровая логика   │
│ - реакция на       │
│   AppState         │
└────────┬───────────┘
         │ читает / изменяет
         ▼
┌────────────────────┐
│      Data          │
│ (MiniGameData)     │
│                    │
│ - State            │
│ - OnGameStateChanged
│ - OnUpdated        │
│ - параметры игры   │
└────────┬───────────┘
         │ подписка на события
         ▼
┌────────────────────┐
│      View          │
│ (MonoBehaviour)    │
│                    │
│ - получает данные  │
│   через IDataStorage
│ - отображает       │
│ - действия через   │
│   ExtLogic →       │
│   Controller       │
└────────────────────┘
```

Поток данных однонаправленный: Controller → Data → View.
Обратная связь: View → ExtLogic (extension method) → Controller.

### Ключевые концепции

| Концепция | Описание |
|-----------|----------|
| **Hub** | Точка входа для внешнего кода. `MonoBehaviourSingleton`. Хранит конфигурацию, создаёт контроллер через `Activator.CreateInstance`, реализует `IDataStorage`. Один экземпляр на миниигру |
| **Controller** | POCO-синглтон. Владеет моделью данных, реализует игровую логику. Единственный компонент, который изменяет состояние через `SetState()` |
| **Data** | Наследник `MiniGameData`. Содержит состояние, параметры, события. Изменяется только контроллером |
| **GeneralConfig** | `ScriptableObject` с настройками: тип контроллера (строка), префаб View, уровни сложности, таймеры |
| **ExtLogic** | Статический класс с extension-методами на модели данных. Пробрасывает действия View в контроллер, не раскрывая контроллер View-слою |
| **MiniGameViewContainer** | Инстанциирует префаб View из конфигурации и связывает его с `IDataStorage` |
| **MiniGameObserver** | Подписывается на события Hub'а и транслирует их в `MiniGamesController` для статистики. Гарантирует атомарную отписку |

### Компоненты фреймворка

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `MiniGamesController` | static | Регистрация хабов, сбор статистики |
| `MiniGameHub<T,TD,TCf,TC>` | `MonoBehaviourSingleton`, abstract | Базовый хаб |
| `MiniGameController<T,TU>` | `Singleton<T>`, abstract | Базовый контроллер |
| `MiniGameData` | abstract | Базовая модель данных |
| `MiniGameObserver` | internal | Подписки на Hub → статистика |
| `MiniGameStates` | enum | Off, Play, Win, Fail, Paused, Loading |
| `MiniGameStatisticData` | POCO | Статистика одной миниигры |
| `MiniGamesStatisticsData` | `IGameData` | Индекс статистики всех миниигр |
| `FieldSize` | struct | Размер поля: columns x rows |
| `IMiniGameConfig` | interface | Контракт конфигурации: GetView(), GetController() |
| `IMiniGameController<T>` | interface | Контракт контроллера: Init, Play, Exit, Pause, Cheats |
| `IMiniGameHub` | interface | Контракт хаба: события OnWin/OnFail/OnStart |
| `IGameModelWithTimer` | interface | Доступ к таймеру из модели |
| `IHaveGodMode` | interface | Режим бога (пропуск потери HP и т.п.) |

### Переиспользуемые обработчики

| Класс | Назначение |
|-------|-----------|
| `GameTimerView` | Slider-отображение прогресса таймера |
| `MiniGameStateSwitcher` | `UIStateSwitcher` по `MiniGameStates` |
| `MockUpStateSwitcher` | Debug-переключатель состояния кнопкой |
| `MiniGameCheatWinHandler` | Кнопка чит-победы |
| `MiniGameCheatFailHandler` | Кнопка чит-поражения |
| `MiniGameViewContainer` | Инстанциирование View из конфигурации |

## Контракт

### Вход
- `ScriptableObject`-конфиг: контроллер, View-префаб, уровни сложности
- Hub-компонент на сцене с назначенным конфигом
- PlayConfig-структура с параметрами запуска (опционально)

### Выход
- `async UniTask Play()` — завершается при переходе в `Off`
- События `OnWin`, `OnFail`, `OnStart` на Hub'е
- Статистика в `MiniGamesStatisticsData` через `GameController.Get<>()`

### Гарантии
- При `GameStates.Paused` — автоматическая пауза миниигры
- При `GameStates.Off/Fail/Win/Loading` — состояние миниигры сбрасывается в `Off`
- `MiniGameObserver` гарантирует отписку при дерегистрации
- Регистрация в `MiniGamesController` — автоматическая в `Awake()` / `OnDestroy()`

### Ограничения
- Один экземпляр миниигры на приложение (Hub — Singleton, Controller — Singleton)
- Контроллер создаётся через `Activator.CreateInstance` — нужен публичный конструктор без параметров
- Тип контроллера задаётся строкой — при переименовании / смене assembly будет `NullReferenceException`
- Префаб View должен содержать `DataStorage` на корневом GameObject
- `Data.State` изменяется только через `Controller.SetState()` (кроме debug-инструментов)

## Использование

### Создание новой миниигры из шаблона

1. В окне Project выбрать целевую папку
2. **Assets → Create → Vortex Templates → MiniGame**
3. Ввести название миниигры
4. Шаблонизатор создаст структуру файлов

### Структура реализации

Каждая миниигра повторяет паттерн:

```
MyGame/
├── Abstractions/
│   └── IMyGameController.cs       ← интерфейс контроллера
├── Config/
│   └── MyGameGeneralConfig.cs     ← ScriptableObject-конфигурация
├── Controllers/
│   ├── MyGameController.cs        ← игровая логика
│   └── MyGameDataExtLogic.cs      ← extension-методы View → Controller
├── Models/
│   ├── MyGameData.cs              ← модель данных
│   └── MyGamePlayConfig.cs        ← параметры запуска
├── View/
│   └── MyGameFieldView.cs         ← визуальное представление
├── Editor/
│   └── MyGameMenuController.cs    ← editor-утилиты
└── Prefabs/
```

### Реализация контроллера

```csharp
public class MyGameController : MiniGameController<MyGameController, MyGameData>, IMyGameController
{
    public override void Play()
    {
        Data.StartTimer(OnTimerRunOut);
        Data.OnGameStateChanged -= OnStateLogic;
        Data.OnGameStateChanged += OnStateLogic;
        SetState(MiniGameStates.Play);
    }

    public void PlayerAction(MyPieceData piece)
    {
        // игровая логика
        if (CheckWinCondition())
            SetState(MiniGameStates.Win);
    }

    private void OnTimerRunOut()
    {
        SetState(CheckWinCondition() ? MiniGameStates.Win : MiniGameStates.Fail);
    }

    private void OnStateLogic(MiniGameStates state)
    {
        switch (state)
        {
            case MiniGameStates.Play:
                Data.Timer.Resume();
                break;
            case MiniGameStates.Off:
                Data.OnGameStateChanged -= OnStateLogic;
                break;
            default:
                if (!Data.Timer.IsComplete)
                    Data.Timer.SetPause();
                break;
        }
    }
}
```

### Связка View → Controller через ExtLogic

```csharp
// ExtLogic
public static class MyGameDataExtLogic
{
    public static void PlayerAction(this MyPieceData piece) =>
        MiniGamesController.GetController<IMyGameController>().PlayerAction(piece);
}

// View вызывает extension-метод на данных — не знает о контроллере
private void OnClick() => _data.PlayerAction();
```

### Запуск из внешнего кода

```csharp
await MyGameHub.Play(new MyGamePlayConfig { Difficulty = 1 });

MyGameHub.Instance.OnWin += HandleWin;
MyGameHub.Instance.OnFail += HandleFail;
```

## Шаблонные реализации

Пакет содержит три шаблонные миниигры — примеры стандартизированного паттерна:

| Игра | Assembly | Механика |
|------|----------|----------|
| **Gym** | `ru.vortex.minigames.gym` | Тайминг-слайдер, фазы (Ready → Push → Return), анимации тренера (Spine / VideoPlayer) |
| **Painting** | `ru.vortex.minigames.paint` | Клеточное поле, инверсия соседних клеток по кресту, объектный пул |
| **Puzzle** | `ru.vortex.minigames.puzzle` | Нарезка текстуры, Fisher-Yates shuffle, drag & drop с `AsyncTween` |

Все реализации следуют одному шаблону и служат справочными примерами для создания новых миниигр.

## Редакторные инструменты

- **Генератор из шаблона** (`MiniGameTemplateMenu`) — создаёт полную структуру файлов новой миниигры
- **Валидация контроллера** — конфиги (`GymGeneralConfig`, `PaintingGeneralConfig`, `PuzzleGeneralConfig`) подсвечивают поле контроллера красным при невалидном типе
- **Синхронизация массивов** — `OnLevelsChanged` автоматически подгоняет длины массивов таймеров, размеров поля и пр. под количество уровней сложности
- **Debug-кнопки** — `MockUpStateSwitcher`, `MiniGameCheatWinHandler`, `MiniGameCheatFailHandler` для тестирования без прохождения

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Config не назначен в Hub | `LogError`, Hub не регистрируется, `Play()` выбросит исключение |
| Тип контроллера не найден | `NullReferenceException` при первом обращении к `Controller` |
| View-префаб без `DataStorage` на корне | `NullReferenceException` в `MiniGameViewContainer.Start()` |
| `Play()` при активной игре | Новый цикл поверх текущего — поведение не определено |
| Потеря фокуса при игре | Автоматическая пауза через `GameController` → `AppStateCheck` |
| `difficulty` превышает длину массивов конфига | `IndexOutOfRangeException` (fail-fast) |
| `difficulty` превышает длину `levels` | Возврат `null` |

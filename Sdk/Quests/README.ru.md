# Quests

**Namespace:** `Vortex.Sdk.Quests`
**Assembly:** `ru.vortex.sdk.game.quests`

## Назначение

Система квестов с асинхронным выполнением. Управляет жизненным циклом квестов: проверка условий старта, последовательное выполнение логик, завершение с результатом.

Возможности:
- Жизненный цикл: `Locked` → `Ready` → `InProgress` → `Reward` → `Completed` / `Failed`
- Условия старта — AND-группы произвольных проверок
- Асинхронное последовательное выполнение логик через UniTask
- Автозапуск квестов при выполнении условий
- Рекурсивная перепроверка условий при завершении квестов (с предохранителем глубины)
- Реактивные слушатели — подписка на `IReactiveData` для автоматической перепроверки условий
- Режим `UnFailable` — при провале квест возвращается в `Locked` вместо `Failed`
- Отмена всех активных квестов через `CancellationToken` при новой игре
- Восстановление квестов при загрузке — пропуск логик до сохранённого `SavePoint`

Вне ответственности:
- Конкретная логика квестов (реализуется в наследниках `QuestLogic`)
- Конкретные условия старта (реализуются в наследниках `QuestConditionLogic`)
- UI квестов (только `IDataStorage` для привязки)
- Конкретная реализация наград (реализуется в наследниках `QuestRewardLogic`)

## Зависимости

### Core
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`, `RecordPreset`
- `Vortex.Core.System.Abstractions` — `IDataStorage`, `IReactiveData`
- `Vortex.Core.Extensions.LogicExtensions` — сериализация

### SDK
- `Vortex.Sdk.Core.GameCore` — `GameController`, `GameModel.IGameData`, `OnNewGame`

### Внешние
- **UniTask** — асинхронное выполнение логик
- **Odin Inspector** — атрибуты инспектора

## Архитектура

```
QuestController (static, partial)
├── QuestModels : IGameData                       ← регистрируется в GameModel
│   └── Dictionary<string, QuestModel> Index      ← multi-instance копии из Database
│       ├── State: QuestState
│       ├── StartConditions[]                     ← AND-группы условий
│       ├── Logics[]                              ← последовательная очередь
│       ├── Step: byte                             ← ключ SavePoint для восстановления
│       ├── Autorun                               ← автозапуск при Ready
│       └── UnFailable                            ← возврат в Locked при провале
├── ActiveQuests                                  ← Dictionary<QuestModel, UniTask>
├── CompletedQuests                               ← Dictionary<string, QuestModel>
└── Listeners                                     ← IReactiveData → автоперепроверка
```

### Жизненный цикл квеста

```
Locked ──[условия выполнены]──→ Ready ──[Run()]──→ InProgress
  ↑                               │                    │
  │                               │ (Autorun)          ├──[все логики OK, есть награды]──→ Reward ──[GiveRewards()]──→ Completed
  │                               │                    │
  └────────────────────[UnFailable]├──[логика Failed]  ├──[все логики OK, нет наград]──→ Completed
                                  │                    │
                                  └────────────────────└──[логика Failed]──→ Failed
```

### Восстановление при загрузке

При `LoadGame()` квесты в состоянии `InProgress` восстанавливаются через `RestoreQuest`:

```
Run(quest) ──[State == InProgress]──→ RestoreQuest()
                                        ├── Step != 0 → пропуск логик до SavePoint с Key == Step
                                        └── Step == 0 → выполнение с начала
```

`SavePoint` — маркерная логика, которая при выполнении сохраняет свой `Key` в `QuestModel.Step`. При восстановлении все логики до соответствующего `SavePoint` (включительно) пропускаются.

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `QuestController` | static, partial | Контроллер жизненного цикла |
| `QuestControllerExtIndex` | partial | Запросы: `IsComplete(id)` |
| `QuestControllerExtEditor` | partial, `#if UNITY_EDITOR` | Editor-интеграция |
| `QuestModel` | `Record` | Модель квеста: состояние, условия, логики |
| `QuestModels` | `IGameData` | Контейнер индекса квестов |
| `QuestPreset` | `RecordPreset<QuestModel>` | ScriptableObject-пресет для Database |
| `QuestState` | `enum` | Locked, Ready, InProgress, Reward, Completed, Failed |
| `QuestLogic` | `abstract` | Атомарная логика: `UniTask<bool> Run(CancellationToken)` |
| `SavePoint` | `QuestLogic` | Маркер точки сохранения: сохраняет `Key` в `QuestModel.Step` |
| `QuestConditionLogic` | `abstract` | Условие: `bool Check()` |
| `QuestConditions` | `Serializable` | AND-группа условий |
| `QuestCompleted` | `QuestConditionLogic` | Условие: квест с заданным ID завершён |
| `QuestDataStorage` | `MonoBehaviour`, `IDataStorage` | Привязка UI к квесту по GUID |
| `RunQuestHandler` | `MonoBehaviour` | Запуск квеста через `IDataStorage` |

## Контракт

### Вход
- `QuestPreset` — ScriptableObject, зарегистрированный в Database как MultiInstance
- `GameController.OnNewGame` — триггер новой игры
- `GameController.OnLoadData` — триггер загрузки сохранения

### Выход
- `QuestController.OnUpdateData` — событие изменений
- `QuestController.IsComplete(id)` — проверка завершения
- `QuestModel.OnStateUpdated` — событие смены состояния конкретного квеста

### Гарантии
- Логики выполняются строго последовательно
- При `NewGame()` и `LoadGame()` все активные квесты отменяются через `CancellationToken`
- Рекурсивная перепроверка условий ограничена глубиной 10
- `UnFailable`-квест при провале возвращается в `Locked` и не попадает в `CompletedQuests` — может быть перезапущен
- `Run()` на квест в состоянии `Ready` — запускает `RunQuest`; в состоянии `InProgress` — запускает `RestoreQuest`; в ином состоянии — логируется ошибка, вызов игнорируется

### Ограничения
- Квесты — строго MultiInstance записи (каждая игра получает свежие копии)
- Один `CancellationTokenSource` на все квесты — отмена групповая
- `QuestConditionLogic.Check()` — синхронный, не поддерживает async-условия

## Использование

### Создание квеста

1. Создать наследника `QuestLogic`:
```csharp
[Serializable]
public class CollectItemsLogic : QuestLogic
{
    [SerializeField] private int targetCount;

    public override async UniTask<bool> Run(CancellationToken token)
    {
        while (Inventory.Count < targetCount)
        {
            if (token.IsCancellationRequested) return false;
            await UniTask.Yield(token);
        }
        return true;
    }
}
```

2. Создать наследника `QuestConditionLogic` (опционально):
```csharp
[Serializable]
public class LevelReached : QuestConditionLogic
{
    [SerializeField] private int level;
    public override bool Check() => PlayerData.Level >= level;
}
```

3. Создать `QuestPreset` через **Assets → Create → Database → Quest Preset**
4. В инспекторе настроить: условия старта, логики, autorun, unFailable

### Реактивная перепроверка условий

```csharp
// Подписка: при изменении данных — автоперепроверка условий квестов
QuestController.SetListener(GameController.Instance, this);

// Отписка
QuestController.RemoveListener(this);
```

### Привязка UI

Разместить `QuestDataStorage` на сцене, указать GUID квеста. View-компоненты получают `QuestModel` через `IDataStorage.GetData<QuestModel>()`.

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Все условия пусты | Квест сразу получает `Ready` |
| `Autorun` + условия выполнены | Квест запускается автоматически при `NewGame`, `LoadGame` или вызове `CheckQuestStartConditions()` |
| Логика возвращает `false`, `UnFailable = true` | Состояние → `Locked`, в `CompletedQuests` не добавляется (перезапуск возможен) |
| Логика возвращает `false`, `UnFailable = false` | Состояние → `Failed`, квест в `CompletedQuests` |
| `NewGame()` / `LoadGame()` при активных квестах | Все отменяются через `CancellationToken` |
| Рекурсия условий > 10 уровней | Прерывается (предохранитель) |
| `Run()` на квест в `InProgress` | Восстановление через `RestoreQuest` — пропуск логик до `SavePoint` |
| Квест завершён → условия другого квеста зависят от него | Рекурсивная перепроверка через `CheckQuestStartConditions` |

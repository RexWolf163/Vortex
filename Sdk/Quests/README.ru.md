# Quests

**Namespace:** `Vortex.Sdk.Quests`
**Assembly:** `ru.vortex.sdk.game.quests`

## Активация

Пакет активируется через `SdkSettings` (меню **Tools → Vortex → Configs → SDK Settings**), тоггл **`questsSdk`**. Тоггл управляет define-символом `USING_VORTEX_QUESTS`, который указан в `defineConstraints` asmdef'а — при выключении пакет не компилируется и его типы недоступны.

Канон описания активации SDK-пакетов: `Vortex/Sdk/SdkSettingsSystem/README.ru.md`.

## Назначение

Система квестов с асинхронным выполнением. Управляет жизненным циклом квестов: проверка условий старта, последовательное выполнение логик, завершение с результатом.

Возможности:
- Жизненный цикл: `Unset` → `Locked` → `Ready` → `InProgress` → `Reward` → `Completed` / `Failed`
- Условия старта — AND-группы произвольных проверок с автоподпиской (`InitListeners`/`DisposeListeners`)
- Асинхронное последовательное выполнение логик через UniTask
- Автозапуск квестов при выполнении условий
- Рекурсивная перепроверка условий при завершении квестов (с предохранителем глубины)
- Защита от проверки в неактивных состояниях игры (`GameStates.Off`, `Loading`)
- Режим `UnFailable` — при провале квест возвращается в `Locked` вместо `Failed`
- Отмена всех активных квестов через `CancellationToken` при новой игре
- Восстановление квестов при загрузке — пропуск логик до сохранённого `SavePoint`
- Условия прерывания (`InterruptConditions`) и состояние `Blocked` — квест блокируется из любого «живого» состояния (`Locked`/`Ready`/`InProgress`); флаг `BlockRemovable` задаёт обратимость блока

Вне ответственности:
- Конкретная логика квестов (реализуется в наследниках `QuestLogic`)
- Конкретные условия старта (реализуются в наследниках `QuestConditionLogic`)
- UI квестов (только `IDataStorage` для привязки)
- Конкретная реализация наград (реализуется в наследниках `QuestRewardLogic`)

## Зависимости

### Core
- `Vortex.Core.DatabaseSystem` — `Record`, `RecordPreset`
- `Vortex.Core.System.Abstractions` — `IDataStorage`
- `Vortex.Core.Extensions.ReactiveValues` — `IReactiveData` (для `SetListener`)
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
│       ├── State: QuestState (Unset→Locked→Ready→InProgress→…→Blocked)
│       ├── StartConditions[]                     ← AND-группы (и между группами AND)
│       ├── InterruptConditions[]                 ← OR-группы, приоритетнее старта → Blocked
│       ├── Logics[]                              ← последовательная очередь
│       ├── Step: byte                             ← ключ SavePoint для восстановления
│       ├── Autorun                               ← автозапуск при Ready
│       ├── UnFailable                            ← возврат в Locked при провале
│       └── BlockRemovable                        ← обратимость Blocked (иначе — навсегда)
├── ActiveQuests                                  ← Dictionary<QuestModel, UniTask>
├── ActiveCts                                     ← Dictionary<QuestModel, CTS> (пер-квестовая отмена)
├── CompletedQuests                               ← Dictionary<string, QuestModel>
├── Listeners                                     ← IReactiveData → автоперепроверка (альтернативный API)
└── CheckState()                                  ← подписка на OnGameStateChanged (Reset при Off/Loading)
```

### Жизненный цикл квеста

```
Unset ──[NewGame/LoadGame]──→ Locked ──[условия выполнены]──→ Ready ──[Run()]──→ InProgress
                                ↑                               │                    │
                                │                               │ (Autorun)          ├──[все логики OK, есть награды]──→ Reward ──[GiveRewards()]──→ Completed
                                │                               │                    │
                                └────────────────────[UnFailable]├──[логика Failed]  ├──[все логики OK, нет наград]──→ Completed
                                                                │                    │
                                                                └────────────────────└──[логика Failed]──→ Failed
```

`Unset` — начальное состояние после создания из пресета. При `NewGame`/`LoadGame` безусловно переводится в `Locked`. Полезен для отлова новых квестов на существующих сейвах.

### Восстановление при загрузке

При `LoadGame()` квесты в состоянии `InProgress` восстанавливаются через `RestoreQuest`:

```
Run(quest) ──[State == InProgress]──→ RestoreQuest()
                                        ├── Step != 0 → пропуск логик до SavePoint с Key == Step
                                        └── Step == 0 → выполнение с начала
```

`SavePoint` — маркерная логика, которая при выполнении сохраняет свой `Key` в `QuestModel.Step`. При восстановлении все логики до соответствующего `SavePoint` (включительно) пропускаются.

### Прерывание квеста (`Blocked`)

Второй набор условий — `InterruptConditions` — отвечает на вопрос «когда запретить» (условия старта — «когда открыть»). Свёртка **зеркальна старту**: **OR между группами** (сработала любая группа — квест блокируется), AND внутри группы. Пустой набор ⇒ квест непрерываем (обратная совместимость: существующие квесты не меняют поведение).

Проверка прерывания идёт **первой строкой** `CheckQuestStartConditions`, до проверки старта, — поэтому запрет всегда перебивает открытие. Квест уходит в `Blocked` из любого «живого» состояния (`Locked`/`Ready`/`InProgress`). Если он был `InProgress` — его логика немедленно отменяется (пер-квестовый `CancellationToken`), прогресс сбрасывается (`Step = 0`).

```
Locked / Ready / InProgress ──[любая группа прерывания = true]──→ Blocked
        ↑                                                            │
        └──[BlockRemovable && все группы прерывания = false]─────────┘
```

**`BlockRemovable`:**
- `false` (по умолчанию) — `Blocked` держится до `Reset`/новой игры («стоп-кран навсегда»).
- `true` — когда условия прерывания перестали выполняться, квест возвращается в `Locked` и заново проходит турникет старта. Повторный вход — **строго с нуля** (`RunQuest`, `Step = 0`), сохранённый прогресс не восстанавливается.

Терминальные состояния (`Reward`/`Completed`/`Failed`) прерыванию не подлежат — только «живые».

> **Контракт условий прерывания (INV-7).** Любое пробуждение перепроверки обязано проходить через `CheckQuestStartConditions` (прямой `+=` или `SetListener`) — из этой единой точки перечитываются и старт, и прерывание. Условие, будящее иной символ, выпадет из interrupt-логики.

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `QuestController` | static, partial | Контроллер жизненного цикла |
| `QuestControllerExtIndex` | partial | Запросы: `IsComplete(id)` |
| `QuestControllerExtEditor` | partial, `#if UNITY_EDITOR` | Editor-интеграция |
| `QuestModel` | `Record` | Модель квеста: состояние, условия, логики |
| `QuestModels` | `IGameData` | Контейнер индекса квестов |
| `QuestPreset` | `RecordPreset<QuestModel>` | ScriptableObject-пресет для Database |
| `QuestState` | `enum` | Unset, Locked, Ready, InProgress, Reward, Completed, Failed, Blocked |
| `QuestLogic` | `abstract` | Атомарная логика: `UniTask<bool> Run(CancellationToken)` |
| `SavePoint` | `QuestLogic` | Маркер точки сохранения: сохраняет `Key` в `QuestModel.Step` |
| `AlwaysFail` | `QuestLogic` | Жёсткий `false`: у `UnFailable`-квеста зацикливает его (Locked → рестарт) |
| `QuestConditionLogic` | `abstract` | Условие: `Check()`, `InitListeners()`, `DisposeListeners()` |
| `QuestConditions` | `Serializable` | Группа условий: `Check()` по AND. Свёртка между группами — AND (старт) или OR (прерывание) — на уровне контроллера |
| `OrNotCondition` | `QuestConditionLogic` | Комбинатор над вложенными условиями с режимом (`OR` / `NOT`=NOR / `XOR`=ровно одно). Подписывается на **все** дочерние (альтернативность). Выражает не-AND-логику внутри AND-группы; `NOT` над одним вложенным заменяет убранный флаг `inverted` у условий |
| `QuestCompleted` | `QuestConditionLogic` | Условие: квест с заданным ID завершён |
| `QuestDataStorage` | `MonoBehaviour`, `IDataStorage` | Привязка UI к квесту по GUID |
| `RunQuestHandler` | `MonoBehaviour` | Запуск квеста через `IDataStorage` |

## Контракт

### Вход
- `QuestPreset` — ScriptableObject, зарегистрированный в Database как MultiInstance
- `GameController.OnNewGame` — триггер новой игры
- `GameController.OnLoadGame` — триггер загрузки сохранения

### Выход
- `QuestController.OnUpdateData` — событие изменений
- `QuestController.IsComplete(id)` — проверка завершения
- `QuestModel.OnStateUpdated` — событие смены состояния конкретного квеста

### Гарантии
- Логики выполняются строго последовательно
- При `NewGame()` и `LoadGame()` все активные квесты отменяются через `CancellationToken`
- `CheckQuestStartConditions` блокируется при `GameStates.Off` (вызывает `Reset()` на всех квестах) и `Loading`
- Рекурсивная перепроверка условий ограничена глубиной 10
- `UnFailable`-квест при провале возвращается в `Locked` и не попадает в `CompletedQuests` — может быть перезапущен
- `Run()` на квест в состоянии `Ready` — запускает `RunQuest`; в состоянии `InProgress` — запускает `RestoreQuest`; в ином состоянии — логируется ошибка, вызов игнорируется
- При запуске квеста подписки условий старта снимаются (`DisposeListeners`); подписки прерывания живут через `Locked → Ready → InProgress`
- Прерывание приоритетнее старта: interrupt-пасс — первая строка `CheckQuestStartConditions`, до старт-пасса и на каждой итерации settle-рекурсии
- Прерванный `InProgress`-квест теряет прогресс (`Step = 0`); при обратимом блоке возврат — строго с нуля через `RunQuest`

### Ограничения
- Квесты — строго MultiInstance записи (каждая игра получает свежие копии)
- Пер-квестовые linked-CTS: отмена одного квеста (при блокировке) не трогает остальные; глобальный `CancellationTokenSource` — для группового teardown (новая игра/загрузка)
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

### Зацикленный квест (`AlwaysFail`)

`AlwaysFail` — логика с жёстким `false`. У квеста с **`unFailable = true`** она превращает завершение в перезапуск: `false` → квест уходит в `Locked` (а не `Failed`), не попадает в `CompletedQuests`, и перепроверка условий старта запускает его заново.

Собрать циклический квест:
1. Включить у квеста **`unFailable`**.
2. Добавить **`AlwaysFail`** **последней** логикой (после полезной работы и наград).
3. Частоту цикла задают условия старта: пока они истинны — квест крутится (один прогон за кадр, `AlwaysFail` уступает кадр через `UniTask.Yield`, поэтому кадр не виснет); транзиентное условие (событие) — квест ждёт следующего срабатывания.

Форма списка логик: `[…полезные логики…] → [награда] → AlwaysFail`. Без `unFailable` `AlwaysFail` просто завершит квест как `Failed`.

### Условия прерывания и `Blocked`

`InterruptConditions` настраиваются в пресете рядом со стартовыми, но свёртка между группами — **OR** (сработала любая → блок). Тип условия тот же, что у старта (`QuestConditionLogic`).

```csharp
// Квест доступен, ПОКА не пройден ни один из боссов.
// interruptConditions: [ группа{ BossDefeated("boss_1") },
//                        группа{ BossDefeated("boss_2") } ]   // OR: любой босс блокирует
// blockRemovable = false                                       // блок навсегда
```

- Пустой `interruptConditions` ⇒ квест непрерываем (как раньше).
- Прерывание срабатывает и на уже идущем квесте: логика отменяется, прогресс сбрасывается.
- `blockRemovable = true` — при снятии условий квест вернётся в `Locked` и стартует заново с нуля.

### Реактивная перепроверка условий

Каждый `QuestConditionLogic` управляет своими подписками через `InitListeners()`/`DisposeListeners()`:

```csharp
[Serializable]
public class ExampleStarted : QuestConditionLogic
{
    public override bool Check() => ExampleEngine.IsPlaying;

    public override void InitListeners()
    {
        ExampleEngine.OnStart += QuestController.CheckQuestStartConditions;
    }

    public override void DisposeListeners()
    {
        ExampleEngine.OnStart -= QuestController.CheckQuestStartConditions;
    }
}
```

`QuestConditions.Check()` автоматически вызывает `DisposeListeners` перед проверкой и `InitListeners` только для условий, вернувших `false` — подписки живут только пока условие не выполнено.

#### Почему оба уровня — AND (атомарное детерминированное отслеживание)

И условия внутри группы (`QuestConditions.Check` → все по AND), и группы между собой (`CheckQuestStart` → `StartConditions.All`) объединяются по **AND с ленивым short-circuit**. Это осознанный design, а не избыточность: в каждый момент квест подписан ровно на **одно** невыполненное условие — «блокер» (первое `false` в первой невыполненной группе), а остальные проверяются только когда оно откроется (перепроверка каскадом продвигает подписку к следующему блокеру). Отсюда:

- **атомарность** — одна активная подписка за раз;
- **детерминированность** — блокер всегда однозначен (слева направо).

> ⚠️ **Не менять внутренний уровень на OR.** OR-группа истинна при выполнении *любого* из условий, значит становится истинной при открытии *любого* из них — пришлось бы подписываться на **все** условия невыполненной группы (однозначного блокера нет). Это ломает атомарное отслеживание. Организационная группировка (имя группы) — **не** логический OR: дерево условий вычисляется как **плоское AND**.

Чтобы выразить OR / NOR / XOR **внутри** группы, не ломая эту атомарность, оберните условия в **`OrNotCondition`** — одно `QuestConditionLogic`, сворачивающее свои вложенные по выбранному режиму (`OR` = любое, `NOT` = ни одного/NOR, `XOR` = ровно одно). Оно осознанно подписывается на **все** вложенные (альтернативность — результат может измениться от любого), поэтому атомарная AND-модель самой группы не страдает: OR живёт *внутри одного* условия, «плоский AND» группы сохраняется. Вложения произвольны (`OrNotCondition` может содержать другой). Режим `NOT` над одним вложенным — замена убранного флага `inverted` (например, «квест **не** завершён» = `OrNotCondition{ Not, [ QuestCompleted(id) ] }`).

Альтернативный путь — `SetListener`/`RemoveListener` для `IReactiveData`:

```csharp
[Serializable]
public class ExampleVariableCondition : QuestConditionLogic
{
    public override bool Check() => /* ... */;

    public override void InitListeners()
    {
        QuestController.SetListener(GameController.Instance, this);
        QuestController.SetListener(ExampleVariableListener.Instance, this);
    }

    public override void DisposeListeners()
    {
        QuestController.RemoveListener(this);
    }
}
```

`SetListener` подписывается на `IReactiveData.OnUpdateData` с подсчётом ссылок — одна подписка на `IReactiveData` независимо от количества условий. `RemoveListener` снимает подписку когда источников не осталось.

### Привязка UI

Разместить `QuestDataStorage` на сцене, указать GUID квеста. View-компоненты получают `QuestModel` через `IDataStorage.GetData<QuestModel>()`.

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Новый квест на существующем сейве | Состояние `Unset`, при `LoadGame` переводится в `Locked` и участвует в проверке условий |
| Все условия пусты | Квест сразу получает `Ready` |
| `Autorun` + условия выполнены | Квест запускается автоматически при `NewGame`, `LoadGame` или вызове `CheckQuestStartConditions()` |
| Логика возвращает `false`, `UnFailable = true` | Состояние → `Locked`, в `CompletedQuests` не добавляется (перезапуск возможен) |
| Логика возвращает `false`, `UnFailable = false` | Состояние → `Failed`, квест в `CompletedQuests` |
| `NewGame()` / `LoadGame()` при активных квестах | Все отменяются через `CancellationToken`, подписки снимаются |
| `GameStates.Off` | `CheckQuestStartConditions` вызывает `Reset()` на всех квестах, проверка не выполняется |
| `GameStates.Loading` | `CheckQuestStartConditions` пропускается |
| Рекурсия условий > 10 уровней | Прерывается (предохранитель) |
| `Run()` на квест в `InProgress` | Восстановление через `RestoreQuest` — пропуск логик до `SavePoint` |
| Квест завершён → условия другого квеста зависят от него | Рекурсивная перепроверка через `CheckQuestStartConditions` |
| Пустой `InterruptConditions` | Квест непрерываем — поведение как до фичи |
| Условие прерывания сработало на `InProgress` | Логика отменяется (пер-квестовый CTS), `Step = 0`, состояние → `Blocked` |
| Прерывание vs старт при одновременной истинности | Побеждает прерывание (interrupt-пасс раньше старт-пасса) |
| `BlockRemovable = false`, условие снялось | Остаётся `Blocked` (до `Reset`/новой игры) |
| `BlockRemovable = true`, условие снялось | → `Locked`, при выполненных условиях старта — рестарт с нуля |
| Сейв/лоад в `Blocked` | Условия перечитываются: истинны → остаётся `Blocked`; сняты и `BlockRemovable` → `Locked` |
| Прерывание в `Reward`/`Completed`/`Failed` | Не срабатывает — терминальные состояния непрерываемы |

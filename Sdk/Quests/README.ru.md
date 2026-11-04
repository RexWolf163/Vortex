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
- `Vortex.Core.DatabaseSystem` — `Record`, `Database`
- `Vortex.Core.System.Abstractions` — `IDataStorage`
- `Vortex.Core.Extensions.ReactiveValues` — `IReactiveData` (для `SetListener`)
- `Vortex.Core.Extensions.LogicExtensions` — сериализация
- `Vortex.Core.UIProviderSystem` — `UIProvider`, `UserInterfaceData` (логики `CallUIOpen`, `WaitUIClose`)

### Unity
- `Vortex.Unity.DatabaseSystem` — `RecordPreset`, атрибут `DbRecord`
- `Vortex.Unity.AppSystem` — `TimeController` (перепроверка условий отложена на конец кадра)
- `Vortex.Unity.EditorTools` — атрибуты инспектора (`ClassLabel`, `ClassFilter`, `DataModel`)

### SDK
- `Vortex.Sdk.Core.GameCore` — `GameController`, `GameModel.IGameData`, `OnNewGame`, `OnLoadGame`, `OnGameStateChanged`

### Внешние
- **UniTask** — асинхронное выполнение логик
- **Odin Inspector** — атрибуты инспектора

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

`QuestConditions.Check()` проверяет условия слева направо: перед проверкой каждого снимает его подписки (`DisposeListeners`), на первом `false` подписывает только его (`InitListeners`) и останавливается — подписки живут только пока условие не выполнено.

#### Почему оба уровня — AND (атомарное детерминированное отслеживание)

И условия внутри группы (`QuestConditions.Check` → все по AND), и группы между собой (`CheckQuestStart` → `StartConditions.All`) объединяются по **AND с ленивым short-circuit**. Это осознанный design, а не избыточность: в каждый момент квест подписан ровно на **одно** невыполненное условие — «блокер» (первое `false` в первой невыполненной группе), а остальные проверяются только когда оно откроется (перепроверка каскадом продвигает подписку к следующему блокеру). Отсюда:

- **атомарность** — одна активная подписка за раз;
- **детерминированность** — блокер всегда однозначен (слева направо).

> ⚠️ **Не менять внутренний уровень на OR.** OR-группа истинна при выполнении *любого* из условий, значит становится истинной при открытии *любого* из них — пришлось бы подписываться на **все** условия невыполненной группы (однозначного блокера нет). Это ломает атомарное отслеживание. Организационная группировка (имя группы) — **не** логический OR: дерево условий вычисляется как **плоское AND**.

Чтобы выразить OR / NOR / XOR / вложенный AND **внутри** группы, не ломая эту атомарность, оберните условия в **`LogicGateCondition`** — одно `QuestConditionLogic`, сворачивающее свои вложенные по выбранному режиму (`OR` = любое, `NOT` = ни одного/NOR, `XOR` = ровно одно, `AND` = все). Оно осознанно подписывается на **все** вложенные (альтернативность — результат может измениться от любого), поэтому атомарная AND-модель самой группы не страдает: комбинатор живёт *внутри одного* условия, «плоский AND» группы сохраняется. Вложения произвольны (`LogicGateCondition` может содержать другой) — так собираются сложные деревья, например `OR` над двумя наборами, каждый свёрнут по `AND`: `LogicGateCondition{ Or, [ LogicGateCondition{ And, [...] }, LogicGateCondition{ And, [...] } ] }`. Режим `NOT` над одним вложенным — замена убранного флага `inverted` (например, «квест **не** завершён» = `LogicGateCondition{ Not, [ QuestCompleted(id) ] }`). *(Класс переименован из `OrNotCondition`; существующие `[SerializeReference]`-ссылки сохраняются через `[MovedFrom]`.)*

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

## Архитектура

```
QuestController (static, partial)
├── QuestModels : IGameData                       ← регистрируется в GameModel
│   └── Dictionary<string, QuestModel> Index      ← multi-instance копии из Database
│       ├── QuestName: string                     ← имя из пресета, только для чтения человеком
│       ├── State: QuestState (Unset→Locked→Ready→InProgress→…→Blocked)
│       ├── StartConditions[]                     ← AND-группы (и между группами AND)
│       ├── InterruptConditions[]                 ← OR-группы, приоритетнее старта → Blocked
│       ├── Logics[]                              ← последовательная очередь
│       ├── Step: byte                             ← ключ SavePoint для восстановления
│       ├── Autorun                               ← автозапуск при Ready
│       ├── UnFailable                            ← возврат в Locked при провале
│       └── BlockRemovable                        ← обратимость Blocked (иначе — навсегда)
├── ActiveCts                                     ← Dictionary<QuestModel, CTS>: реестр квестов с запущенной логикой + пер-квестовая отмена
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

Квест без наград тоже проходит через `Reward`: после последней логики он на один кадр уходит в `Reward`, затем сам переходит в `Completed`. Квест с наградами стоит в `Reward`, пока игра не вызовет `GiveRewards()` (список таких квестов — `GetRewardsReady()`); `IsComplete` для него уже `true`.

### Восстановление при загрузке

При `LoadGame()` квесты в состоянии `InProgress` восстанавливаются через `RestoreQuest`:

```
Run(quest) ──[State == InProgress]──→ RestoreQuest()
                                        ├── Step != 0 → пропуск логик до SavePoint с Key == Step
                                        └── Step == 0 → выполнение с начала
```

`SavePoint` — маркерная логика, которая при выполнении сохраняет свой `Key` в `QuestModel.Step`. При восстановлении все логики до соответствующего `SavePoint` (включительно) пропускаются. Если `SavePoint` с таким ключом в квесте нет (пресет изменили после сейва), пропускаются все логики и квест сразу завершается.

### Читаемое имя (`QuestName`)

`Record.Name` помечен `[NotPOCO]`: он приходит из пресета, но в сейв не пишется и в окне данных не показывается. Поэтому в модели есть отдельное поле `QuestName` — копия имени пресета с тем же GUID, только для чтения человеком. Логика квестов на него не опирается.

| Момент | Что происходит |
|--------|----------------|
| Сборка индекса (`QuestModels`) | Имя проставляется сразу: записи создаются из пресетов, `CopyFrom` переносит `Name`, индекс копирует его в `QuestName` |
| Новая игра, загрузка | `QuestController.FillQuestNames` проставляет имя заново — нужно для сейвов, записанных до появления поля: там в `QuestName` пусто, и загрузка затирает им значение |

Видно в сейве и в окне `Tools/Vortex/SaveData/Game Index`: без этого поля квест отображался бы одним GUID.

### Прерывание квеста (`Blocked`)

Второй набор условий — `InterruptConditions` — отвечает на вопрос «когда запретить» (условия старта — «когда открыть»). Свёртка **зеркальна старту**: **OR между группами** (сработала любая группа — квест блокируется), AND внутри группы. Пустой набор ⇒ квест непрерываем (обратная совместимость: существующие квесты не меняют поведение).

Проверка прерывания идёт **первой строкой** `CheckQuestStartConditions`, до проверки старта, — поэтому запрет всегда перебивает открытие (исключение — запуск при `LoadGame`, см. «Гарантии»). Квест уходит в `Blocked` из любого «живого» состояния (`Locked`/`Ready`/`InProgress`). Если он был `InProgress` — его логика немедленно отменяется (пер-квестовый `CancellationToken`), прогресс сбрасывается (`Step = 0`).

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

### Монитор квестов (диагностика)

Окно **Tools → Vortex → Debug → Quest Monitor**, только Play Mode. Показывает список квестов, текущую логику, результаты последней проверки условий старта и прерывания (с блокером), журнал переходов с причиной; даёт дебаг-действия.

Раскладка окна:
- **Панель инструментов** — поиск по имени, фильтр по состояниям, счётчики состояний, заполненность журнала (красным при вытеснении), экспорт журнала.
- **Список квестов** (слева) — состояние, имя, текущая логика N/M, флаги `A`utorun/`U`nFailable/`B`lockRemovable, `Step`; квесты с исключением в журнале подсвечены.
- **Вкладка «Квест»** — карточка выбранного квеста: шапка (имя, GUID, состояние, флаги), логики со статусами, деревья условий старта и прерывания с результатами и блокером, награды, журнал квеста. Справа — фиксированная колонка: дебаг-действия (недоступные для текущего состояния неактивны), под разделителем — «Пресет» (подсветка ассета пресета в Project).
- **Вкладка «Журнал»** — общий журнал с фильтрами «только выбранный квест» / «только ошибки»; клик по записи выбирает квест.

- **Только редактор.** Точки записи (`QuestDiagnostics`) — `[Conditional("UNITY_EDITOR")]`: в билде вызовы вырезаются вместе с аргументами. В билде остаются только перечисления `TransitionReason`/`ConditionSetKind` (аргументы `SetState`) и классы настроек `QuestDebugSettings`/`ConditionLabel` (данные без логики).
- **Наблюдение без вмешательства (INV-D1).** Монитор не вызывает `Check()`: результаты условий пишутся в момент штатной проверки внутри контекста, который открывает контроллер. Условия, до которых проверка не дошла (short-circuit), показываются «не проверялось». Проверки вне контроллера (UI-переключатели по условиям и т.п.) в снимки не попадают.
- **Единая точка смены состояния (INV-D2).** `QuestModel.State` меняется только через `SetState(state, reason)`; каждая фактическая смена — одна запись журнала.
- **Журнал** — кольцевой буфер (ёмкость в `QuestDebugSettings`, по умолчанию 500), только в памяти, экспорт в текст кнопкой. Защиты от спама нет намеренно: квест, забивающий журнал, — видимый симптом бага; вытеснение видно по счётчику и пропуску `seq`.
- **Дебаг-действия** выполняет контроллер (`QuestControllerExtDebug`) по штатным переходам — инвариант `Reward`/`Completed`/`Failed` ⇔ квест в `CompletedQuests` сохраняется. «Завершить успехом» — Reward при наградах, иначе Completed; «Завершить провалом» — как провал логики (`UnFailable` → Locked); «Сбросить» — из любого состояния в Locked с `Step = 0` (уже открывшиеся от квеста квесты не откатываются).
- **Подписи условий** — `QuestDebugSettings.conditionLabels`: тип → шаблон, `{поле}` заменяется значением сериализованного поля; неизвестное поле — `{поле?}`; нет записи — имя класса.

> **Контракт составных условий.** Условие, которое само вызывает `Check()` вложенных (как `LogicGateCondition`), обязано оборачивать каждый вызов в `QuestDiagnostics.EnterNode(i)` / `ExitNode(result)` — иначе его вложенные в мониторе всегда «не проверялось».

> **Ограничение.** Принудительное действие над квестом, логика которого ждёт UI (`WaitUIClose`, `CallUIOpen`), отменяет логику, но уже открытое окно игры не закрывает.

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `QuestController` | static, partial | Контроллер жизненного цикла |
| `QuestControllerExtIndex` | partial | Запросы: `IsComplete(id)` |
| `QuestControllerExtEditor` | partial, `#if UNITY_EDITOR` | Editor-интеграция |
| `QuestControllerExtDebug` | partial, `#if UNITY_EDITOR` | Дебаг-действия монитора квестов |
| `QuestDiagnostics` | static, internal | Точки записи монитора: журнал, след квеста, контекст проверки условий |
| `QuestMonitorWindow` | `EditorWindow`, `#if UNITY_EDITOR` | Окно монитора квестов |
| `QuestDebugSettings` | `ScriptableObject` | Настройки монитора: ёмкость журнала, подписи условий |
| `QuestModel` | `Record` | Модель квеста: состояние, условия, логики, читаемое имя (`QuestName`) |
| `QuestModels` | `IGameData` | Контейнер индекса квестов |
| `QuestPreset` | `RecordPreset<QuestModel>` | ScriptableObject-пресет для Database |
| `QuestState` | `enum` | Unset, Locked, Ready, InProgress, Reward, Completed, Failed, Blocked |
| `QuestLogic` | `abstract` | Атомарная логика: `UniTask<bool> Run(CancellationToken)` |
| `SavePoint` | `QuestLogic` | Маркер точки сохранения: сохраняет `Key` в `QuestModel.Step` |
| `AlwaysFail` | `QuestLogic` | Жёсткий `false`: у `UnFailable`-квеста зацикливает его (Locked → рестарт) |
| `CallUIOpen` | `QuestLogic` | Открывает интерфейс по ID и сразу пропускает квест дальше |
| `WaitUIClose` | `QuestLogic` | Ждёт закрытия интерфейсов: перечисленных (blacklist) или всех Common, кроме перечисленных (whitelist) |
| `QuestRewardLogic` | `abstract` | Награда: `RewardLogic()`, `GetRewardData()`; выдаётся контроллером в `GiveRewards()` |
| `QuestConditionLogic` | `abstract` | Условие: `Check()`, `InitListeners()`, `DisposeListeners()` |
| `QuestConditions` | `Serializable` | Группа условий: `Check()` по AND. Свёртка между группами — AND (старт) или OR (прерывание) — на уровне контроллера |
| `LogicGateCondition` | `QuestConditionLogic` | Комбинатор над вложенными условиями с режимом (`OR` / `NOT`=NOR / `XOR`=ровно одно / `AND`=все). Подписывается на **все** дочерние (альтернативность). Выражает произвольную логику внутри AND-группы; деревья вкладываются свободно (напр. `OR` над двумя `AND`-наборами); `NOT` над одним вложенным заменяет убранный флаг `inverted` у условий. *(бывш. `OrNotCondition`; сериализованные `[SerializeReference]`-ссылки мигрируют через `[MovedFrom]`)* |
| `QuestCompleted` | `QuestConditionLogic` | Условие: квест с заданным ID завершён с любым результатом (`Reward`/`Completed`/`Failed`) |
| `QuestDataStorage` | `MonoBehaviour`, `IDataStorage` | Привязка UI к квесту по GUID |
| `RunQuestHandler` | `MonoBehaviour` | Запуск квеста через `IDataStorage` |

## Контракт

### Вход
- `QuestPreset` — ScriptableObject, зарегистрированный в Database как MultiInstance
- `GameController.OnNewGame` — триггер новой игры
- `GameController.OnLoadGame` — триггер загрузки сохранения
- `GameController.OnGameStateChanged` — сброс квестов при `Off`/`Loading`
- `GameController.OnEditorGetData` (только редактор) — инициализация квестов при сборе данных вне Play Mode

### Выход
- `QuestController.OnUpdateData` — событие изменений
- `QuestController.OnCheckConditions` — завершилась перепроверка условий (для `QuestConditions`, используемых вне квестов)
- `QuestController.IsComplete(id)` — проверка завершения (любой результат)
- `QuestController.CheckQuestStartConditions()` — запрос перепроверки условий (выполняется в конце кадра)
- `QuestModel.Run()` — запуск квеста в `Ready` / восстановление в `InProgress`
- `QuestController.GetRewardsReady()`, `QuestModel.GiveRewards()` — квесты в `Reward` и выдача их наград
- `QuestModel.OnStateUpdated` — событие смены состояния конкретного квеста

### Гарантии
- Логики выполняются строго последовательно
- При `NewGame()` и `LoadGame()` все активные квесты отменяются через `CancellationToken`
- При переходе игры в `GameStates.Off` или `Loading` контроллер сбрасывается: активные квесты отменяются, все квесты получают `Reset()` (→ `Unset`, `Step = 0`, подписки сняты). `CheckQuestStartConditions` в этих состояниях ничего не делает
- Рекурсивная перепроверка условий ограничена глубиной 10
- `UnFailable`-квест при провале возвращается в `Locked` и не попадает в `CompletedQuests` — может быть перезапущен
- `Run()` на квест в состоянии `Ready` — запускает `RunQuest`; в состоянии `InProgress` — запускает `RestoreQuest`; в ином состоянии или если логика квеста уже выполняется — логируется ошибка, вызов игнорируется
- При запуске квеста подписки условий старта снимаются (`DisposeListeners`); подписки прерывания живут через `Locked → Ready → InProgress`
- Прерывание приоритетнее старта: interrupt-пасс — первая строка `CheckQuestStartConditions`, до старт-пасса и на каждой итерации settle-рекурсии. **Исключение — `LoadGame`:** квесты `InProgress` и `Ready` + `Autorun` запускаются сразу (`Run()`), а `CheckQuestStartConditions` отложен на конец кадра — синхронная часть их логик (до первого незавершённого `await`) отрабатывает до проверки прерывания; при истинном прерывании квест блокируется в конце кадра, побочные эффекты уже выполненных логик не откатываются
- Прерванный `InProgress`-квест теряет прогресс (`Step = 0`); при обратимом блоке возврат — строго с нуля через `RunQuest`

### Ограничения
- Квесты — строго MultiInstance записи (каждая игра получает свежие копии)
- Пер-квестовые linked-CTS: отмена одного квеста (при блокировке) не трогает остальные; глобальный `CancellationTokenSource` — для группового teardown (новая игра/загрузка)
- `QuestConditionLogic.Check()` — синхронный, не поддерживает async-условия

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Новый квест на существующем сейве | Состояние `Unset`, при `LoadGame` переводится в `Locked` и участвует в проверке условий |
| Все условия пусты | Квест сразу получает `Ready` |
| `Autorun` + условия выполнены | Квест запускается автоматически при `NewGame`, `LoadGame` или вызове `CheckQuestStartConditions()` |
| Логика возвращает `false`, `UnFailable = true` | Состояние → `Locked`, в `CompletedQuests` не добавляется (перезапуск возможен) |
| Логика возвращает `false`, `UnFailable = false` | Состояние → `Failed`, квест в `CompletedQuests` |
| `NewGame()` / `LoadGame()` при активных квестах | Все отменяются через `CancellationToken`, подписки снимаются |
| Переход в `GameStates.Off` / `Loading` | Активные квесты отменяются, на всех квестах `Reset()` → `Unset`; `CheckQuestStartConditions` в этих состояниях пропускается |
| Рекурсия условий > 10 уровней | Прерывается (предохранитель) |
| `Run()` на квест в `InProgress` | Восстановление через `RestoreQuest` — пропуск логик до `SavePoint` |
| `Run()` на квест, чья логика уже выполняется | Ошибка в лог, повторный запуск отклонён |
| `SavePoint` с сохранённым `Step` не найден | Пропускаются все логики, квест завершается (`Reward`/`Completed`) |
| Логика бросила исключение | Исключение логируется, квест принудительно → `Completed` и попадает в `CompletedQuests` |
| Награда бросила исключение | Исключение логируется, квест всё равно → `Completed` |
| Квест удалён из Database, но есть в сейве | При `LoadGame` запись убирается из индекса |
| Квест завершён → условия другого квеста зависят от него | Рекурсивная перепроверка через `CheckQuestStartConditions` |
| Пустой `InterruptConditions` | Квест непрерываем — поведение как до фичи |
| Условие прерывания сработало на `InProgress` | Логика отменяется (пер-квестовый CTS), `Step = 0`, состояние → `Blocked` |
| Прерывание vs старт при одновременной истинности | Побеждает прерывание (interrupt-пасс раньше старт-пасса) |
| `LoadGame`: квест `InProgress` или `Ready` + `Autorun` при истинном прерывании | Квест сначала запускается (синхронная часть логик отрабатывает), в конце кадра блокируется с отменой логики; побочные эффекты не откатываются |
| `BlockRemovable = false`, условие снялось | Остаётся `Blocked` (до `Reset`/новой игры) |
| `BlockRemovable = true`, условие снялось | → `Locked`, при выполненных условиях старта — рестарт с нуля |
| Сейв/лоад в `Blocked` | Условия перечитываются: истинны → остаётся `Blocked`; сняты и `BlockRemovable` → `Locked` |
| Прерывание в `Reward`/`Completed`/`Failed` | Не срабатывает — терминальные состояния непрерываемы |

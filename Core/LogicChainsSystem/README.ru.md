# LogicChainsSystem (Core)

**Namespace:** `Vortex.Core.LogicChainsSystem.Bus`, `Vortex.Core.LogicChainsSystem.Model`
**Сборка:** `ru.vortex.logicchains`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Система логических цепочек. Позволяет описывать последовательность этапов с действиями, переходами и условиями. Цепочка выполняется пошагово: при входе в этап срабатывают действия, затем активируются коннекторы с условиями перехода к следующему этапу.

Возможности:

- Граф этапов с направленными переходами (коннекторами)
- Действия (`LogicAction`) при входе в этап
- Условия (`Condition`) на переходах — асинхронные, с callback-механизмом
- Автоматические переходы (коннектор без условий)
- Сохранение/загрузка текущего этапа через `Record`
- Регистрация цепочек по GUID, multi-instance через `Database`

Вне ответственности:

- Конкретные действия и условия — реализуются наследниками в Unity-слое
- Визуальное редактирование графа — пресеты в Unity-слое
- UI отображение прогресса

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `SystemModel` |
| `Vortex.Core.DatabaseSystem` | `Record`, `Database.GetNewRecord()` |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid()`, `DictionaryExt` |
| `Vortex.Core.LoggerSystem` | `Log.Print()` для диагностики |
| `Vortex.Core.SettingsSystem` | `Settings.Data().DebugMode` для отладочных логов |

---

## Архитектура

```
LogicChains (static bus)
  ├── Index: Dictionary<string, LogicChain>
  ├── AddChain(LogicChain) → guid
  ├── AddChain(presetGuid) → guid         ← Database.GetNewRecord<LogicChain>
  ├── RunChain(guid)                       ← запуск / продолжение цепочки
  └── CheckConditions(guid, connector)     ← внутренняя проверка перехода

LogicChain : Record
  ├── ChainSteps: Dictionary<string, ChainStep>
  ├── StartStep: string (GUID)
  ├── CurrentStep: string (GUID)
  ├── GetDataForSave() → CurrentStep
  └── LoadFromSaveData(data) → CurrentStep = data

ChainStep : SystemModel
  ├── Guid, Name, Description
  ├── Actions: LogicAction[]              ← выполняются при входе в этап
  └── Connectors: Connector[]             ← переходы к другим этапам

Connector : SystemModel
  ├── TargetStepGuid: string
  └── Conditions: Condition[]             ← все должны быть true для перехода

LogicAction (abstract)
  └── Invoke()                            ← действие при входе в этап

Condition (abstract)
  ├── Init(Action callback)               ← запуск мониторинга
  ├── Check() → bool                      ← проверка выполнения
  ├── DeInit()                            ← остановка мониторинга
  ├── Start()                             ← внутренняя инициализация
  └── RunCallback()                       ← уведомление о возможном выполнении
```

### Жизненный цикл цепочки

```
AddChain → RunChain → [Enter Step]
                         ├── Invoke Actions[]
                         └── For each Connector:
                              ├── Conditions.Length == 0 → автопереход
                              └── Conditions[].Init(callback)
                                   └── callback → CheckConditions
                                        ├── All Check() == true → переход
                                        │    ├── DeInit all Conditions
                                        │    ├── CurrentStep = TargetStepGuid
                                        │    ├── target == "-1" → завершение
                                        │    └── RunChain (следующий этап)
                                        └── Any Check() == false → ожидание
```

### Condition — асинхронная модель

Условие не опрашивается по таймеру. `Init(callback)` запускает наблюдение (подписка на событие, таймер и т.д.). При изменении состояния условие вызывает `RunCallback()`, что запускает `CheckConditions` — проверку всех условий коннектора. Если все `Check() == true`, переход выполняется.

### Завершение цепочки

Коннектор с `TargetStepGuid == "-1"` (`CompleteChainStep`) завершает цепочку: она удаляется из `Index`. При `DebugMode` выводится лог.

---

## Контракт

### Вход

- `LogicChain` добавляется через `AddChain()` — напрямую или по GUID пресета из `Database`
- `RunChain(guid)` запускает выполнение

### Выход

- Действия (`LogicAction.Invoke()`) вызываются при входе в этап
- Переходы выполняются автоматически при выполнении условий
- Цепочка завершается при переходе к `CompleteChainStep`

### API

| Метод | Описание |
|-------|----------|
| `LogicChains.AddChain(LogicChain)` | Регистрация цепочки, возвращает GUID |
| `LogicChains.AddChain(string presetGuid)` | Создание цепочки из пресета Database, возвращает GUID |
| `LogicChains.RunChain(string guid)` | Запуск или продолжение цепочки |

### Константы

| Константа | Значение | Описание |
|-----------|----------|----------|
| `CompleteChainStep` | `"-1"` | GUID-маркер завершения цепочки |

### Сохранение

`LogicChain : Record` — сохраняет и восстанавливает `CurrentStep`. При загрузке `RunChain` продолжит с сохранённого этапа.

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Один активный этап на цепочку | `CurrentStep` — единственный указатель |
| Условия проверяются AND-логикой | Все `Condition.Check()` должны быть `true` |
| Первый подходящий коннектор побеждает | При автопереходе (0 условий) остальные коннекторы не проверяются |
| Нет отмены цепочки | Удаление из `Index` только при завершении |
| Цикличные цепочки допустимы | Нет защиты от бесконечных циклов |

---

## Использование

### Создание действия

```csharp
public class ShowNotification : LogicAction
{
    public string Message;

    public override void Invoke()
    {
        NotificationSystem.Show(Message);
    }
}
```

### Создание условия

```csharp
public class PlayerLevelReached : Condition
{
    public int TargetLevel;

    protected override void Start()
    {
        PlayerModel.OnLevelChanged += OnLevelChanged;
        if (Check()) RunCallback();
    }

    private void OnLevelChanged(int level)
    {
        if (Check()) RunCallback();
    }

    public override bool Check() => PlayerModel.Level >= TargetLevel;
    public override void DeInit() => PlayerModel.OnLevelChanged -= OnLevelChanged;
}
```

### Запуск цепочки

```csharp
// Из пресета Database
var guid = LogicChains.AddChain("preset-guid-from-database");
LogicChains.RunChain(guid);

// Из объекта
var chain = new LogicChain();
// ... настройка ChainSteps, StartStep ...
var guid = LogicChains.AddChain(chain);
LogicChains.RunChain(guid);
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| GUID цепочки не найден в `Index` | `Log.Print(Error)`, выполнение прерывается |
| `StartStep` не найден в `ChainSteps` | `Log.Print(Error)`, выполнение прерывается |
| Коннектор без условий | Автоматический переход к `TargetStepGuid` |
| Все условия коннектора выполнены | Переход, `DeInit` всех условий текущего этапа |
| `TargetStepGuid == "-1"` | Цепочка завершена, удалена из `Index` |
| Исключение в `CheckConditions` | `Log.Print(Error)`, `condition.DeInit()` |
| `CurrentStep` уже установлен при `RunChain` | Продолжение с текущего этапа (не с `StartStep`) |
| `DebugMode` активен | Дополнительный лог при завершении цепочки |

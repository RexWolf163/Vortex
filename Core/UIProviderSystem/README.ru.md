# UIProviderSystem (Core)

**Namespace:** `Vortex.Core.UIProviderSystem.Bus`, `Vortex.Core.UIProviderSystem.Model`, `Vortex.Core.UIProviderSystem.Enums`
**Сборка:** `ru.vortex.uiprovider`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Шина управления жизненным циклом пользовательских интерфейсов. Регистрирует интерфейсы по GUID, управляет их состоянием (открыто/закрыто) и обеспечивает автоматическое открытие/закрытие через декларативные условия.

Концептуально интерфейс рассматривается как пустой контейнер без собственной логики. Наследование `UserInterface` не предусмотрено. Поведение интерфейса целиком формируется из автономных компонентов: условия (`UserInterfaceCondition`) определяют когда открывать/закрывать, тип (`UserInterfaceTypes`) — участие в массовых операциях, drag-offset — позиционирование. Логика, специфичная для конкретного интерфейса, реализуется внешними компонентами, а не внутри контейнера.

Возможности:

- `UIProvider` — статическая шина: `Open()`, `Close()`, `CloseAll()`, `Register()`, `Unregister()`
- `UserInterfaceData` — модель данных интерфейса (`Record`) с условиями, событиями и drag-offset
- `UserInterfaceCondition` — абстрактное условие с callback-мониторингом
- `ConditionAnswer` — результат проверки: `Idle`, `Open`, `Close`
- `UserInterfaceTypes` — типизация: `Common`, `Panel`, `Overlay`, `Popup`
- Событийная модель: `OnOpen`/`OnClose` на шине и на каждом `UserInterfaceData`
- Сохранение позиции окна через `Record.GetDataForSave()`/`LoadFromSaveData()`

Вне ответственности:

- ScriptableObject-пресеты, MonoBehaviour-представления — Unity-слой
- Анимации открытия/закрытия (TweenerHub) — Unity-слой
- Перетаскивание окон (drag handler) — Unity-слой
- Кодогенерация условий — Unity-слой

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.DatabaseSystem` | `Record`, `Database.GetRecord()` |
| `Vortex.Core.AppSystem` | `App.GetState()` (проверка при регистрации) |
| `Vortex.Core.SettingsSystem` | `Settings.Data().AppStateDebugMode` (debug-лог) |
| `Vortex.Core.LoggerSystem` | `Log.Print()` |
| `Vortex.Core.Extensions` | `DictionaryExt.AddNew()`, `DictionaryExt.Get()` |

---

## Архитектура

```
UIProvider (static partial class)
  ├── Uis: Dictionary<string, UserInterfaceData>
  │
  ├── Register(id) → UserInterfaceData       ← Database.GetRecord, debug-лог
  ├── Unregister(id)
  ├── Open(id)                                ← data.Open()
  ├── Close(id)                               ← data.Close()
  ├── CloseAll()                              ← только Common
  ├── HasOpenedUIs() → bool                   ← только Common
  ├── GetOpenedUIs() → UserInterfaceData[]    ← только Common
  ├── OnOpen: Action (event)
  └── OnClose: Action (event)

UserInterfaceData : Record
  ├── IsOpen: bool
  ├── UIType: UserInterfaceTypes
  ├── Conditions: UserInterfaceCondition[]
  ├── Offset: (int x, int y)                 ← позиция окна
  │
  ├── OnOpen: Action                          ← safe subscribe: если IsOpen — вызов сразу
  ├── OnClose: Action                         ← safe subscribe: если !IsOpen — вызов сразу
  │
  ├── Init() → foreach Condition.Init(this, CheckConditions)
  ├── DeInit() → foreach Condition.DeInit()
  ├── Open() → IsOpen=true, OnOpen, UIProvider.CallOnOpen
  ├── Close() → IsOpen=false, OnClose, UIProvider.CallOnClose
  ├── CheckConditions()                       ← вызывается conditions callback'ом
  │
  ├── GetDataForSave() → "x;y"
  └── LoadFromSaveData(data) → Offset

UserInterfaceCondition (abstract, Serializable)
  ├── Data: UserInterfaceData (protected)
  ├── Init(data, callback) → Run()
  ├── RunCallback() → callback
  ├── Run() (abstract)                        ← подписка на события
  ├── DeInit() (abstract)                     ← отписка
  └── Check() → ConditionAnswer (abstract)

ConditionAnswer
  ├── Idle                                    ← не влияет
  ├── Open                                    ← требует открыть
  └── Close                                   ← требует закрыть

UserInterfaceTypes
  ├── Common                                  ← базовые окна (участвуют в CloseAll, GetOpenedUIs)
  ├── Panel
  ├── Overlay
  └── Popup
```

### Механизм условий

1. `Init()` вызывает `Condition.Init(data, CheckConditions)` для каждого условия
2. Условие в `Run()` подписывается на внешние события и вызывает `RunCallback()` при изменении
3. `RunCallback()` запускает `CheckConditions()` — проверку всех условий
4. `CheckConditions()`: начальное состояние = текущее (`IsOpen`). Для каждого условия:
   - `Idle` — пропуск
   - `Open` — `state = Open`
   - `Close` (или исключение) → немедленный `Close()`, return
5. Итоговый `state`: `Open` → `Open()`, иначе → `Close()`
6. Приоритет: **Close** побеждает — любое условие, вернувшее `Close`, немедленно закрывает UI

### Safe Subscribe на OnOpen/OnClose

`UserInterfaceData.OnOpen` — custom event accessor. При подписке, если `IsOpen == true`, callback вызывается немедленно. Аналогично `OnClose` — при подписке, если `!IsOpen`, callback вызывается сразу. Паттерн аналогичен `SystemController.OnInit`.

### Типизация

`CloseAll()`, `HasOpenedUIs()`, `GetOpenedUIs()` фильтруют только `Common`. Типы `Panel`, `Overlay`, `Popup` не затрагиваются массовыми операциями.

---

## Контракт

### Вход

- `Register(id)` — получает `UserInterfaceData` из `Database`, добавляет в индекс
- Условия (`UserInterfaceCondition[]`) устанавливаются через пресет в Unity-слое
- `Open(id)` / `Close(id)` — ручное управление

### Выход

- `IsOpen` на `UserInterfaceData` — текущее состояние
- События: `OnOpen` / `OnClose` на данных и на шине
- `GetOpenedUIs()` — массив открытых Common-интерфейсов
- `GetDataForSave()` — строка `"x;y"` для сохранения позиции

### API

| Метод | Описание |
|-------|----------|
| `UIProvider.Register(id)` | Регистрация по GUID пресета из Database |
| `UIProvider.Unregister(id)` | Снятие с регистрации |
| `UIProvider.Open(id)` | Открыть интерфейс |
| `UIProvider.Close(id)` | Закрыть интерфейс |
| `UIProvider.CloseAll()` | Закрыть все Common |
| `UIProvider.HasOpenedUIs()` | Есть ли открытые Common |
| `UIProvider.GetOpenedUIs()` | Массив открытых Common |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `Register` требует `App.GetState() >= Starting` | Зависимость от инициализации App |
| `CloseAll` / `GetOpenedUIs` — только Common | Панели, оверлеи, попапы не затрагиваются |
| `Close` приоритетнее `Open` в условиях | Первый `Close` прерывает проверку |
| Позиция — `(int, int)` | Целочисленные пиксели |
| GUID пресета должен быть уникален | Дубликаты перезаписывают запись в индексе |

---

## Использование

### Создание условия

```csharp
[Serializable]
public class PlayerDeadCondition : UserInterfaceCondition
{
    protected override void Run()
    {
        PlayerModel.OnDeath += RunCallback;
        PlayerModel.OnRespawn += RunCallback;
        RunCallback();
    }

    public override void DeInit()
    {
        PlayerModel.OnDeath -= RunCallback;
        PlayerModel.OnRespawn -= RunCallback;
    }

    public override ConditionAnswer Check()
    {
        return PlayerModel.IsDead ? ConditionAnswer.Open : ConditionAnswer.Close;
    }
}
```

### Ручное управление

```csharp
UIProvider.Open("settings-menu-guid");
UIProvider.Close("settings-menu-guid");
UIProvider.CloseAll();

var openUIs = UIProvider.GetOpenedUIs();
```

### Подписка на события

```csharp
// Safe subscribe — если UI уже открыт, callback вызовется сразу
uiData.OnOpen += () => Debug.Log("Opened");
uiData.OnClose += () => Debug.Log("Closed");
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `Open` на уже открытом UI | No-op (проверка `IsOpen`) |
| `Close` на уже закрытом UI | No-op |
| GUID не найден в индексе | `Log.Print(Error)`, no-op |
| `Register` при `App.GetState() < Starting` | Возвращает `null` |
| Условие выбрасывает исключение в `Check()` | `Debug.LogException`, немедленный `Close()` |
| Все условия `Idle` | Состояние не изменяется (остаётся текущим) |
| Подписка на `OnOpen` при `IsOpen == true` | Callback вызывается немедленно |
| Подписка на `OnClose` при `IsOpen == false` | Callback вызывается немедленно |
| `AppStateDebugMode` активен | Лог при Register/Unregister |

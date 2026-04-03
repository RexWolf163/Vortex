# SaveSystem (Core)

**Namespace:** `Vortex.Core.SaveSystem.Bus`, `Vortex.Core.SaveSystem.Abstraction`, `Vortex.Core.SaveSystem.Model`
**Сборка:** `ru.vortex.save`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Система сохранения и загрузки данных. Предоставляет шину `SaveController` для управления процессами save/load, реестр `ISaveable`-модулей и асинхронный сбор/раздачу данных. Данные хранятся как `Dictionary<string, Dictionary<string, string>>` — иерархия модуль → ключ → значение (строки).

Возможности:

- `SaveController` — шина: `Save()`, `Load()`, `Remove()`, `GetIndex()`
- `ISaveable` — интерфейс для модулей, данные которых подлежат сохранению
- Асинхронный сбор данных при `Save`, асинхронная раздача при `Load` (UniTask)
- `SaveProcessData` — двухуровневый прогресс (глобальный + модульный)
- События: `OnSaveStart`, `OnSaveComplete`, `OnLoadStart`, `OnLoadComplete`, `OnRemove`
- `SaveSummary` — метаданные сохранения (имя, дата, XML-сериализуемый)
- Автогенерация GUID для новых сохранений

Вне ответственности:

- Физическое хранение (PlayerPrefs, файлы) — Unity-слой
- Сжатие/шифрование данных — Unity-слой (драйвер)
- UI-отображение прогресса — Unity-слой

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `ProcessData` |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid()`, `DictionaryExt.AddNew()` |
| `Vortex.Core.LoggerSystem` | `Log.Print()` при ошибках |
| UniTask | `UniTask`, `CancellationToken` (в `ISaveable`) |

---

## Архитектура

```
SaveController : SystemController<SaveController, IDriver>
  ├── SaveDataIndex: Dictionary<string, Dictionary<string, string>>
  │    └── модуль (SaveId) → { ключ → значение }
  ├── Saveables: HashSet<ISaveable>
  ├── State: SaveControllerStates
  ├── ProcessData: SaveProcessData
  │
  ├── Save(name, guid?) → async void
  │    ├── State = Saving, OnSaveStart
  │    ├── foreach ISaveable → GetSaveData() → SaveDataIndex
  │    ├── guid ??= Crypto.GetNewGuid()
  │    ├── Driver.Save(name, guid)
  │    └── State = Idle, OnSaveComplete
  │
  ├── Load(guid) → async void
  │    ├── State = Loading, OnLoadStart
  │    ├── Driver.Load(guid) → заполняет SaveDataIndex
  │    ├── foreach ISaveable → OnLoad()
  │    └── State = Idle, OnLoadComplete
  │
  ├── Remove(guid) → Driver.Remove(guid), OnRemove
  ├── GetData(id) → Dictionary<string, string>
  ├── GetIndex() → Driver.GetIndex()
  ├── Register(ISaveable) / UnRegister(ISaveable)
  └── GetProcessData() → SaveProcessData

ISaveable
  ├── GetSaveId() → string
  ├── GetSaveData(CancellationToken) → UniTask<Dictionary<string, string>>
  ├── GetProcessInfo() → ProcessData
  └── OnLoad(CancellationToken) → UniTask

IDriver : ISystemDriver
  ├── Save(name, guid)
  ├── Load(guid)
  ├── Remove(guid)
  ├── SetIndexLink(Dictionary<string, Dictionary<string, string>>)
  └── GetIndex() → Dictionary<string, SaveSummary>
```

### Формат данных

```
SaveDataIndex: Dictionary<string, Dictionary<string, string>>
  └── "ModuleA" → { "key1" → "json1", "key2" → "json2" }
  └── "ModuleB" → { "key1" → "json1" }
```

Каждый `ISaveable` возвращает свой `GetSaveId()` (идентификатор модуля) и `Dictionary<string, string>` (ключ → JSON-строка). `SaveController` агрегирует все модули в `SaveDataIndex`.

### Жизненный цикл Save

1. Проверка замка (`State == Saving` → return)
2. `State = Saving`, `OnSaveStart`
3. `SaveDataIndex.Clear()`
4. Для каждого `ISaveable` — `await GetSaveData(token)` → добавление в `SaveDataIndex`
5. Генерация GUID если не передан
6. `Driver.Save(name, guid)` — физическое сохранение
7. `State = Idle`, `OnSaveComplete`

### Жизненный цикл Load

1. `State = Loading`, `OnLoadStart`
2. `Driver.Load(guid)` — драйвер заполняет `SaveDataIndex`
3. Для каждого `ISaveable` — `await OnLoad(token)` (модуль читает из `SaveController.GetData()`)
4. `State = Idle`, `OnLoadComplete`

### SaveProcessData — двухуровневый прогресс

| Уровень | Поле | Описание |
|---------|------|----------|
| Глобальный | `Global.Progress` / `Global.Size` | Текущий модуль / всего модулей |
| Модульный | `Module.Progress` / `Module.Size` | Прогресс внутри текущего модуля |

### Структуры данных

| Тип | Назначение |
|-----|-----------|
| `SaveData` | struct: `Id`, `Data` — единица данных |
| `SaveFolder` | struct: `Id`, `SaveData[]` — папка модуля |
| `SaveSummary` | struct: `Name`, `Date`, `UnixTimestamp` — метаданные сохранения (XML-сериализуемый) |
| `SaveControllerStates` | enum: `Idle`, `Saving`, `Loading` |

---

## Контракт

### Вход

- `ISaveable`-модули регистрируются через `Register()`
- `Save(name, guid?)` / `Load(guid)` / `Remove(guid)` запускают процессы

### Выход

- `GetData(id)` — данные модуля после загрузки
- `GetIndex()` — все существующие сохранения (`Dictionary<string, SaveSummary>`)
- События: `OnSaveStart`, `OnSaveComplete`, `OnLoadStart`, `OnLoadComplete`, `OnRemove`

### API

| Метод | Описание |
|-------|----------|
| `SaveController.Save(name, guid?)` | Сохранение (async void) |
| `SaveController.Load(guid)` | Загрузка (async void) |
| `SaveController.Remove(guid)` | Удаление сохранения |
| `SaveController.GetData(id)` | Данные модуля по `SaveId` |
| `SaveController.GetIndex()` | Все сохранения |
| `SaveController.Register(ISaveable)` | Регистрация модуля |
| `SaveController.UnRegister(ISaveable)` | Отмена регистрации |
| `SaveController.GetNumberLastSave()` | Номер-инкремент последнего сохранения |
| `SaveController.GetProcessData()` | Данные прогресса |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `Save` — замок от повторного вызова | `State == Saving` → return |
| `Load` — без замка | Повторный вызов не блокируется |
| Данные — только строки | `Dictionary<string, string>`, JSON-сериализация на стороне модуля |
| `async void` | `Save`/`Load` — fire-and-forget, исключения ловятся внутри |
| `CancellationToken` объявлен, но не используется | Зарезервирован на будущее |

---

## Использование

### Реализация ISaveable

```csharp
public class InventoryController : ISaveable
{
    private ProcessData _processData = new("Inventory");

    public string GetSaveId() => "Inventory";

    public async UniTask<Dictionary<string, string>> GetSaveData(CancellationToken ct)
    {
        var data = new Dictionary<string, string>();
        data["items"] = JsonUtility.ToJson(items);
        data["gold"] = gold.ToString();
        return data;
    }

    public ProcessData GetProcessInfo() => _processData;

    public async UniTask OnLoad(CancellationToken ct)
    {
        var data = SaveController.GetData("Inventory");
        if (data.TryGetValue("items", out var json))
            items = JsonUtility.FromJson<ItemList>(json);
        if (data.TryGetValue("gold", out var g))
            gold = int.Parse(g);
    }
}
```

### Сохранение / загрузка

```csharp
// Регистрация
SaveController.Register(inventoryController);

// Сохранение (новый GUID)
SaveController.Save("Quick Save");

// Сохранение (перезапись)
SaveController.Save("Quick Save", existingGuid);

// Загрузка
SaveController.Load(guid);

// Список сохранений
var saves = SaveController.GetIndex();
foreach (var (guid, summary) in saves)
    Debug.Log($"{summary.Name} — {summary.Date}");

// Удаление
SaveController.Remove(guid);
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `Save` во время сохранения | Блокируется (`State == Saving` → return) |
| `Load` во время загрузки | Не блокируется (нет замка) |
| `GetData` с несуществующим `id` | `Log.Print(Error)`, возвращает пустой `Dictionary` |
| GUID не передан в `Save` | Генерируется `Crypto.GetNewGuid()` |
| Исключение в `GetSaveData` / `OnLoad` | `Log.Print(Error)`, `State = Idle`, событие Complete вызывается |
| `ISaveable` не зарегистрирован | Данные не собираются/не раздаются |
| `SaveSummary` — XML-сериализация | `Date` как `UnixTimestamp` (long), `DateTime.FromFileTimeUtc` |

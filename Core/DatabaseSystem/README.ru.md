# DatabaseSystem (Core)

Платформонезависимая шина данных приложения с доступом по GUID.

## Назначение

Централизованное хранилище записей: индексация по GUID, два режима хранения (Singleton / MultiInstance), интеграция с SaveSystem, событийная модель, интерфейс драйвера.

- Индексированное хранение записей (`Dictionary<GUID, Record>`)
- Singleton-записи: один экземпляр, персистентность через `SaveSystem`
- MultiInstance-записи: новая копия из пресета при каждом запросе
- O(1) доступ по GUID
- Проверка существования записи (`TestRecord`)
- Фильтрация по типу (`GetRecords<T>`, `GetMultiInstancePresets<T>`)
- Асинхронное сохранение/загрузка через `ISaveable`

Вне ответственности: загрузка пресетов с диска, кеширование ассетов, UI для выбора записей — это задача драйвера (Layer 2).

## Зависимости

- `Vortex.Core.System.Abstractions` — `SystemController`, `Singleton`, `ISystemDriver`, `SystemModel`
- `Vortex.Core.SaveSystem` — `ISaveable`, `SaveController`
- `Vortex.Core.LoaderSystem` — `IProcess`, `ProcessData`
- `Vortex.Core.LoggerSystem` — логирование ошибок
- `Cysharp.Threading.Tasks` — `UniTask` (асинхронные операции сохранения)

## Архитектура

```
Database (partial, SystemController<Database, IDriver>)
├── Database.cs         — реестры, API доступа, OnDriverConnect/Disconnect
├── DatabaseExtSave.cs  — ISaveable: GetSaveData(), OnLoad()
└── DatabaseExtEditor.cs — GetDriver() для editor-инструментов

Record (abstract partial, SystemModel)
├── GuidPreset    — string
├── Name          — string
├── Description   — string
├── GetDataForSave()      — abstract → string
└── LoadFromSaveData()    — abstract ← string

IDriver (ISystemDriver)
├── SetIndex(records, uniqRecords)
├── GetNewRecord<T>(guid)
├── GetNewRecords<T>()
└── CheckPresetType<T>(guid)

IDriverEditor (editor-only)
├── GetPresetForRecord(guid)
└── ReloadDatabase()
```

### Singleton vs MultiInstance

| Тип | Хранение | Доступ | Сохранение |
|-----|----------|--------|------------|
| `Singleton` | `Dictionary<string, Record>` | `GetRecord<T>(guid)` | Через `SaveSystem` (`ISaveable`) |
| `MultiInstance` | `HashSet<string>` (только GUID) | `GetNewRecord<T>(guid)` | Нет — новая копия каждый раз |

### IDriver

Контракт платформенного драйвера:

| Метод | Описание |
|-------|----------|
| `SetIndex(records, uniqRecords)` | Получение ссылок на реестры для заполнения |
| `GetNewRecord<T>(guid)` | Создание нового экземпляра из пресета |
| `GetNewRecords<T>()` | Все новые экземпляры MultiInstance по типу |
| `CheckPresetType<T>(guid)` | Проверка соответствия пресета типу |

### ISaveable (DatabaseExtSave)

- `GetSaveData()` — итерирует Singleton-записи, вызывает `Record.GetDataForSave()`, пропускает `null/empty`. Yield каждые 20 записей.
- `OnLoad()` — загружает данные из `SaveController`, вызывает `Record.LoadFromSaveData()` для существующих записей. Записи отсутствующие в реестре — игнорируются.

### RecordTypes

```csharp
enum RecordTypes { MultiInstance, Singleton }
```

### IRecord

Интерфейс-маркер (пустой).

## Контракт

### Вход
- Регистрация драйвера через `Database.SetDriver(IDriver)`
- Заполнение реестров — ответственность драйвера

### Выход
- `Database.GetRecord<T>(guid)` — Singleton-запись
- `Database.GetNewRecord<T>(guid)` — новая копия MultiInstance
- `Database.GetNewRecords<T>()` — все MultiInstance копии по типу
- `Database.GetRecords<T>()` / `GetRecords()` — все Singleton-записи
- `Database.TestRecord(guid)` — проверка существования
- `Database.GetMultiInstancePresets<T>()` — GUID всех MultiInstance пресетов по типу
- `Database.GetDriver()` — активный драйвер
- Событие `Database.OnInit` — после загрузки данных драйвером

### Гарантии
- `OnDriverConnect` передаёт ссылки на реестры и регистрирует в `SaveController`
- `OnDriverDisconnect` отписывает от `SaveController`
- `GetRecord` при запросе MultiInstance как Singleton — `null` + лог `Error`
- `GetNewRecord` при запросе Singleton как MultiInstance — `null` + лог `Error`
- Несуществующий GUID — `null` + лог `Error`
- Несовпадение типа — `null` + лог `Error`
- `TestRecord` проверяет оба реестра

### Ограничения
- Дубликаты GUID — последний перезаписывает (зависит от драйвера)
- `SetState` на `_data` без null-guard — обращение до инициализации приведёт к NRE
- Подписка на `OnInit` после инициализации — callback не вызовется
- `GetDataForSave()` возвращает `null/empty` — запись пропускается при сохранении
- `Record` — abstract, `internal` нет — экземпляры создаются через драйвер

## Использование

### Создание модели данных

```csharp
public class ProductRecord : Record
{
    public float Price { get; set; }
    public int Quantity { get; set; }

    public override string GetDataForSave()
        => this.SerializeProperties();

    public override void LoadFromSaveData(string data)
        => this.CopyFrom(data.DeserializeProperties<ProductRecord>());
}
```

### Доступ к данным

```csharp
// Singleton
var product = Database.GetRecord<ProductRecord>("product-guid");
product.Quantity -= 1;

// MultiInstance — новая копия
var template = Database.GetNewRecord<ProductRecord>("template-guid");

// Все записи типа
ProductRecord[] all = Database.GetRecords<ProductRecord>();
ProductRecord[] copies = Database.GetNewRecords<ProductRecord>();

// Проверка существования
bool exists = Database.TestRecord("guid");

// GUID всех MultiInstance
string[] guids = Database.GetMultiInstancePresets<ProductRecord>();
```

### Подписка на инициализацию

```csharp
Database.OnInit += () =>
{
    var settings = Database.GetRecord<GameSettings>("game-settings");
};
```

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Несуществующий GUID | `null` + лог `Error` |
| Singleton запрошен как MultiInstance | `null` + лог `Error` |
| MultiInstance запрошен как Singleton | `null` + лог `Error` |
| Несовпадение типа при `GetRecord<T>` | `null` + лог `Error` |
| Драйвер не назначен | `Instance` не создан, все вызовы — NRE |
| Подписка на `OnInit` после загрузки | Callback не вызовется |
| `GetDataForSave()` → `null` | Запись пропускается при сохранении |
| Запись в save, но не в реестре | Игнорируется при загрузке |

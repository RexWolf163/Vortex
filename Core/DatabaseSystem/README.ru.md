# DatabaseSystem (Core)

Платформонезависимая шина данных приложения с доступом по GUID.

## Назначение

Централизованное хранилище **лёгких записей** общего типа: индексация по GUID, два режима хранения (Singleton / MultiInstance), интеграция с SaveSystem, событийная модель, интерфейс драйвера.

Записи держатся **лёгкими** — без прямых ссылок на тяжёлые ассеты (см. раздел «Границы расширения»), что позволяет держать всю базу резидентно в памяти.

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
- Подписка на `OnInit` после инициализации — callback вызывается немедленно (accessor проверяет `IsInit` и сразу выполняет делегат)
- `GetDataForSave()` возвращает `null/empty` — запись пропускается при сохранении
- `Record` — abstract, `internal` нет — экземпляры создаются через драйвер

## Границы расширения

Database — это **шина общих данных**, а не хранилище ассетов. У неё есть граница применимости: не всё, что хочется «достать по ключу», должно стать `Record`.

### Правило: только лёгкие пресеты

Сущности Database расширяются **строго лёгкими пресетами**. «Лёгкий» — значит запись не тащит за собой тяжёлые ассеты (аудиоклипы, текстуры, префабы, меши) **прямой сериализованной ссылкой**. Тяжёлое выносится в отдельный ассет и подтягивается по линковке (addressable-ключ / путь), а сам `Record` хранит только ссылку + лёгкие метаданные.

Причина — в загрузке. Драйвер при инициализации поднимает **все** пресеты разом (`Resources.LoadAll` / `Addressables` по лейблам), и прямые ссылки на тяжёлые ассеты каскадом тянут их в память на старте. Лёгкие записи держать резидентно дёшево (тысяча пустых SO ≈ единицы-десятки мс); тяжёлые — нет. Поэтому вся БД может жить в памяти целиком, если каждая запись лёгкая, а тяжёлая загрузка размазана по требованию на уровне ассета (вне Database).

### Тест «принадлежит ли запись шине»

Запись оправдана в Database, если выполнено хотя бы одно:
- **Save** — `GetDataForSave()` возвращает осмысленное состояние (не `null`). Запись участвует в save/load как доменное состояние.
- **MultiInstance** — нужны рабочие копии с мутабельным состоянием (`GetNewRecord`).
- **Cross-domain** — ссылается по GUID из нескольких независимых систем или из save-данных.

Если ни одно не выполнено, а запись по сути **реестр ассетов** («именованный ассет + параметры»), и потребитель один — её дом не Database, а **собственный каталог** этой системы (по образцу `EffectsCatalog` в `EffectSpawnSystem`), с линковкой через свой атрибут-ключ.

`GetDataForSave() => null` — прямой маркер: запись не использует save-ось шины. Сам по себе он не приговор (Singleton-конфиг без состояния допустим), но вместе с «один потребитель + это ассет-реестр» означает, что место записи — в каталоге, не в шине.

### Матрица размещения данных

Две оси: **доступность** (частное / общее) и **вес** (лёгкое / тяжёлое).

| | Лёгкое | Тяжёлое |
|---|---|---|
| **Общее** | Database (лёгкий пресет напрямую) | Database (лёгкая запись) + тяжёлый ассет по линковке, загрузка/жизнь — на стороне представления-потребителя |
| **Частное** | Отдельный ассет-конфиг системы (не в Database) | Отдельный ассет-конфиг + линкованный тяжёлый ассет |

- **Общее-лёгкое** → Database. Чистый случай шины: GUID-доступ, типизация, save/MultiInstance.
- **Общее-тяжёлое** → Database держит лёгкую запись (идентичность, параметры, ссылку), а тяжёлый ассет линкуется и грузится по требованию системой-потребителем (представлением), а не шиной. Контракт Database остаётся синхронным и лёгким; владение жизнью ассета — у потребителя.
- **Частное** (вне зависимости от веса) → **не в Database**. Локальные данные системы живут в её собственном ассет-конфиге; тяжёлое внутри — по линковке. Класть частное в общую шину — нарушение «общее → шина, частное → внутри компонента».

> **Примечание про AudioSystem.** `AudioSystem` (звуки/музыка через `Database`) применять как систему под **общесистемные** звуки — те, что шарятся по всему приложению (клики UI, общие SFX, фоновая музыка меню). **Внутренние** звуки подгружаемых-выгружаемых систем (мини-игры, отдельные сцены, катсцены) лучше прописывать через **линкованные ассеты звуков** в конфиге самой этой системы, а не заводить в общий `Database`. Иначе локальный звук мини-игры висит в общей шине весь сеанс, хотя нужен только пока система загружена. Это прямое применение оси «частное-тяжёлое → отдельный ассет-конфиг + линковка».

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
| Подписка на `OnInit` после загрузки | Callback вызывается немедленно |
| `GetDataForSave()` → `null` | Запись пропускается при сохранении |
| Запись в save, но не в реестре | Игнорируется при загрузке |

# DatabaseSystem (Unity)

Unity-реализация драйверов базы данных, пресеты, атрибуты.

## Назначение

Платформенная адаптация `Database`: загрузка пресетов из Resources / Addressables, ScriptableObject-пресеты записей, атрибут для type-safe выбора записей в Inspector, настройки Addressable-меток.

- Два драйвера: `ResourcesDriver` и `AddressablesDriver`
- `DatabaseDriverBase` — общая логика (DRY)
- `RecordPreset<T>` — ScriptableObject-шаблон для записей
- `DbRecordAttribute` — фильтрация записей по типу/режиму в Inspector
- `DatabaseSettings` — конфигурация Addressable-меток
- Partial-расширение `Record` — добавление `Icon` (Sprite)

Вне ответственности: логика хранения, доступ по GUID, сохранение — это Core (Layer 1).

## Зависимости

- `Vortex.Core.DatabaseSystem` — `Database`, `Record`, `IDriver`, `RecordTypes`
- `Vortex.Core.LoaderSystem` — `Loader`, `IProcess`
- `Vortex.Core.SettingsSystem` — `Settings`, `SettingsModel`
- `Vortex.Core.Extensions` — `Crypto.GetNewGuid()`, `AddNew()`
- `Cysharp.Threading.Tasks` — `UniTask`
- `Sirenix.OdinInspector` — редакторские атрибуты
- `UnityEngine.AddressableAssets` (опционально, за `ENABLE_ADDRESSABLES`)

---

## Драйверы

### DatabaseDriverBase

Internal static класс — общая логика для обоих драйверов.

```
DatabaseDriverBase
├── _recordsLink              — Dictionary<string, Record> (ссылка на реестр Database)
├── _multiInstanceRecordsLink — HashSet<string> (ссылка на MultiInstance GUID)
├── _resourcesIndex           — Dictionary<string, IRecordPreset> (кеш пресетов)
├── SetIndex()                — получение ссылок на реестры
├── PutData(IRecordPreset)    — добавление пресета в кеш + запись в реестр
├── AddRecord()               — маршрутизация: Singleton → _recordsLink, MultiInstance → _multiInstanceRecordsLink
├── GetNewRecord<T>(guid)     — создание экземпляра из пресета
├── GetNewRecords<T>()        — все MultiInstance копии по типу
├── CheckPresetType<T>(guid)  — проверка соответствия типа
└── Clean()                   — очистка всех кешей
```

### ResourcesDriver

Загрузка из `Resources/Database/`.

```
DatabaseDriver (Singleton<DatabaseDriver>, IDriver, IProcess)
├── DatabaseDriver.cs                    — IDriver API, делегирование к DatabaseDriverBase
├── DatabaseDriverExtLoadingSystem.cs    — IProcess: Register, RunAsync
└── DatabaseDriverExtEditor.cs           — IDriverEditor: ReloadDatabase, GetPresetForRecord
```

**Загрузка:**
1. `[RuntimeInitializeOnLoadMethod]` → `Database.SetDriver(Instance)` + `Loader.Register(Instance)`
2. `RunAsync()` → `Resources.LoadAll("Database")` → фильтрация по `IRecordPreset` → `PutData()` для каждого
3. `CallOnInit()` → `Database.OnInit`

**WaitingFor:** `Type.EmptyTypes` — нет зависимостей.

### AddressablesDriver

Загрузка через Addressables API по меткам.

Структура аналогична `ResourcesDriver`. Код за `#if ENABLE_ADDRESSABLES`.

**Загрузка:**
1. `[RuntimeInitializeOnLoadMethod]` → `Database.SetDriver(Instance)` + `Loader.Register(Instance)`
2. `RunAsync()` → чтение меток из `Settings.Data().DatabaseLabels`
3. Для каждой метки: `Addressables.LoadAssetsAsync<IRecordPreset>(label, null)`
4. Все загруженные пресеты → `PutData()`
5. `CallOnInit()` → `Database.OnInit`
6. `finally` → `Addressables.Release(handle)` для каждого handle

**Требования:**
- Пакет `com.unity.addressables` (define `ENABLE_ADDRESSABLES` устанавливается автоматически через `DefinitionManager`)
- Метки указаны в `DatabaseSettings.databaseLabels`
- Пустой массив меток — лог ошибки, загрузка не выполняется

---

## RecordPreset\<T\>

Abstract ScriptableObject — шаблон данных для записей.

```
RecordPreset<T> (SoData, IRecordPreset) where T : Record, new()
├── type          — RecordTypes (Singleton / MultiInstance)
├── guid          — string (Crypto.GetNewGuid())
├── nameRecord    — string (auto-rename файла при изменении)
├── description   — string
├── icon          — Sprite
├── GetData()     — new T() + CopyFrom(this)
├── CheckRecordType<TU>() / CheckRecordType(Type)
├── ResetGuid()   — [Editor] генерация нового GUID
└── OnNameChanged() — [Editor] переименование ассета
```

### IRecordPreset

```csharp
interface IRecordPreset
{
    RecordTypes RecordType { get; }
    string GuidPreset { get; }
    string Name { get; }
    Record GetData();
    bool CheckRecordType<TU>() where TU : Record;
    bool CheckRecordType(Type type);
}
```

### Record (partial, Unity)

Partial-расширение Core-класса `Record`, добавляет:
```csharp
public Sprite Icon { get; protected set; }
```

---

## DbRecordAttribute

Атрибут для `string`-полей — type-safe picker записей в Inspector.

```csharp
[DbRecord]                                              // все записи
[DbRecord(typeof(ProductRecord))]                       // по типу
[DbRecord(RecordTypes.Singleton)]                       // по режиму
[DbRecord(typeof(TemplateRecord), RecordTypes.MultiInstance)] // тип + режим
```

Свойства:
- `RecordClass` — тип записи (default: `Record`)
- `RecordType` — nullable `RecordTypes` (default: `null` — все)

---

## DatabaseSettings

`SettingsPreset` для конфигурации Addressables-драйвера.

- `databaseLabels` — `string[]`, метки Addressable-ассетов
- В Editor: `[ValueDropdown("GetLabels")]` — dropdown из всех меток, назначенных ассетам в Addressable Asset Settings
- `SettingsModelExtDatabase` (partial `SettingsModel`) — `DatabaseLabels` property

---

## Использование

### 1. Создание пресета

```csharp
[CreateAssetMenu(menuName = "Database/Product")]
public class ProductPreset : RecordPreset<ProductRecord>
{
    [SerializeField] private float price;
    [SerializeField] private int quantity;

    public float Price => price;
    public int Quantity => quantity;
}
```

Для корректной работы `CopyFrom()` данные должны быть доступны через публичные свойства-геттеры с именами, совпадающими со свойствами `Record`.

### 2. Настройка ResourcesDriver

Пресеты размещаются в `Assets/Resources/Database/`. Драйвер назначается в `DriverConfig`.

### 3. Настройка AddressablesDriver

1. Пометить пресеты метками в окне Addressables
2. В ассете `DatabaseSettings` указать метки в `databaseLabels`
3. Назначить драйвер в `DriverConfig`

### 4. Атрибут DbRecord

```csharp
[SerializeField, DbRecord(typeof(Sound))]
private string audioSample;
```

### 5. Editor API

```csharp
#if UNITY_EDITOR
var driver = Database.GetDriver() as IDriverEditor;
driver?.ReloadDatabase();
var preset = driver?.GetPresetForRecord(guid);
#endif
```

## Редакторские инструменты

- `RecordPreset.ResetGuid()` — кнопка генерации нового GUID
- `RecordPreset.OnNameChanged()` — автоматическое переименование файла ассета
- `DbRecordAttribute` — picker с фильтрацией по типу и режиму
- `IDriverEditor.ReloadDatabase()` — обновление кеша без перезапуска
- `IDriverEditor.GetPresetForRecord(guid)` — получение пресета по GUID
- Кодогенерация: `Assets/Create/Vortex Templates/Record`, `Assets/Create/Vortex Templates/Preset for Record`

### Preset for Record — генерация свойств

Контракт иммутабельности сгенерированных свойств:

| Тип свойства Record | Поле Preset | Свойство Preset |
|---------------------|-------------|-----------------|
| Примитив / иммутабельный | `T field` | `=> field` |
| `List<T>` (T иммутабельный) | `T[] field` | `=> new List<T>(field)` |
| `List<T>` (T ссылочный) | `T[] field` | `=> new List<T>(Array.ConvertAll(field, e => e.DeepCopy()))` |
| Массив иммутабельных (`T[]`) | `T[] field` | `=> (T[])field.Clone()` |
| Массив ссылочных / прочие ссылочные | `T field` | `=> field.DeepCopy()` |

Иммутабельность определяется через `ObjectExtDeepClone.IsImmutable` (примитивы + платформенные типы `SimpleTypeMarker`).

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Пустая папка `Resources/Database` | Драйвер инициализируется без данных, `OnInit` вызывается |
| Пустой `databaseLabels` (Addressables) | Лог ошибки, загрузка не выполняется |
| Дубликат GUID в Singleton | `AddNew` — последний перезаписывает |
| Дубликат GUID в MultiInstance | Лог ошибки, GUID не добавляется повторно |
| `GetNewRecord` с несуществующим GUID | `null` + лог ошибки |
| `CheckRecordType` с несовпадающим типом | `false` |
| `SetDriver` возвращает `false` | Экземпляр уничтожается (`Dispose()`) |
| Переименование пресета в дубликат | Суффикс `(N)`, лог ошибки |
| `CancellationToken` при загрузке | Загрузка прерывается, `OnInit` не вызывается |

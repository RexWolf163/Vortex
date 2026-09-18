# SaveSystem (Core)

**Namespace:** `Vortex.Core.SaveSystem`, `Vortex.Core.SaveSystem.Bus`, `Vortex.Core.SaveSystem.Abstraction`, `Vortex.Core.SaveSystem.Model`
**Сборка:** `ru.vortex.save`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Система сохранения и загрузки данных. Две независимые шины:

- `SaveController` — слоты сохранения: процессы save/load, реестр `ISaveable`-модулей, асинхронный сбор/раздача данных. Данные хранятся как `Dictionary<string, Dictionary<string, string>>` — иерархия модуль → ключ → значение (строки).
- `GlobalSaveController` — глобальное хранилище межсессионных данных, которые переживают слоты, новые игры и перезапуски. Описано в разделе [Глобальное хранилище](#глобальное-хранилище).

Возможности слотов:

- `SaveController` — шина: `Save()`, `Load()`, `Remove()`, `GetIndex()`
- `ISaveable` — интерфейс для модулей, данные которых подлежат сохранению
- Асинхронный сбор данных при `Save`, асинхронная раздача при `Load` (UniTask)
- `SaveProcessData` — двухуровневый прогресс (глобальный + модульный)
- События: `OnSaveStart`, `OnSaveComplete`, `OnLoadStart`, `OnLoadComplete`, `OnRemove`
- `SaveSummary` — метаданные сохранения (имя, дата, версия приложения, XML-сериализуемый)
- Автогенерация GUID для новых сохранений

Вне ответственности:

- Физическое хранение (PlayerPrefs, файлы) — Unity-слой (драйверы)
- Сжатие тела слота — Unity-слой (драйвер слотов); контейнер глобального хранилища кодирует Core-контроллер
- Шифрование — не выполняется; для данных глобального хранилища — забота модуля
- UI-отображение прогресса — Unity-слой

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `ProcessData` |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid()`, `DictionaryExt.AddNew()` |
| `Vortex.Core.LoggerSystem` | `Log.Print()` при ошибках |
| `Vortex.Core.ComplexModelSystem` | `ComplexModel<T>` — основа `GlobalModel` |
| `Vortex.Core.AppSystem` | `App.OnStateChanged` (немедленная запись в `Unfocused`/`Stopping`), `App.Exit()` (fail-fast) |
| `Vortex.Core.SettingsSystem` | `Settings.Data()` — число копий и fail-fast; расширение `SettingsModel` через asmref |
| UniTask | `UniTask`, `CancellationToken` (в `ISaveable`, `IProcess`) |

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
  ├── Save(name, guid?) → UniTask<string>
  │    ├── State = Saving, OnSaveStart
  │    ├── foreach ISaveable → GetSaveData() → SaveDataIndex
  │    ├── guid ??= Crypto.GetNewGuid()
  │    ├── Driver.Save(name, guid)
  │    └── State = Idle, OnSaveComplete
  │
  ├── Load(guid) → UniTask
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
  ├── GetIndex() → Dictionary<string, SaveSummary>
  └── GetNumberLastSave() → int
```

### Формат данных

```
SaveDataIndex: Dictionary<string, Dictionary<string, string>>
  └── "ModuleA" → { "key1" → "json1", "key2" → "json2" }
  └── "ModuleB" → { "key1" → "json1" }
```

Каждый `ISaveable` возвращает свой `GetSaveId()` (идентификатор модуля) и `Dictionary<string, string>` (ключ → JSON-строка). `SaveController` агрегирует все модули в `SaveDataIndex`.

### Жизненный цикл Save

1. Проверка замка (`State == Saving` → возврат `null`)
2. `State = Saving`, `OnSaveStart`
3. `SaveDataIndex.Clear()`
4. Для каждого `ISaveable` — `await GetSaveData(token)` → добавление в `SaveDataIndex`
5. Генерация GUID если не передан
6. `Driver.Save(name, guid)` — физическое сохранение
7. `State = Idle`, `OnSaveComplete`

### Жизненный цикл Load

1. Проверка замка (`State == Loading` → return)
2. `State = Loading`, `OnLoadStart`
3. `Driver.Load(guid)` — драйвер заполняет `SaveDataIndex`
4. Для каждого `ISaveable` — `await OnLoad(token)` (модуль читает из `SaveController.GetData()`)
5. `State = Idle`, `OnLoadComplete`

### SaveProcessData — двухуровневый прогресс

| Уровень | Поле | Описание |
|---------|------|----------|
| Глобальный | `Global.Progress` / `Global.Size` | Текущий модуль / всего модулей |
| Модульный | `Module.Progress` / `Module.Size` | Прогресс внутри текущего модуля |

### Структуры данных

| Тип | Назначение |
|-----|-----------|
| `SaveData` | struct: `Id`, `Data` — единица данных |
| `SaveFolder` | struct: `Id`, `SaveData[] DataSet` — папка модуля |
| `SaveSummary` | struct: `Name`, `Date`, `UnixTimestamp`, `Version` — метаданные сохранения (XML-сериализуемый). `Version` — строковая версия приложения (`Application.version`), которая была активна на момент создания записи. Полезна для миграций и фильтрации старых сейвов в UI. |
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
| `SaveController.Save(name, guid?)` | Сохранение, `UniTask<string>` (возвращает GUID или `null` если идёт другой Save) |
| `SaveController.Load(guid)` | Загрузка, `UniTask` |
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
| `Save` — замок от повторного вызова | `State == Saving` → return `null` |
| `Load` — замок от повторного вызова | `State == Loading` → return |
| Данные — только строки | `Dictionary<string, string>`, JSON-сериализация на стороне модуля |
| `Save`/`Load` возвращают `UniTask` | Можно ждать через `await` либо запускать fire-and-forget через `.Forget()`. Исключения логируются внутри |
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
| `Save` во время сохранения | Блокируется (`State == Saving` → возврат `null`) |
| `Load` во время загрузки | Блокируется (`State == Loading` → выход без действий) |
| `GetData` с несуществующим `id` | `Log.Print(Error)`, возвращает пустой `Dictionary` |
| GUID не передан в `Save` | Генерируется `Crypto.GetNewGuid()` |
| Исключение в `GetSaveData` / `OnLoad` | `Log.Print(Error)`, `State = Idle`, событие Complete вызывается |
| `ISaveable` не зарегистрирован | Данные не собираются/не раздаются |
| `SaveSummary` — XML-сериализация | `Date` как `UnixTimestamp` (long), `DateTime.FromFileTimeUtc` |

---

## Глобальное хранилище

`GlobalSaveController` хранит межсессионные данные: купленные дополнения, пройденные квесты, общую статистику, время в приложении. Данные делятся на модули (`IGlobalData`); хранилище находит их рефлексией, как `GameModel` — модули `IGameData`. Слоты (`SaveController`) глобальных данных не касаются: новая игра, загрузка и удаление сейва их не трогают, в списке сейвов хранилище не показывается.

### Архитектура

```
GlobalSaveController : SystemController<GlobalSaveController, IGlobalSaveDriver>, IProcess  (partial)
  ├── GlobalModel : ComplexModel<IGlobalData>     ← все модули, найденные рефлексией
  ├── ключ модуля → модуль                        ← только модули с уникальным непустым ключом
  │
  ├── RunAsync()  (Loader, фаза Starting)
  │    ├── индекс ключей; модули → значения по умолчанию (в тех же экземплярах)
  │    ├── Driver.Read → Decompress → XML → папки модулей
  │    │    └── нечитаемо → самая свежая целая копия (при копиях > 0)
  │    ├── папки → существующие экземпляры модулей
  │    └── CallOnInit() → IsInit = true, шлюз OnInit
  │
  ├── Commit<T>()  → запись в конце кадра (Driver.ScheduleFlush); Unfocused/Stopping — сразу
  ├── Reset<T>() / Reset(Type) / ResetAll()  → значения по умолчанию на месте → запись сразу
  ├── OnChanged(IReadOnlyList<Type>)          ← одно уведомление на запись
  └── Stopping → запись незаписанного + резервная копия (одна за запуск)

IGlobalData  [POCO]
  └── GetGlobalKey() → string

IGlobalSaveDriver : ISystemDriver
  ├── Read(out data) → GlobalReadStatus (Ok / NoData / Error)
  ├── Write(data)                                  ← атомарно
  ├── WriteCopy(id, data) / GetCopies() / ReadCopy(id, out data) / DeleteCopy(id)
  └── ScheduleFlush(Action)                        ← конец кадра, повтор заменяет предыдущий
```

Драйвер — «глупое» хранилище строк: формат контейнера, выбор целой копии и ротацию копий знает только контроллер. Конец кадра — понятие движка, поэтому откладывание записи тоже делает драйвер. Драйверы — Unity-слой (`GlobalFileDriver`, `GlobalPrefsDriver`); принятый драйвер ставит контроллер в очередь `Loader`.

Готовность объявляет контроллер по завершении чтения, драйвер `OnInit` не поднимает. Поэтому `IsInit` означает «хранилище прочитано», а `WaitingFor(typeof(GlobalSaveController))` в `Loader` ждёт именно чтения.

### Формат

`GlobalContainer` — XML по образцу `SavePreset`: список `SaveData` «ключ модуля → строка сериализатора Vortex». Контейнер сжимается `Compress(…, "global")`, как тело слота. Шифрования нет: файл открывается архиватором, защита от правки — забота модуля. Версии приложения в контейнере нет: формат данных — забота модуля.

### Жизненный цикл

1. **До загрузки** `Get<T>()` отдаёт значения по умолчанию — те, что задал конструктор модуля.
2. **Загрузка (Starting).** Сохранённые данные заливаются в существующие экземпляры модулей — ссылки, полученные раньше, остаются рабочими. Нет контейнера — первый запуск, значения по умолчанию. Нечитаемый контейнер при включённых копиях поднимается из самой свежей целой копии и сразу перезаписывается ею.
3. **Готовность.** `IsInit = true`, срабатывают подписчики `OnInit`. Хранилище готово раньше загрузки любых слотов.
4. **Фиксация.** Модуль меняет данные и вызывает `Commit<T>()`. Все фиксации кадра — одна запись и одно `OnChanged`. В `Unfocused` и `Stopping` запись и уведомление сразу: после этих переходов кадров может не быть, а порядок обработчиков `Stopping` не определён.
5. **Завершение.** Незаписанное пишется сразу; при числе копий > 0 делается одна копия за запуск, самые старые удаляются.

### Настройки

| Где | Параметр | По умолчанию |
|-----|----------|--------------|
| `DriverConfig`, строка `GlobalSaveController` | Драйвер: файл или PlayerPrefs | — (без драйвера хранилище не загружается) |
| `SaveSettings` (Unity) → `SettingsModel.GlobalSaveBackups` | Число резервных копий | 0 — копий нет |
| `SaveSettings` (Unity) → `SettingsModel.GlobalSaveFolder` | Папка файла (файловый драйвер) | `Global` |
| `DebugSettings` (Unity) → `SettingsModel.GlobalSaveFailFast` | Fail-fast для ошибок чтения, дублей и пустых ключей; только редактор, от `DebugMode` не зависит | включён |

Fail-fast: загрузка останавливается (`App.Exit()`), шлюз не открывается, запись заблокирована — испорченный файл не перезаписывается. В билде fail-fast всегда выключен: значения по умолчанию и лог.

### API

| Член | Описание |
|------|----------|
| `Get<T>()` | Модуль с текущими значениями. До загрузки — значения по умолчанию |
| `HasStoredData<T>()` | Данные модуля были в прочитанном контейнере — для миграций |
| `Commit<T>()` / `Commit(Type)` | Зафиксировать изменения модуля. До загрузки — отклоняется с ошибкой. Перегрузка по типу — для инструментов |
| `Reset<T>()` / `Reset(Type)` | Модуль — к значениям по умолчанию в том же экземпляре, запись сразу, уведомление |
| `ResetAll()` | То же для всех модулей |
| `OnInit` / `IsInit` | Шлюз и признак готовности (хранилище прочитано) |
| `OnChanged` | Записаны изменения: типы модулей. Приходит независимо от успеха записи |
| `Modules` | Модули по ключу — для инструментов |

### Модуль данных: инструкция для авторов

```csharp
public class QuestFlagsData : IGlobalData
{
    public List<string> Completed { get; internal set; } = new();

    public string GetGlobalKey() => "MyGame.QuestFlags";
}

// Изменение — только контроллер своего пакета
var flags = GlobalSaveController.Get<QuestFlagsData>();
if (!flags.Completed.Contains(questId))
{
    flags.Completed.Add(questId);
    GlobalSaveController.Commit<QuestFlagsData>();
}

// Реакция: после загрузки и на изменения
GlobalSaveController.OnInit += RefreshBackground;
GlobalSaveController.OnChanged += types =>
{
    if (types.Contains(typeof(QuestFlagsData)))
        RefreshBackground();
};
```

Правила:

- **Конструктор без параметров обязателен** — он задаёт значения по умолчанию. Модуль без него не попадёт в реестр.
- **Ключ стабилен.** Смена ключа после релиза — потеря данных модуля. Переименование и перенос класса модуля безопасны: при чтении маркер типа подменяется текущим типом. Имена вложенных POCO-типов — забота модуля.
- **Данные — свойства** с getter и setter (правила сериализатора). Поля не сохраняются и при сбросе не сбрасываются.
- **Когда фиксировать, решает модуль:** факт квеста — в момент прохождения, кэш покупок — при ответе платформы.
- **Смена формата** — забота модуля: недостающие свойства берут значения по умолчанию, лишние игнорируются; словари сливаются с текущими, списки и массивы заменяются.
- **Миграции** — через `HasStoredData<T>()`. Пример — `AppTimeData` в SDK GameCore: перенос из `PlayerPrefs`, старый ключ удаляется, когда следующий запуск прочитал перенесённое значение.
- **Шифрование — на модуле.** `Crypto.SetCryptoPack` выполняет PBKDF2 на каждый вызов (порядка сотен миллисекунд), поэтому шифровать стоит только при изменении данных.
- **Ссылку на модуль держать можно:** загрузка и сброс меняют значения в том же экземпляре.
- **Проверка модуля** — окно `Tools/Vortex/SaveData/Global Index` (Unity SaveSystem): вне Play Mode покажет модуль, его ключ и проблемы, из-за которых хранилище его пропустит; в Play Mode — текущие значения с правкой на лету.

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Нет шифрования | Защита данных — забота модуля; файл правится вручную |
| При числе копий 0 испорченный контейнер не восстанавливается | Разработчик отказался от страховки |
| Данные отключённых пакетов при следующей записи пропадают | Настройки пакетов структурообразующие: отключение считается окончательным |
| Копия делается только при `Stopping` | Процесс, убитый без `Stopping`, копии за этот запуск не оставит |
| Облачная синхронизация не объединяет данные устройств | Вне области пакета |

### Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Нет драйвера в `DriverConfig` | Загрузки нет, `IsInit = false`; `Get` — значения по умолчанию, фиксации отклоняются с ошибкой |
| Первый запуск (контейнера нет) | Значения по умолчанию; контейнер создаёт первая фиксация |
| Основной контейнер нечитаем, копии есть | Самая свежая целая копия, предупреждение в лог, основной контейнер сразу перезаписывается ею |
| Нечитаем, целых копий нет (или копии выключены) | Ошибка в лог, значения по умолчанию; первая фиксация перезаписывает контейнер. Редактор с fail-fast — загрузка останавливается |
| Нечитаемые данные одного модуля | Модуль по умолчанию, ошибка в лог, остальные читаются; fail-fast — как выше |
| Два модуля с одним ключом | Оба по умолчанию, не читаются и не пишутся; ошибка в лог; fail-fast — как выше |
| Пустой ключ или исключение в `GetGlobalKey` | Модуль вне индекса; ошибка в лог; fail-fast — как выше |
| Данные в контейнере без модуля | Предупреждение; не читаются, при следующей записи пропадают |
| Фиксация или сброс до загрузки | Ошибка в лог, операция отклонена |
| Серия фиксаций за кадр | Одна запись, одно `OnChanged` |
| Ошибка записи | Ошибка в лог; данные в памяти, следующая фиксация повторит запись; `OnChanged` всё равно приходит |
| Рестарт без выгрузки домена | Экземпляры модулей те же, значения сбрасываются к умолчанию перед чтением |

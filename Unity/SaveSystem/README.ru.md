# SaveSystem (Unity)

**Namespace:** `Vortex.Unity.SaveSystem.Drivers.PlayerPrefsDriver`, `Vortex.Unity.SaveSystem.Drivers.FileSystemDriver`, `Vortex.Unity.SaveSystem.Drivers.GlobalPrefsDriver`, `Vortex.Unity.SaveSystem.Drivers.GlobalFileDriver`, `Vortex.Unity.SaveSystem.Presets`, `Vortex.Unity.SaveSystem.View`, `Vortex.Unity.SaveSystem.Editor`
**Сборка:** `ru.vortex.unity.save`
**Платформа:** Unity 2021.3+

---

## Назначение

Unity-слой системы сохранений. Предоставляет сменные драйверы хранения слотов (XML-сериализация и сжатие) и глобального хранилища, общий ассет настроек `SaveSettings`, UI-компонент индикации прогресса и окно глобального хранилища. Активные драйверы выбираются через `DriverConfig` (codegen-whitelist) — у каждой шины своя строка.

Возможности:

- `PlayerPrefsDriver/SaveSystemDriver` — драйвер слотов на `PlayerPrefs`
- `FileSystemDriver/FileSystemDriver` — драйвер слотов на файловую систему (`FileBus.GetAppPath()/{savesFolder}/`, по умолчанию `Saves`)
- `GlobalPrefsDriver`, `GlobalFileDriver` — драйверы глобального хранилища (`GlobalSaveController`)
- `SavePreset` — XML-сериализуемая обёртка для `SaveFolder[]` (общая для драйверов слотов)
- `SaveSettings` — общий ассет настроек: папка сейвов, число копий, папка глобального хранилища и список блоков коррекции устаревших сейвов
- `UISaveLoadComponent` — MonoBehaviour для отображения прогресса save/load
- Окно `Tools/Vortex/SaveData/Global Index` — индекс модулей глобального хранилища; в Play Mode — текущие значения с правкой на лету и сброс
- Каждый драйвер слотов хранит индекс сохранений и метаданные (`SaveSummary`) в своём формате

Вне ответственности:

- `SaveController`, `GlobalSaveController`, `ISaveable`, `IGlobalData`, модели данных — Core
- Логика сбора/раздачи данных, кодек и резервные копии глобального хранилища — Core
- Шифрование (за пределами сжатия) — прикладной уровень

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.SaveSystem` | `SaveController`, `IDriver`, `SaveData`, `SaveFolder`, `SaveSummary`, `SaveProcessData` |
| `Vortex.Core.System` | `Singleton<T>`, `SystemController`, `DriversGenericList.WhiteList` |
| `Vortex.Core.Extensions` | `DictionaryExt.AddNew()`, `StringExtensions.Compress/Decompress`, `IsNullOrWhitespace()` |
| `Vortex.Core.LocalizationSystem` | `StringExt.Translate()` (в `UISaveLoadComponent`) |
| `Vortex.Unity.LocalizationSystem` | `[LocalizationKey]` атрибут |
| `Vortex.Unity.UI.UIComponents` | `UIComponent` (в `UISaveLoadComponent`) |
| `Vortex.Unity.FileSystem` | `FileBus.GetAppPath()`, `FileBus.CreateFolders()` (в файловых драйверах) |
| `Vortex.Core.SettingsSystem` | `Settings.Data()` — папки и число копий из `SaveSettings` |
| `Vortex.Core.LoaderSystem` | `Loader.Register` — глобальный драйвер ставит контроллер в очередь загрузки |
| `Vortex.Unity.AppSystem` | `TimeController.Call` — запись глобального хранилища в конце кадра |
| `Vortex.Unity.SettingsSystem` | `SettingsPreset` — база `SaveSettings` (asmref в `ru.vortex.unity.settings`) |
| `Vortex.Unity.DriverManagerSystem` | `DriverConfig` ассет, `DriversGenericList.cs` codegen |

---

## Выбор активного драйвера

Оба драйвера регистрируются автоматически через `[RuntimeInitializeOnLoadMethod]`, но `SystemController.SetDriver` валидирует кандидата против codegen-whitelist `DriversGenericList.WhiteList`, наполняемого из ассета `DriverConfig`. Только драйвер, явно прописанный в whitelist для системы `SaveController`, будет принят; остальные будут вызвать `Dispose()`.

```
DriverConfig (ScriptableObject в Resources/)
    ↓ codegen
DriversGenericList.cs   (WhiteList: SystemType → DriverType)
    ↓ читается рефлексией при первом SetDriver
SystemController.SetDriver(driver) → принимает только whitelist-кандидата
```

Для смены драйвера: открыть ассет `DriverConfig`, выбрать нужный `DriverType` для `SaveController`, нажать «Save Config» — перегенерировать `DriversGenericList.cs`.

У глобального хранилища своя строка — `GlobalSaveController`: `GlobalFileDriver/GlobalFileDriver` или `GlobalPrefsDriver/GlobalPrefsDriver`. Без неё хранилище не загружается, а фиксации отклоняются с ошибкой.

---

## Архитектура

### Общая структура

```
Vortex/Unity/SaveSystem/
├── Drivers/
│   ├── PlayerPrefsDriver/
│   │   ├── SaveSystemDriver.cs                — partial: IDriver + поля
│   │   ├── SaveSystemDriverExtRun.cs          — [RuntimeInitializeOnLoadMethod]
│   │   └── Editor/SaveSystemDriverExtEditor.cs — [InitializeOnLoadMethod]
│   ├── FileSystemDriver/
│   │   ├── FileSystemDriver.cs                — каркас, поля
│   │   ├── FileSystemDriver.Run.cs            — bootstrap, Init
│   │   ├── FileSystemDriver.Save.cs           — Save + BuildSavePreset
│   │   ├── FileSystemDriver.Load.cs           — Load, Remove
│   │   ├── FileSystemDriver.Index.cs          — GetIndex, GetNumberLastSave, ScanIndex
│   │   ├── FileSystemDriver.Paths.cs          — пути и имена файлов (папка — из SaveSettings)
│   │   ├── FileSystemDriver.Serialization.cs  — XML serialize/deserialize, Compress
│   │   └── Editor/FileSystemDriverExtEditor.cs — [InitializeOnLoadMethod]
│   ├── GlobalPrefsDriver/GlobalPrefsDriver.cs — глобальное хранилище в PlayerPrefs
│   └── GlobalFileDriver/GlobalFileDriver.cs   — глобальное хранилище в файле
├── Settings/                                  — asmref → ru.vortex.unity.settings
│   ├── SaveSettings.cs                        — общий ассет настроек
│   └── SaveSettingsMenu.cs                    — Tools/Vortex/Configs/Save Settings
├── Debug/                                     — asmref → ru.vortex.unity.debug
│   └── DebugSettingsExtGlobalSave.cs          — тумблер fail-fast глобального хранилища
├── Editor/GlobalDataIndexWindow.cs            — Tools/Vortex/SaveData/Global Index
├── Presets/SavePreset.cs                      — общий XML-контейнер слотов
└── View/UISaveLoadComponent.cs                — UI прогресса
```

### Driver: PlayerPrefs

Хранит сейвы как ключи в `PlayerPrefs`. Подходит для коротких сейвов и платформ с ограниченным файловым доступом.

```
SaveSystemDriver : Singleton<SaveSystemDriver>, IDriver  (partial)
  ├── Saves: Dictionary<string, SaveSummary>     ← in-memory индекс
  ├── _saveDataIndex → SaveController.SaveDataIndex
  │
  ├── Init()
  │    ├── PlayerPrefs.GetString("SavesData") → "guid1;guid2;..."
  │    └── Для каждого GUID → GetSaveSummary() → Saves
  │
  ├── Save(name, guid)
  │    ├── _saveDataIndex → SavePreset (XML) → Compress(guid) → PlayerPrefs "Save-{guid}"
  │    ├── SaveSummary { Name, Version = Application.version } → XML → PlayerPrefs "SaveSummary-{guid}"
  │    └── Обновление "SavesData", инкремент "SavesCount"
  │
  ├── Load(guid)
  │    ├── PlayerPrefs "Save-{guid}" → Decompress(guid) → XML → SavePreset
  │    └── SaveFolder → _saveDataIndex
  │
  ├── Remove(guid)
  │    ├── Saves.Remove(guid)
  │    ├── PlayerPrefs.DeleteKey "Save-{guid}", "SaveSummary-{guid}"
  │    └── Обновление "SavesData"
  │
  ├── [RuntimeInitializeOnLoadMethod] Run()
  └── [InitializeOnLoadMethod] EditorRegister()
```

#### Формат хранения PlayerPrefs

| Ключ | Содержимое |
|------|-----------|
| `SavesData` | `"guid1;guid2;guid3"` — список всех GUID через `;` |
| `SavesCount` | `int` — инкремент-счётчик последнего сейва |
| `Save-{guid}` | Сжатая XML-строка (`SavePreset`), ключ сжатия = GUID |
| `SaveSummary-{guid}` | XML-строка (`SaveSummary`) — имя и дата |

### Driver: FileSystem

Хранит сейвы как файлы на диске. Корневой путь — `FileBus.GetAppPath()/{savesFolder}/` (`SaveSettings`, по умолчанию `Saves`; пусто — корень). Подходит для больших сейвов и read/write операций без ограничений PlayerPrefs.

```
FileSystemDriver : Singleton<FileSystemDriver>, IDriver  (partial)
  ├── Saves: Dictionary<string, SaveSummary>     ← in-memory индекс
  ├── _saveDataIndex → SaveController.SaveDataIndex
  │
  ├── Init()
  │    └── ScanIndex() → читает все *.summary в Saves/
  │
  ├── Save(name, guid)
  │    ├── _saveDataIndex → SavePreset (XML) → Compress(guid) → {guid}.save
  │    ├── SaveSummary { Name, Version = Application.version } → XML → {guid}.summary
  │    └── При новом GUID — _increment = GetNumberLastSave() + 1 → запись в файл .in
  │
  ├── Load(guid)
  │    ├── File.ReadAllText({guid}.save) → Decompress(guid) → XML → SavePreset
  │    └── SaveFolder → _saveDataIndex
  │
  ├── Remove(guid)
  │    ├── File.Delete({guid}.save), File.Delete({guid}.summary)
  │    └── Saves.Remove(guid)
  │
  ├── [RuntimeInitializeOnLoadMethod] Run()
  └── [InitializeOnLoadMethod] EditorRegister()
```

#### Формат хранения FileSystem

Пути даны для папки по умолчанию `Saves`.

| Файл | Содержимое |
|------|-----------|
| `Saves/{guid}.save` | Сжатая XML-строка (`SavePreset`), ключ сжатия = GUID |
| `Saves/{guid}.summary` | XML-строка (`SaveSummary`) — имя, дата и `Application.version` на момент сохранения |
| `Saves/.in` | `int` — инкремент-счётчик последнего сейва |

### SavePreset (общий)

```
SavePreset [XmlRoot]
  └── Data: List<SaveFolder>                    ← XML-сериализуемый контейнер
```

Используется обоими драйверами для сериализации `SaveFolder[]`.

### UISaveLoadComponent

```
UISaveLoadComponent : MonoBehaviour
  ├── title: UIComponent                        ← "Загрузка" / "Сохранение"
  ├── progress: UIComponent                     ← форматированный прогресс
  ├── loadingText, savingText: string           ← [LocalizationKey]
  ├── progressTextPattern: string               ← [LocalizationKey], pattern для string.Format
  └── Run() → Coroutine: обновление текста каждый кадр
```

### SaveSettings (общий ассет)

`SaveSettings` — `SettingsPreset` в `Resources/Settings`, общий для слотов и глобального хранилища; меню `Tools/Vortex/Configs/Save Settings`. Файл компилируется в сборку `ru.vortex.unity.settings` (asmref в `Settings/`), значения копируются в `SettingsModel`, откуда их читают драйверы и Core-контроллер.

| Поле | Свойство `SettingsModel` | Кто читает | По умолчанию |
|------|--------------------------|------------|--------------|
| `savesFolder` | `SavesFolder` | `FileSystemDriver` | `Saves` |
| `globalSaveBackups` | `GlobalSaveBackups` | `GlobalSaveController` | `0` — копий нет |
| `globalSaveFolder` | `GlobalSaveFolder` | `GlobalFileDriver` | `Global` |
| `reactors` | — (читается из пресета) | драйверы слотов | пусто |

`reactors` — блоки коррекции устаревших сейвов (`SaveReactor`, `[SerializeReference]`). В `SettingsModel` список не переносится: расширение модели собирается в сборку настроек, а она о SaveSystem не знает — ссылка дала бы цикл сборок. Драйверы берут список из самого пресета через `SaveSettings.GetReactors()` (ленивая загрузка из `Resources/Settings`; ассета нет — пустой список). Механизм описан в README Core SaveSystem.

Папки задаются относительно `FileBus.GetAppPath()`; пусто — корень. Путь склеивается `Path.Combine`: абсолютный путь в поле заменит корень. Файловые драйверы читают папку при подключении — смена во время игры применится со следующего запуска.

### Драйверы глобального хранилища

Реализуют `IGlobalSaveDriver` (Core): только хранение строк и откладывание записи до конца кадра — `TimeController.Call(flush, this)`, повтор в кадре заменяет предыдущий. Формат, копии и их ротацию ведёт `GlobalSaveController`.

Bootstrap: `[RuntimeInitializeOnLoadMethod]` → `GlobalSaveController.SetDriver(Instance)`. Принятый драйвер регистрирует контроллер в `Loader`, отклонённый — `Dispose()`. Событие `OnInit` драйвер не поднимает: готовность объявляет контроллер после чтения.

#### GlobalFileDriver

| Файл | Содержимое |
|------|-----------|
| `{globalSaveFolder}/GlobalSave.dat` | Контейнер (сжатый XML) |
| `{globalSaveFolder}/GlobalSave_copy_{id}.dat` | Резервные копии; `id` — UTC-тики |
| `*.tmp` | Временный файл атомарной записи |

Запись атомарная: временный файл, затем `File.Replace` / `File.Move`. Расширения `.summary` нет — в список сейвов файл не попадает.

#### GlobalPrefsDriver

| Ключ | Содержимое |
|------|-----------|
| `VortexGlobalSave` | Контейнер |
| `VortexGlobalSave_copy_{id}` | Резервные копии |
| `VortexGlobalSave_copies` | Перечень копий через `;` — PlayerPrefs не перечисляет ключи |

Атомарность записи ключа обеспечивает платформенная реализация PlayerPrefs.

### Окно Global Data

`Tools/Vortex/SaveData/Global Index` (`Editor/GlobalDataIndexWindow.cs`, редакторная часть рантайм-сборки). Режим зависит от Play Mode.

**Вне Play Mode — индекс модулей проекта.** Все реализации `IGlobalData` (`TypeCache`): ключ, тип, сборка, значения по умолчанию (только чтение). Отмечаются проблемы, из-за которых хранилище пропустит модуль:

- нет публичного конструктора без параметров — модуль не будет найден;
- пустой ключ — модуль не читается и не записывается;
- ключ повторяется — все модули с этим ключом не читаются и не записываются (с перечнем).

«Обновить» пересобирает индекс; после перекомпиляции он пересобирается сам.

**В Play Mode — содержимое хранилища с правкой на лету.** Модули по ключу (`GlobalSaveController.Modules`):

- изменённое значение сразу фиксируется (`GlobalSaveController.Commit(Type)`) — запись и `OnChanged`, как от кода модуля; подписчики (контейнеры, условия квестов) реагируют штатно;
- «Сбросить» у модуля и «Сбросить всё» (с подтверждением) — значения по умолчанию, запись сразу;
- пока хранилище не загружено или для него не выбран драйвер — сообщение вместо списка.

«Настройки» — переход к `SaveSettings` (в обоих режимах).

**Какие свойства видны.** Ровно те, что сохраняет сериализатор: getter и setter, публичный getter или `[IsPOCO]`, без `[NotPOCO]`. Список свойств и поля значений даёт общий `EditorTools/DataTools/PocoInspector.cs` — тот же, что у окна данных игры `Tools/Vortex/SaveData/Game Index`.

| Тип свойства | Отображение |
|--------------|-------------|
| `bool`, `int`, `long`, `float`, `double`, `string`, `enum` | Поле, редактируется |
| Коллекции, вложенные объекты, прочие типы | Строка сериализатора, только чтение |

### Тумблер fail-fast

`DebugSettings.globalSaveFailFast` (`Debug/`, asmref в `ru.vortex.unity.debug`) → `SettingsModel.GlobalSaveFailFast`. Включён по умолчанию, работает только в редакторе, не подчинён `DebugMode`. Поведение описано в README Core SaveSystem.

---

## Сжатие и коррекция

Оба драйвера сжимают тело сейва через `string.Compress(guid)` и распаковывают через `string.Decompress(guid)`. GUID используется как ключ сжатия. Метаданные (`SaveSummary`) и инкремент-файл (`.in`) **не сжимаются**.

Порядок на загрузке: чтение → `Decompress(guid)` → `SaveReactors.Apply(raw, version, SaveSettings.GetReactors())` → разбор XML. Версия берётся из сводки, прочитанной на `Init`, поэтому обращений к диску не добавляется; сводки нет — сейв считается самым старым. Правило и формат реакторов — в README Core SaveSystem.

---

## Контракт

### Вход

- Драйверы регистрируются автоматически через `[RuntimeInitializeOnLoadMethod]`
- Активный выбирается через `DriverConfig` → `DriversGenericList.WhiteList`
- `SaveController.Save/Load/Remove` делегируют активному драйверу
- Папка сейвов, число копий и папка глобального хранилища — `SaveSettings`

### Выход

- Данные хранятся согласно формату активного драйвера (PlayerPrefs или файлы)
- `GetIndex()` — `Dictionary<string, SaveSummary>` из памяти драйвера

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Хранение в `PlayerPrefs` (PlayerPrefsDriver) | Ограничение размера зависит от платформы |
| Сжатие через GUID как ключ | `Compress`/`Decompress` из `StringExtensions` |
| Файловые операции синхронные (FileSystemDriver) | Простота; для крупных сейвов можно вынести в async позже |
| Имя файла инкремента — `.in` | Скрытый файл на Unix/Mac, обычный на Windows |
| Активный драйвер только один | Через codegen-whitelist `DriversGenericList` |
| `UISaveLoadComponent` — Coroutine | Обновление каждый кадр, не UniTask |

---

## Использование

### Индикация прогресса

1. Добавить `UISaveLoadComponent` на UI-элемент
2. Назначить `title` и `progress` (`UIComponent`)
3. Указать ключи локализации: `loadingText`, `savingText`, `progressTextPattern`
4. Формат `progressTextPattern`: `"{0}/{1} — {2} ({3}%)"` — глобальный прогресс, имя модуля, процент модуля

### Работа с сохранениями

```csharp
// Все сохранения
var saves = SaveController.GetIndex();

// Сохранение
SaveController.Save("Слот 1");

// Загрузка
SaveController.Load(selectedGuid);

// Удаление
SaveController.Remove(selectedGuid);
```

### Смена драйвера

1. Открыть ассет `DriverConfig` в Inspector (находится в `Resources/`).
2. Найти строку для системы `SaveController`.
3. Выбрать `DriverType` — `PlayerPrefsDriver/SaveSystemDriver` или `FileSystemDriver/FileSystemDriver`.
4. Нажать «Save Config» — `DriversGenericList.cs` перегенерируется.
5. Перезапустить Play или editor домен.

---

## Граничные случаи

### Общие

| Ситуация | Поведение |
|----------|-----------|
| Активный драйвер не задан в `DriverConfig` | Whitelist пустой, ни один драйвер не пройдёт `SetDriver`; `SaveController` без драйвера |
| Дубликат GUID при `Save` | PlayerPrefsDriver: `Saves.Add` обёрнут в try/catch — исключение логируется (`Debug.LogException`) и не пробрасывается; FileSystemDriver: `Saves[guid] = summary` перезапишет, файл будет перезаписан |
| Повреждённый XML при десериализации | `SavePreset = null`, `LogError` |
| `UISaveLoadComponent` выключен во время процесса | `OnDisable` → `StopAllCoroutines` |

### PlayerPrefsDriver

| Ситуация | Поведение |
|----------|-----------|
| GUID не найден в `PlayerPrefs` при `Load` | `LogError`, `_saveDataIndex` остаётся пустым |
| GUID не найден при `Remove` | `LogError`, no-op |
| `PlayerPrefs` переполнен | Поведение зависит от платформы |
| `SavesData` пуст при `Init` | Пустой `Saves`, корректное поведение |

### FileSystemDriver

| Ситуация | Поведение |
|----------|-----------|
| Папка `Saves/` не существует при `Save` | Создаётся автоматически через `FileBus.CreateFolders` |
| Файл `{guid}.save` не существует при `Load` | `LogError`, индекс не меняется |
| Файл `.in` не существует при `GetNumberLastSave` | Создаётся с содержимым `0`, возвращает `0` |
| Файл `{guid}.save` повреждён при `Load` | Decompress/XML-парсер бросит исключение, обработается catch с `LogError` |
| `Remove` для отсутствующего GUID | `LogError`, no-op |
| Дисковая ошибка записи (`Save`) | `LogError` через `Debug.LogException`, состояние `Saves` не обновляется |
| Смена `savesFolder` | Старые сейвы остаются в прежней папке и в списке не появляются: переноса нет |

### Драйверы глобального хранилища

| Ситуация | Поведение |
|----------|-----------|
| Смена `globalSaveFolder` | Старый файл не читается — для хранилища это первый запуск (значения по умолчанию); переноса нет |
| Папки нет при записи | Создаётся через `FileBus.CreateFolders` |
| Ошибка ввода-вывода при чтении | `GlobalReadStatus.Error` → ветка «нечитаемо» контроллера |
| Ошибка записи | `LogError` драйвера, `false` → контроллер логирует и повторит запись при следующей фиксации |
| Переполнение `PlayerPrefs` (GlobalPrefsDriver) | Зависит от платформы |

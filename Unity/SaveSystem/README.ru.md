# SaveSystem (Unity)

**Namespace:** `Vortex.Unity.SaveSystem.Drivers.PlayerPrefsDriver`, `Vortex.Unity.SaveSystem.Drivers.FileSystemDriver`, `Vortex.Unity.SaveSystem.Presets`, `Vortex.Unity.SaveSystem.View`
**Сборка:** `ru.vortex.unity.save`
**Платформа:** Unity 2021.3+

---

## Назначение

Unity-слой системы сохранений. Предоставляет два сменных драйвера хранения с XML-сериализацией и сжатием, а также UI-компонент индикации прогресса. Активный драйвер выбирается через `DriverConfig` (codegen-whitelist).

Возможности:

- `PlayerPrefsDriver/SaveSystemDriver` — драйвер на `PlayerPrefs`
- `FileSystemDriver/FileSystemDriver` — драйвер на файловую систему (`FileBus.GetAppPath()/Saves/`)
- `SavePreset` — XML-сериализуемая обёртка для `SaveFolder[]` (общая для обоих драйверов)
- `UISaveLoadComponent` — MonoBehaviour для отображения прогресса save/load
- Каждый драйвер хранит индекс сохранений и метаданные (`SaveSummary`) в своём формате

Вне ответственности:

- `SaveController`, `ISaveable`, модели данных — Core
- Логика сбора/раздачи данных — Core
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
| `Vortex.Unity.FileSystem` | `FileBus.GetAppPath()`, `FileBus.CreateFolders()` (в `FileSystemDriver`) |
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
│   └── FileSystemDriver/
│       ├── FileSystemDriver.cs                — каркас, поля
│       ├── FileSystemDriver.Run.cs            — bootstrap, Init
│       ├── FileSystemDriver.Save.cs           — Save + BuildSavePreset
│       ├── FileSystemDriver.Load.cs           — Load, Remove
│       ├── FileSystemDriver.Index.cs          — GetIndex, GetNumberLastSave, ScanIndex
│       ├── FileSystemDriver.Paths.cs          — пути и имена файлов
│       ├── FileSystemDriver.Serialization.cs  — XML serialize/deserialize, Compress
│       └── Editor/FileSystemDriverExtEditor.cs — [InitializeOnLoadMethod]
├── Presets/SavePreset.cs                      — общий XML-контейнер
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

Хранит сейвы как файлы на диске. Корневой путь — `FileBus.GetAppPath()/Saves/`. Подходит для больших сейвов и read/write операций без ограничений PlayerPrefs.

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

---

## Сжатие

Оба драйвера сжимают тело сейва через `string.Compress(guid)` и распаковывают через `string.Decompress(guid)`. GUID используется как ключ сжатия. Метаданные (`SaveSummary`) и инкремент-файл (`.in`) **не сжимаются**.

---

## Контракт

### Вход

- Драйверы регистрируются автоматически через `[RuntimeInitializeOnLoadMethod]`
- Активный выбирается через `DriverConfig` → `DriversGenericList.WhiteList`
- `SaveController.Save/Load/Remove` делегируют активному драйверу

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

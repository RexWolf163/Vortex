# SaveLoad

**Namespace:** `Vortex.Sdk.UIs.SaveLoad`
**Assembly:** `ru.vortex.sdk.game.uis.saveload`

## Активация

Пакет подключается через `SdkSettingsSystem`:

- Тоггл: `saveLoadWrapperSdk` в инспекторе ассета `SdkSettings`
- Define-символ: `USING_VORTEX_SAVE_LOAD_WRAPPER`
- Меню: `Tools → Vortex → Configs → SDK Settings`

При выключенном тоггле define снимается из PlayerSettings, и пакет не компилируется (asmdef содержит `defineConstraints: ["USING_VORTEX_SAVE_LOAD_WRAPPER"]`). Канон активации — `Vortex/Sdk/SdkSettingsSystem/README.ru.md`.

## Назначение

UI-обёртка над `SaveController` (Core SaveSystem): представления списка сейвов, отдельного слота, всплывашка завершения сохранения, а также хэндлеры кнопок Save/Load/Remove. Дополнительно — захват скриншота сцены через многослойный композитинг камер и канвасов, превью и тип сейва как метаблоки сводки (`SaveSummary.Meta`).

Возможности:
- Список сейвов (`SaveListView`) с пулом слотов и асинхронным восстановлением превью
- Фильтры списка по метаданным сводки: ключ метаблока + регулярное выражение для значения; задаются в инспекторе, из кода (`SetFilter`/`ClearFilters`/`Refresh`) или хэндлером `SaveListFilterHandler`
- Слот-карточка (`SaveView`) с подсветкой фокуса, шаблоном имени `auto_N` / `manual_N` и локализованным текстом
- Кнопки Save/Load/Remove как `MonoBehaviour`-хэндлеры на `UIComponent`
- Многослойный захват экрана (`CameraCaptureHandler`) с src_over блендингом через `Hidden/AlphaBlit`
- Превью сейва — метаблок `preview` (`SavePreviewController`): снимается сам на начале каждого сохранения, JPEG Medium в base64. Старая форма — превью в имени через `||` — устарела и читается только для старых сейвов
- Тип сейва — метаблок `kind` (`SaveKindController`): `auto` / `manual`, точка сохранения передаёт его в `SaveKindController.Save`. Для старых сейвов тип берётся из префикса имени
- Префиксы имени и значения типа: `auto`, `manual` (`SavingSystemConstants`)

Вне ответственности:
- Хранение и сериализация сейвов (Core `SaveController`)
- Структура самого `SaveSummary` (Core `SaveSystem`)
- Сама модель UI-кнопок и пула (`Vortex.Unity.UI.UIComponents`, `PoolSystem`)

## Зависимости

### Core
- `Vortex.Core.SaveSystem.Bus` — `SaveController` (`Save`, `Load`, `Remove`, `GetIndex`, `AddMeta`, `OnSaveStart`, `OnSaveComplete`, `OnLoadComplete`, `OnRemove`, `GetNumberLastSave`)
- `Vortex.Core.SaveSystem.Abstraction` — `SaveSummary` (`GetMeta`, `SetMeta`)
- `Vortex.Core.System.Abstractions` — `IDataStorage`
- `Vortex.Core.Extensions.LogicExtensions` — `Base64ToTexture`, `TextureToBase64`, `TextureEncodingRules`
- `Vortex.Core.Extensions.ReactiveValues` — `StringData`
- `Vortex.Core.Extensions.DefaultEnums` — `SwitcherState`
- `Vortex.Core.LocalizationSystem` — `Translate`

### Unity
- `Vortex.Unity.UI.UIComponents` — `UIComponent` (`SetAction`, `SetText`, `SetSwitcher`)
- `Vortex.Unity.UI.PoolSystem` — `Pool`
- `Vortex.Unity.UI.Misc` — `DataStorage`
- `Vortex.Unity.UI.TweenerSystem` — `TweenerHub`
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController.Call`/`RemoveCall`
- `Vortex.Unity.LocalizationSystem` — `[LocalizationKey]`
- `Vortex.Unity.EditorTools.Attributes` — `[ClassFilter]`, `[AutoLink]`

### Sdk
- `Vortex.Sdk.SdkSettingsSystem` — partial `SdkSettings` (тоггл активации)

### External
- UniTask — `UniTask`, `UniTask.WaitForEndOfFrame`, `Forget`
- Sirenix Odin Inspector — `[Button]`
- Шейдер `Hidden/AlphaBlit` (Always Included Shaders)

## Использование

### Сцена со списком сейвов

1. На корне UI разместить `SaveListView` со ссылками на `Pool` (префаб слота со `SaveView`) и `DataStorage` для текущего фокуса.
2. На префабе слота: `SaveView` с `[AutoLink]` на источник `IDataStorage`, ссылками на `slotButton` (`UIComponent`), `slotName`, `timestamp`, `RawImage slotImage` и ключи локализации `autoSavePattern` / `manualSavePattern`.
3. Кнопки `Load`/`Remove`: `LoadGameHandler` / `RemoveSaveGameHandler` с тем же `DataStorage`.

### Фильтры списка по метаданным

Фильтр — ключ метаблока сводки и регулярное выражение для его значения. Значение сверяется с выражением целиком (якоря `^…$` добавляются сами, регистр учитывается). В списке остаются сейвы, прошедшие **все** фильтры; без фильтров — все сейвы.

Отсутствующий метаблок считается пустой строкой — так одно выражение покрывает и сейвы, записанные до появления метаданных:

| key | pattern | В списке |
|-----|---------|----------|
| `gameMode` | `Main\|` | основная игра и старые сейвы без маркера |
| `gameMode` | `Dlc1` | только DLC1 |
| `gameMode` | `Dlc.*` | любые DLC |
| `kind` | `.+` | только сейвы с записанным типом |
| `gameMode` | `.*` | все — фильтр ничего не отсекает |

Для `kind` пустое значение у старого сейва значит «тип не записан», а не «ручной»: `manual|` пропустит и старые автосейвы. Тип старого сейва различим только по префиксу имени (`SaveKindController.GetKind`), фильтр же смотрит в метаданные.

Три способа задать фильтры:

1. **Инспектор `SaveListView`** — массив `filters`.
2. **Хэндлер `SaveListFilterHandler`** — ссылка на `SaveListView`, массив `{key, pattern}` и флаг `clearFilters` (по умолчанию включён: сначала снимает фильтры списка, в т.ч. из его инспектора). На включении выставляет фильтры и перестраивает список. Порядок включения со списком не важен: включённый список перестроится, выключенный соберётся при включении уже с фильтрами.
3. **Из кода:**

```csharp
saveList.ClearFilters();
saveList.SetFilter(GameModeSaveMeta.MetaKey, "Main|");    // прежние фильтры с этим ключом заменяются одним
saveList.SetFilter(SaveKindController.MetaKey, "manual|");
saveList.Refresh();                                        // SetFilter/ClearFilters список не перестраивают
```

Фильтры, заданные из кода или хэндлером, меняют экземпляр в рантайме и в префаб не попадают.

### Кнопка «Сохранить»

```csharp
// На GameObject с UIComponent (кнопкой) добавить SaveGameHandler.
// Имя сейва: manual_N; в сводке — метаблоки preview (снимок экрана) и kind = manual.
```

### Своя точка сохранения

```csharp
// Конец кадра обязателен: превью снимается на начале сохранения (SaveController.OnSaveStart)
await UniTask.WaitForEndOfFrame(this);

// С типом — метаблок kind будет записан
var guid = await SaveKindController.Save(SavingSystemConstants.AutoName, $"{SavingSystemConstants.AutoName}_{day}");

// В обход SaveKindController — сейв без типа (превью и прочие метаблоки пишутся как обычно)
var guid2 = await SaveController.Save("custom");
```

### Захват экрана

```csharp
// На каждой камере / канвасе разместить CameraCaptureHandler,
// проставить priority (меньше — ниже).
// В Always Included Shaders добавить Hidden/AlphaBlit.

await UniTask.WaitForEndOfFrame(this);
var screenshot = CameraCaptureHandler.Capture();
// ... использовать ...
UnityEngine.Object.Destroy(screenshot);
```

### Превью и тип существующего сейва

```csharp
SaveSummary summary = ...;
Texture2D preview = SavePreviewController.GetPreview(summary); // метаблок preview, у старых сейвов — из имени; либо null
string kind = SaveKindController.GetKind(summary);             // метаблок kind, у старых сейвов — префикс имени; либо null
string pureName = summary.GetSavePureName();                   // у старых сейвов отрезает превью
```

Метаблоки `preview` и `kind` выключаются в `SaveSettings` (Unity SaveSystem, блок «Метаданные сейва»). Выключенное превью `SavePreviewController` всё равно снимает — сборка SDK не видит настроек, блок отбрасывает драйвер.

## Архитектура

```
SaveLoad/
├── SavePreviewController.cs        ← метаблок preview: снимок на начале сохранения, чтение (с запасным путём из имени)
├── SaveKindController.cs           ← метаблок kind: Save(kind, name), GetKind (с запасным путём из имени)
├── SavingSystemConstants.cs        ← префиксы и типы AutoName/ManualName
├── AlphaBlit.shader                ← Hidden/AlphaBlit (src_over)
├── DefineSettings/
│   ├── SdkSettings.SaveLoad.cs     ← partial-кусок SdkSettings (тоггл)
│   └── sdk.settings.system.ext.asmref
├── Handlers/
│   ├── CameraCaptureHandler.cs     ← многослойный захват экрана
│   ├── LoadGameHandler.cs          ← кнопка загрузки
│   ├── RemoveSaveGameHandler.cs    ← кнопка удаления
│   ├── SaveListFilterHandler.cs    ← фильтры списка сейвов на включении
│   └── SaveGameHandler.cs          ← кнопка сохранения (manual)
├── Models/
│   └── SaveSlotData.cs             ← враппер SaveSummary + lazy Texture2D, IDisposable
├── Views/
│   ├── SaveCompleteView.cs         ← всплывашка о новом сейве
│   ├── SaveListView.cs             ← список сейвов на Pool + фильтры по метаданным
│   └── SaveView.cs                 ← карточка одного слота
└── ru.vortex.sdk.game.uis.saveload.asmdef
```

## Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `SavePreviewController` | `static class` | Метаблок `MetaKey = "preview"`. Подписан на `SaveController.OnSaveStart`: `CameraCaptureHandler.Capture()` → `EncodePreview` (JPEG Medium, base64) → `SaveController.AddMeta`, текстура уничтожается. `GetPreview(SaveSummary)` — из метаданных, иначе из имени по `\|\|`; `GetSavePureName(this SaveSummary)`. `GetSaveNameWithPreview(Texture2D, string)` — `[Obsolete]`, старая форма |
| `SaveKindController` | `static class` | Метаблок `MetaKey = "kind"`. `Save(kind, name, guid?)` держит тип на время цикла и пишет его на `OnSaveStart`; `GetKind(SaveSummary)` — из метаданных, иначе из префикса имени (`auto`/`manual`), иначе `null` |
| `SavingSystemConstants` | `static class` | Константы префиксов имени и значений типа: `AutoName = "auto"`, `ManualName = "manual"` |
| `SaveSlotData` | `class : IDisposable` | Враппер `SaveSummary` для `IDataStorage`. `Guid`, `Summary`, lazy `Preview` (Texture2D); `Dispose` уничтожает текстуру |
| `CameraCaptureHandler` | `MonoBehaviour` | Один слой захвата (Camera или Canvas). Самореестр `Handlers`, `priority` (0–10), `Render(RT)`, статический `Capture()` |
| `SaveGameHandler` | `MonoBehaviour` | Привязка кнопки `UIComponent` к `Save()`. Ждёт `WaitForEndOfFrame` (для снимка превью), вызывает `SaveKindController.Save(ManualName, "manual_N")` |
| `LoadGameHandler` | `MonoBehaviour` | Берёт `SaveSlotData` из `IDataStorage`, вызывает `SaveController.Load(guid)` |
| `RemoveSaveGameHandler` | `MonoBehaviour` | Подписан на `IDataStorage.OnUpdateLink`, вызывает `SaveController.Remove(guid)` |
| `SaveListView` | `MonoBehaviour` | Заполняет `Pool` слотами в порядке убывания `UnixTimestamp`, пропуская сейвы, не прошедшие фильтры `filters` (метаблок + регулярное выражение, AND). Хранит `StringData _focused` и кладёт текущий `SaveSlotData` в `DataStorage`. Перезаполнение по `OnSaveComplete`/`OnLoadComplete`/`OnRemove`. Публичные `SetFilter(key, pattern)`, `ClearFilters()`, `Refresh()`. Disposable через `TimeController.Call` |
| `SaveListFilterHandler` | `MonoBehaviour` | На `OnEnable` выставляет фильтры `{key, pattern}` на пролинкованный `SaveListView` (с предварительным `ClearFilters`, если отмечен `clearFilters`) и вызывает `Refresh()` |
| `SaveView` | `MonoBehaviour` | Карточка слота. Подписан на `_focused.OnUpdateData`, переключает `SwitcherState.On/Off`. Парсит имя `auto_N` / `manual_N` и форматирует через `LocalizationKey` |
| `SaveCompleteView` | `MonoBehaviour` | Всплывашка по `SaveController.OnSaveComplete`. Прогоняет массив `TweenerHub` (Back→Forward), выводит имя последнего сейва и `Date` |
| `SdkSettings` (partial) | — | Поле `saveLoadWrapperSdk` с `[ToggleButton]` и `[DefineSymbol("USING_VORTEX_SAVE_LOAD_WRAPPER")]` |

## Контракт

### Вход
- Слой захвата: `CameraCaptureHandler` с заданными `camera` или `canvas` и `priority`
- UI-кнопки сохранения/загрузки/удаления: `UIComponent` через `SetAction`
- Список сейвов берётся из `SaveController.GetIndex()` (`IDictionary<string, SaveSummary>`)
- Передача выбранного слота между View и хэндлерами — через `IDataStorage` (`DataStorage`-MonoBehaviour) с моделью `SaveSlotData`

### Выход
- Сохранение: `SaveKindController.Save(kind, name)` → `SaveController.Save(name)`; в сводку — метаблоки `preview` и `kind` через `SaveController.AddMeta`
- Загрузка/удаление: `SaveController.Load(guid)` / `SaveController.Remove(guid)`
- Превью слота: `RawImage.texture = SaveSlotData.Preview`

### Гарантии
- `SaveListView` фильтрует индекс до сортировки и сортирует по убыванию `UnixTimestamp`; в фокус ставится самый свежий из прошедших фильтры
- `SaveListView.Refresh()` на выключенном представлении ничего не делает — список соберётся при включении
- Выражение фильтра компилируется один раз и пересобирается только при смене текста
- `SaveSlotData.Preview` создаётся лениво, `Dispose` уничтожает `Texture2D`
- `SaveListView.Dispose` отписывается от событий, отменяет CTS, чистит пул и слоты
- `CameraCaptureHandler.Capture()` сортирует активные хэндлеры по `priority` (меньше — ниже), композитит через src_over
- `SaveGameHandler` ждёт `WaitForEndOfFrame` перед сохранением — снимок превью на `OnSaveStart` идёт в консистентном пайплайне при вызове из EventSystem
- Тип сейва живёт только в своём цикле: `SaveKindController.Save` сбрасывает его по завершении, в т.ч. когда сохранение отклонено замком
- Старые сейвы (превью и тип в имени) читаются через запасной путь `GetPreview` / `GetKind`
- `RemoveSaveGameHandler` пере-инициализируется на `IDataStorage.OnUpdateLink`

### Ограничения
- Шейдер `Hidden/AlphaBlit` обязан быть в Always Included Shaders (Project Settings → Graphics)
- Один `CameraCaptureHandler` несёт ровно одну сущность: `camera` или `canvas`. При обоих null — `LogWarning`
- Для `Canvas.ScreenSpaceCamera` без `worldCamera` — `LogWarning`, слой пропускается
- Имя сейва не должно содержать подстроки `||`: `GetSavePureName` и запасной путь `GetPreview` режут по ней имя
- Точка сохранения обязана дождаться конца кадра перед `Save`: превью снимается синхронно на `OnSaveStart`
- Возвращённую `Capture()` текстуру обязан уничтожить вызывающий код (`Destroy`)

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Пустой индекс сейвов | `SaveListView` чистит пул, фокус не выставляется |
| Ни один сейв не прошёл фильтры | Как при пустом индексе |
| Невалидное регулярное выражение в фильтре | Ошибка в лог (ключ и текст выражения), фильтр не пропускает ни одного сейва |
| Несколько фильтров с одним ключом (в инспекторе) | Применяются все (AND); `SetFilter` по этому ключу оставит один |
| `SaveListFilterHandler` без ссылки на `SaveListView` | Ошибка в лог, фильтры не выставляются |
| Новый сейв (превью в метаданных) | `GetPreview` берёт метаблок `preview`, имя чистое |
| Старый сейв: `Name` с `||` | `GetPreview` берёт base64 после `||`, `GetSavePureName` отрезает его |
| Нет ни метаблока, ни `||` в имени | `GetPreview` возвращает `null`, `GetSavePureName` возвращает имя как есть |
| Сохранение без активных `CameraCaptureHandler` | Сейв пишется без метаблока `preview` |
| Сохранение в обход `SaveKindController` | Метаблока `kind` нет; `GetKind` пробует префикс имени |
| `GetKind` у сейва с произвольным именем без метаблока | `null` |
| Имя слота не подходит под шаблон `auto_N` / `manual_N` | Выводится сырое `saveName` без локализации |
| `IDataStorage` без `SaveSlotData` | `LoadGameHandler` пишет `LogError` и выходит; `RemoveSaveGameHandler` отвязывает кнопку |
| `Capture()` без активных хэндлеров | `LogWarning`, возврат `null` |
| Изменение размера экрана между кадрами | `CameraCaptureHandler.Render` пересоздаёт `RenderTexture` |
| `Canvas.ScreenSpaceOverlay` | Захват через временную ортографическую камеру с `cullingMask = 1 << layer`, режим/`worldCamera`/`planeDistance` восстанавливаются |
| Disable `SaveListView` | `Dispose` запланирован через `TimeController.Call`, отписки выполняются вне `OnDisable`-кадра |

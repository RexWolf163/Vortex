# SaveLoad

**Namespace:** `Vortex.Sdk.UIs.SaveLoad`
**Assembly:** `ru.vortex.sdk.game.uis.saveload`

## Активация

Пакет подключается через `SdkSettingsSystem`:

- Тоггл: `saveLoadWrapperSdk` в инспекторе ассета `SdkSettings`
- Define-символ: `USING_VORTEX_SAVE_LOAD_WRAPPER`
- Меню: `Vortex → Configs → SDK Settings`

При выключенном тоггле define снимается из PlayerSettings, и пакет не компилируется (asmdef содержит `defineConstraints: ["USING_VORTEX_SAVE_LOAD_WRAPPER"]`). Канон активации — `Vortex/Sdk/SdkSettingsSystem/README.ru.md`.

## Назначение

UI-обёртка над `SaveController` (Core SaveSystem): представления списка сейвов, отдельного слота, всплывашка завершения сохранения, а также хэндлеры кнопок Save/Load/Remove. Дополнительно — захват скриншота сцены через многослойный композитинг камер и канвасов и упаковка превью в имя сейва base64-блобом.

Возможности:
- Список сейвов (`SaveListView`) с пулом слотов и асинхронным восстановлением превью
- Слот-карточка (`SaveView`) с подсветкой фокуса, шаблоном имени `auto_N` / `manual_N` и локализованным текстом
- Кнопки Save/Load/Remove как `MonoBehaviour`-хэндлеры на `UIComponent`
- Многослойный захват экрана (`CameraCaptureHandler`) с src_over блендингом через `Hidden/AlphaBlit`
- Кодирование превью в имя сейва (`SavePreviewController`, разделитель `||`, формат JPEG Medium)
- Префиксы имени сейва: `auto`, `manual` (`SavingSystemConstants`)

Вне ответственности:
- Хранение и сериализация сейвов (Core `SaveController`)
- Структура самого `SaveSummary` (Core `SaveSystem`)
- Сама модель UI-кнопок и пула (`Vortex.Unity.UI.UIComponents`, `PoolSystem`)

## Зависимости

### Core
- `Vortex.Core.SaveSystem.Bus` — `SaveController` (`Save`, `Load`, `Remove`, `GetIndex`, `OnSaveComplete`, `OnLoadComplete`, `OnRemove`, `GetNumberLastSave`)
- `Vortex.Core.SaveSystem.Abstraction` — `SaveSummary`
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

## Архитектура

```
SaveLoad/
├── SavePreviewController.cs        ← упаковка/распаковка превью в имя сейва (base64)
├── SavingSystemConstants.cs        ← префиксы AutoName/ManualName
├── AlphaBlit.shader                ← Hidden/AlphaBlit (src_over)
├── DefineSettings/
│   ├── SdkSettings.SaveLoad.cs     ← partial-кусок SdkSettings (тоггл)
│   └── sdk.settings.system.ext.asmref
├── Handlers/
│   ├── CameraCaptureHandler.cs     ← многослойный захват экрана
│   ├── LoadGameHandler.cs          ← кнопка загрузки
│   ├── RemoveSaveGameHandler.cs    ← кнопка удаления
│   └── SaveGameHandler.cs          ← кнопка сохранения + захват превью
├── Models/
│   └── SaveSlotData.cs             ← враппер SaveSummary + lazy Texture2D, IDisposable
├── Views/
│   ├── SaveCompleteView.cs         ← всплывашка о новом сейве
│   ├── SaveListView.cs             ← список сейвов на Pool
│   └── SaveView.cs                 ← карточка одного слота
└── ru.vortex.sdk.game.uis.saveload.asmdef
```

## Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `SavePreviewController` | `static class` | `GetPreview(SaveSummary)`, `GetSavePureName(this SaveSummary)`, `GetSaveNameWithPreview(Texture2D, string)`. Разделитель `||`, JPEG Medium |
| `SavingSystemConstants` | `static class` | Константы префиксов: `AutoName = "auto"`, `ManualName = "manual"` |
| `SaveSlotData` | `class : IDisposable` | Враппер `SaveSummary` для `IDataStorage`. `Guid`, `Summary`, lazy `Preview` (Texture2D); `Dispose` уничтожает текстуру |
| `CameraCaptureHandler` | `MonoBehaviour` | Один слой захвата (Camera или Canvas). Самореестр `Handlers`, `priority` (0–10), `Render(RT)`, статический `Capture()` |
| `SaveGameHandler` | `MonoBehaviour` | Привязка кнопки `UIComponent` к `Save()`. Ждёт `WaitForEndOfFrame`, делает `CameraCaptureHandler.Capture()`, формирует имя `manual_N` с превью, вызывает `SaveController.Save(name)` |
| `LoadGameHandler` | `MonoBehaviour` | Берёт `SaveSlotData` из `IDataStorage`, вызывает `SaveController.Load(guid)` |
| `RemoveSaveGameHandler` | `MonoBehaviour` | Подписан на `IDataStorage.OnUpdateLink`, вызывает `SaveController.Remove(guid)` |
| `SaveListView` | `MonoBehaviour` | Заполняет `Pool` слотами в порядке убывания `UnixTimestamp`. Хранит `StringData _focused` и кладёт текущий `SaveSlotData` в `DataStorage`. Перезаполнение по `OnSaveComplete`/`OnLoadComplete`/`OnRemove`. Disposable через `TimeController.Call` |
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
- Сохранение: `SaveController.Save(name)` где имя содержит base64-превью после `||`
- Загрузка/удаление: `SaveController.Load(guid)` / `SaveController.Remove(guid)`
- Превью слота: `RawImage.texture = SaveSlotData.Preview`

### Гарантии
- `SaveListView` сортирует индекс по убыванию `UnixTimestamp`, первый ставится в фокус
- `SaveSlotData.Preview` создаётся лениво, `Dispose` уничтожает `Texture2D`
- `SaveListView.Dispose` отписывается от событий, отменяет CTS, чистит пул и слоты
- `CameraCaptureHandler.Capture()` сортирует активные хэндлеры по `priority` (меньше — ниже), композитит через src_over
- `SaveGameHandler` ждёт `WaitForEndOfFrame` перед захватом — пайплайн консистентен при вызове из EventSystem
- `RemoveSaveGameHandler` пере-инициализируется на `IDataStorage.OnUpdateLink`

### Ограничения
- Шейдер `Hidden/AlphaBlit` обязан быть в Always Included Shaders (Project Settings → Graphics)
- Один `CameraCaptureHandler` несёт ровно одну сущность: `camera` или `canvas`. При обоих null — `LogWarning`
- Для `Canvas.ScreenSpaceCamera` без `worldCamera` — `LogWarning`, слой пропускается
- Имя сейва не должно содержать подстроки `||` (она вырезается через `Replace`)
- Возвращённую `Capture()` текстуру обязан уничтожить вызывающий код (`Destroy`)

## Использование

### Сцена со списком сейвов

1. На корне UI разместить `SaveListView` со ссылками на `Pool` (префаб слота со `SaveView`) и `DataStorage` для текущего фокуса.
2. На префабе слота: `SaveView` с `[AutoLink]` на источник `IDataStorage`, ссылками на `slotButton` (`UIComponent`), `slotName`, `timestamp`, `RawImage slotImage` и ключи локализации `autoSavePattern` / `manualSavePattern`.
3. Кнопки `Load`/`Remove`: `LoadGameHandler` / `RemoveSaveGameHandler` с тем же `DataStorage`.

### Кнопка «Сохранить»

```csharp
// На GameObject с UIComponent (кнопкой) добавить SaveGameHandler.
// Имя сейва будет вида:  manual_N || <base64 jpeg>
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

### Извлечение превью из существующего сейва

```csharp
SaveSummary summary = ...;
Texture2D preview = SavePreviewController.GetPreview(summary); // либо null
string pureName = summary.GetSavePureName();
```

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Пустой индекс сейвов | `SaveListView` чистит пул, фокус не выставляется |
| `SaveSummary.Name` без `||` | `GetPreview` возвращает `null`, `GetSavePureName` возвращает имя как есть |
| Имя слота не подходит под шаблон `auto_N` / `manual_N` | Выводится сырое `saveName` без локализации |
| `IDataStorage` без `SaveSlotData` | `LoadGameHandler` пишет `LogError` и выходит; `RemoveSaveGameHandler` отвязывает кнопку |
| `Capture()` без активных хэндлеров | `LogWarning`, возврат `null` |
| Изменение размера экрана между кадрами | `CameraCaptureHandler.Render` пересоздаёт `RenderTexture` |
| `Canvas.ScreenSpaceOverlay` | Захват через временную ортографическую камеру с `cullingMask = 1 << layer`, режим/`worldCamera`/`planeDistance` восстанавливаются |
| Disable `SaveListView` | `Dispose` запланирован через `TimeController.Call`, отписки выполняются вне `OnDisable`-кадра |

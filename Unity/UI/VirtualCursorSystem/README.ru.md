# VirtualCursorSystem

**Namespace:** `Vortex.Unity.UI.VirtualCursorSystem`
**Сборка:** `ru.vortex.unity.virtualcursorsystem`

---

## Назначение

Виртуальный курсор с мульти-источником для UGUI-проектов. Единая экранная позиция (`ScreenPosition`) кормится любым источником — мышь, геймпад, клавиши, тач — и является единственным источником истины. Позиция **расцеплена от ОС-мыши**: живёт в модели, а не в Mouse-девайсе, поэтому нет warp-хаков и рассинхрона двух устройств.

Родной UGUI (`Button`, `Toggle`, `ScrollRect`, drag, hover, `IPointerXxx`) работает без кода на каждый виджет: пакет предъявляет `InputSystemUIInputModule` виртуальное pointer-устройство (`VirtualUiPointer`), которое ведётся за `ScreenPosition`/действиями.

Внешний вид курсора — **render-агностичная система скинов**: сменные наборы-темы (по ключу в рантайме), масштаб от разрешения (глобальные тиры), спрайт по состоянию действий, с фолбэком вверх. Рендер — через `ICursorRenderer` (по умолчанию UGUI-`Image` в позиции курсора; опционально ОС-курсор через `Cursor.SetCursor`).

**Ввод — подключаемый модуль.** Источники реализованы как драйверы (`InputDriver`), которые перечисляются в настроечном ассете `InputDriverSet` и подключаются на старте контроллером-загрузчиком `CursorInputLoader`. Сам модуль ввода включается тогглом в `SdkSettings` (`USING_VORTEX_CURSOR`). Курсор — **надсистемная сущность**: ситуативного гейта ввода нет, подключённый драйвер активен всегда.

**Противопоставление `CursorSystem`:** `CursorSystem` — ОС-курсор + UGUI-hover, mouse-only, упрощённая альтернатива. `VirtualCursorSystem` — виртуальный курсор + арбитраж источников + render-агностичные сменные скины + подключаемый драйверный слой ввода.

Вне ответственности:
- Игровые триггеры/механики клика — уровень потребителя (`AdvancedButton`/игровой код); пакет отдаёт позицию/действия/проекцию.
- Персист выбранной темы — проектный слой (L2 не зависит от L3-GameCore; см. «Выбор темы»).
- Интерпретация мирового хита — пакет отдаёт сырой `RaycastHit`.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Unity.InputSystem` | `InputAction`, `Mouse`/`MouseState`, `InputState`, кастомный девайс |
| `UnityEngine.UI` | `Image`/`Canvas` (UI-рендер), `IPointerEnter/Exit`, `EventSystem` (IsOverUI/hover) |
| `Vortex.Unity.InputBusSystem` | `InputController` — резолв экшенов по строковому id «Карта/Экшен», карты/подписка (LIFO) |
| `Vortex.Core.LoaderSystem` (apploader) | `IProcess`/`Loader` — подключение драйверов в пайплайне загрузки |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` — авто-провижен ассета `InputDriverSet` в `Resources/Settings` |
| `Vortex.Unity.AppSystem` | `TimeController.Accumulate` — покадровый тик драйверов |
| `Vortex.Sdk.SdkSettingsSystem` | тоггл модуля + `DefineSymbol("USING_VORTEX_CURSOR")` |
| `Vortex.Core.Extensions.ReactiveValues` | `ReactiveValue<T>`, `EnumData`/`StringData`/`BoolData` с owner-защитой |
| `Vortex.Unity.Extensions.ReactiveValues` | `Vector2Data` |
| `Vortex.Unity.EditorTools` | `[AutoLink]`, `[ClassLabel]`, `[ValueSelector]` (дропдаун id экшенов) |
| Sirenix Odin Inspector | `[Tooltip]`, `[SerializeReference]`/`[HideReferenceObjectPicker]`, `[ToggleButton]` |

Драйверы ввода **не** используют `InputActionProperty`: биндинг задаётся строковым id экшена (Vortex-стандарт, как в `InputController`) и резолвится в рантайме.

Персист темы (`IGameData`) реализуется на **проектном слое** (пример: `_SexMusicIdol/_Scripts/UI/CursorSkinPersistence.cs`) — пакет save-агностичен.

---

## Архитектура

```
[CursorSkinSettings] (SO)                       ← конфиг: глобальные тиры + каталог тем
 ├─ int[] resolutionTiers                        (брейкпоинты по Screen.height, ВОЗР.)
 ├─ string defaultSetKey
 └─ CursorSkinSet[] sets                          (тема = ключ + паки по тирам)
      └─ CursorSkinPack[] tiers
           ├─ CursorSkin baseSkin                 (вне hover)
           └─ CursorSkin[] hoverSkins             (по строковому ключу)
                └─ CursorSkin { name, hideCursor, defaultSprite, CursorSpriteEntry[] overrides }

[PointerModel] (IReactiveData, runtime, НЕ Save)
 ├─ Vector2Data ScreenPosition                    ← истина позиции
 ├─ EnumData<PointerSourceKind> ActiveSource      ← Analog/Point/Direct (last-source-wins)
 ├─ PointerActionMaskData Actions                 ← битовая маска одновременных действий
 ├─ StringData HoverKey                           ← активный hover-скин
 └─ BoolData IsOverUI                             ← над UGUI (из EventSystem)

[VirtualCursorController] (static)
 ├─ Init(settings) / Cleanup() / RefreshResolution()
 ├─ ReportPointer(pos, source[, hidesCursor]) / SetAction / SetHover / SetOverUI   (internal — драйверы)
 ├─ Recompute → CursorSkinResolver → Visual (CursorVisualData); подмешивает скрытие по источнику
 └─ Projection: RegisterCamera(LIFO) + ленивый raycast (TryGetWorldHit/GetWorldProjection)

[VirtualCursorBus] (static)  → Data / Visual / IsReady / OnReady            (read-only фасад)
[CursorSkinSelector] (static) → Selected(StringData) / Select(key)          (save-агностично)

Слой ввода (подключаемый SDK-модуль, #if USING_VORTEX_CURSOR):
  [InputDriverSet] (SO, ICoreAsset)  → [SerializeReference] InputDriver[]      (Resources/Settings)
  [CursorInputLoader] (IProcess)     → Register в Loader · Resources.Load + failfast
                                        · connect по платформе · тик через Accumulate (+анти-спам)
  [InputDriver] (POCO, abstract): Connect/Disconnect · NeedsTick/Tick · HidesCursor · SupportsPlatform
     ├─ MouseInputDriver (Analog)     · TouchInputDriver (Point, HidesCursor=true)
     └─ DirectInputDriver (Direct, NeedsTick) · ActionInputDriver (кнопки→маска)

Сценовые MonoBehaviour (не драйверы ввода):
  CursorHoverZone (UGUI→HoverKey) · CameraProvider (камера проекции, LIFO)

UGUI-мост:  VirtualUiPointer (: Mouse, отдельный layout)  ← UiPointerFeeder (LateUpdate: модель→девайс)
Рендер:     ICursorRenderer → UiImageCursorRenderer (дефолт) | OsCursorRenderer (опц.)
```

### Поток данных

```
Источник (мышь/стик/тач/клавиши)
   → InputDriver (резолв экшена по id через InputController) → VirtualCursorController.ReportPointer/SetAction
        → PointerModel (ScreenPosition/Actions/HoverKey; hidesCursor по источнику)
             ├→ CursorSkinResolver → Visual → ICursorRenderer (рисует курсор)
             ├→ UiPointerFeeder → VirtualUiPointer → InputSystemUIInputModule → родной UGUI
             └→ Projection (по запросу) → RaycastHit
```

---

## Ключевые концепции

### Драйверы ввода как подключаемый модуль
- `InputDriver` — абстрактный **POCO** (не MonoBehaviour): `Connect()`/`Disconnect()`, `NeedsTick`/`Tick(dt)`, `HidesCursor`, `SupportsPlatform(platform)`. Экшены резолвятся по строковому id «Карта/Экшен» через `InputController` (`[ValueSelector]`-дропдаун в инспекторе).
- `InputDriverSet` — SO-список драйверов (`[SerializeReference]`), `ICoreAsset` → авто-создаётся в `Resources/Settings/InputDriverSet.asset`.
- `CursorInputLoader` — `IProcess`: регистрируется в `Loader`, в `RunAsync` грузит сет из `Resources`, подключает драйверы под текущую платформу, заводит покадровый тик. **Failfast**: модуль включён (`USING_VORTEX_CURSOR`), а ассета нет или список пуст → исключение (не тихий отказ).
- Включается тогглом `cursorInputSdk` в `SdkSettings` (дефайн `USING_VORTEX_CURSOR`). Ядро курсора (контроллер/рендер/скины/UGUI-мост) компилируется всегда; подключаемым является именно **слой ввода**.
- **Гейта ввода нет** — курсор надсистемный: драйвер, будучи подключённым, активен всегда (без ситуативного отсечения).

### Арбитраж источников (last-source-wins)
`ReportPointer(pos, source)` делает репортящий источник активным (last-source-wins). `PointerSourceKind`: `Analog` (мышь), `Point` (тач), `Direct` (геймпад/клавиши — интеграция скорость×dt, кламп к экрану). Порог антидребезга у мыши из старой реализации в новые драйверы не перенесён (арбитраж — чистый last-source-wins).

### Скрытие курсора по источнику
Драйвер объявляет `HidesCursor` (у `TouchInputDriver` = true: касание — прямой контакт, курсор не нужен). Флаг прокидывается в `ReportPointer(pos, source, hidesCursor)` и по last-source-wins кладётся в контроллер; `Recompute` подмешивает его поверх резолвера (`Hide = resolved.Hide || pointerHidden`). Смена источника корректно возвращает курсор (мышь → снова виден).

### Тик драйверов (TimeController.Accumulate + анти-спам)
Драйверы с `NeedsTick` (Direct) тикаются самоперепланирующейся петлёй через `TimeController.Accumulate` (без скрытого раннера). Петля обёрнута в `try/catch/finally`: внутренний `catch` изолирует сбойный драйвер, `finally` гарантирует продолжение. Анти-спам: исключение драйвера логируется только на **первое** в серии, счётчик сбрасывается на первом успешном кадре. `Tick` работает на `unscaledDeltaTime` — действует и на паузе (меню).

### Маска действий (одновременность + доминанта)
`PointerAction` — последовательный enum-индекс (`None` + `Action1…Action10`; конвенция: 1=LMB, 2=RMB, 3=MMB, 4=Back, 5=Forward, 6=Scroll↑, 7=Scroll↓, 8–10=запас). `PointerActionMask` — `readonly struct` над `int`: биты = одновременно активные действия, `Dominant()` — младший активный бит по приоритету (для спрайта). `ActionInputDriver` по `started`/`canceled` выставляет/снимает биты; `canceled` при alt-tab снимает их сам.

### Скины: тема → тир → hover → действие, с фолбэком вверх
`CursorSkinResolver.Resolve`:
1. **Тема** — `CursorSkinSelector.Selected` → `CursorSkinSet` (дефолт, если не найдена).
2. **Тир разрешения** — `SelectTierIndex(Screen.height)`: минимальный `resolutionTiers[i] >= height`, иначе крупнейший → `CursorSkinPack`.
3. **Скин** — hover-скин по `HoverKey`, иначе базовый; `HideCursor` → курсор скрыт.
4. **Спрайт** — по `Actions.Dominant()`: `override` скина → его `defaultSprite` → **вверх**: базовый скин пакета → его `defaultSprite`.
5. Hotspot — из `Sprite.pivot`, инверсия по Y.

### Глобальные тиры разрешения
Брейкпоинты (`resolutionTiers`) заданы **один раз** в `CursorSkinSettings`; каждая тема даёт по одному паку на тир (`OnValidate` предупреждает о рассогласовании). Смена разрешения → `VirtualCursorController.RefreshResolution()`.

### Виртуальный UI-указатель и родной UGUI
`VirtualUiPointer` — подкласс `Mouse` с **отдельным layout** (`<VirtualUiPointer>`): `InputSystemUIInputModule` биндится на него и генерит все родные события. `UiPointerFeeder` в `LateUpdate` пишет девайс из модели: `position ← ScreenPosition`, кнопки ← биты маски (Action1→left…Action5→forward), scroll ← Action6/7. Реальная мышь кормит драйвер (Analog); UI-модуль читает только виртуальный указатель.

### IsOverUI
`IsOverUiHandler` пишет `PointerModel.IsOverUI` из `EventSystem.IsPointerOverGameObject()` — потребители мировой проекции гейтят клик по флагу.

### Screen→world проекция
`VirtualCursorController` ведёт LIFO-реестр камер (`CameraProvider`); `TryGetWorldHit`/`GetWorldProjection` — ленивый `Physics.Raycast` с кэшем на кадр. Пакет отдаёт сырой хит.

### Выбор темы (save-агностично)
`CursorSkinSelector` держит реактивный ключ темы (`Selected`) и `Select(key)`. Персист (`IGameData`) — на **проектном слое**. Пакет (L2) не зависит от GameCore (L3).

---

## Контракт

### Вход
- `SdkSettings`: тоггл `cursorInputSdk` включён (дефайн `USING_VORTEX_CURSOR`).
- `InputDriverSet` (SO в `Resources/Settings`): непустой список драйверов с назначенными id экшенов.
- Input Actions: экшены под драйверы (позиция мыши, позиция касания, вектор движения, кнопки Action1…Action10); UI-карта модуля забинжена на `<VirtualUiPointer>`.
- `CursorSkinSettings` (SO) передан в `Init` (через `VirtualCursorBootstrap`).

### Выход
- `PointerModel` (позиция/источник/маска/hover/над-UI) — реактивно.
- `CursorVisual` — текущий вид курсора (спрайт+hotspot+hide) для рендера.
- Виртуальный девайс, ведущий родной UGUI.
- `RaycastHit`/точка проекции по запросу.

### Гарантии
- Одна экранная позиция для рендера, UI и проекции — без рассинхрона.
- Одновременность действий на девайсе; одиночная доминанта для спрайта.
- `canceled`/alt-tab снимает биты действий — залипания нет.
- Тик драйверов переживает сбой одного драйвера (try/catch/finally, лог без спама).
- Владение реактивными полями закреплено за контроллером — извне не пишутся.

### Ограничения
- Слой ввода требует включённого дефайна `USING_VORTEX_CURSOR`; иначе драйверы не компилируются и позицию никто не подаёт.
- `InputDriverSet` обязан существовать и быть непустым — иначе `CursorInputLoader` кидает исключение (failfast).
- UGUI-модуль **должен** биндиться на `<VirtualUiPointer>`, иначе UI не следует за курсором.
- Проекция требует зарегистрированной камеры; без неё — промах.
- `OsCursorRenderer` требует **standalone-текстуру** спрайта (`Cursor.SetCursor` берёт целую `Texture2D`). Для атласных курсоров — `UiImageCursorRenderer`.
- `InputController` (шина ввода) должен быть доступен на момент коннекта — он лениво инициализируется по первому обращению (`GetAction`), явного ожидания в `WaitingFor` не требуется.

---

## API

### VirtualCursorBus (static)
```csharp
static PointerModel     Data;      // runtime-модель
static CursorVisualData Visual;    // текущий вид курсора
static bool             IsReady;
static event Action     OnReady;
```

### VirtualCursorController (static)
```csharp
static void Init(CursorSkinSettings settings);
static void Cleanup();
static void RefreshResolution();
static void ConfigureProjection(LayerMask mask, float distance);
static bool TryGetWorldHit(out RaycastHit hit);
static Vector3? GetWorldProjection();
static void InvalidateProjection();
// intake (internal): ReportPointer(pos,src) / ReportPointer(pos,src,hidesCursor)
//                    / SetAction / ClearActions / SetHover / SetOverUI / Register/UnregisterCamera
```

### CursorSkinSelector (static)
```csharp
static StringData Selected;                 // реактивный ключ темы
static void Select(string setKey);
static bool IsSelected(string setKey);
```

### InputDriver (abstract, POCO)  [#if USING_VORTEX_CURSOR]
```csharp
abstract void Connect();
abstract void Disconnect();
virtual  bool NeedsTick { get; }            // Direct → true
virtual  void Tick(float unscaledDeltaTime);
virtual  bool HidesCursor { get; }          // Point/Touch → true
virtual  bool SupportsPlatform(RuntimePlatform platform);
// helpers: ResolveAction / EnableMap / DisableMap / SubscribeAction / UnsubscribeAction / Report
```

### InputDriverSet (SO, ICoreAsset) / CursorInputLoader (IProcess)  [#if USING_VORTEX_CURSOR]
```csharp
InputDriver[] InputDriverSet.Drivers;       // Resources/Settings/InputDriverSet.asset
// CursorInputLoader: Register→Loader, RunAsync(load+failfast+connect+tick), WaitingFor()=пусто
```

---

## Использование

### 1. Включить модуль ввода
В ассете `SdkSettings` включить `cursorInputSdk` → **ApplyChanges** (добавит дефайн `USING_VORTEX_CURSOR`, пересборка).

### 2. Настроить InputDriverSet
`CoreAssetsController` авто-создаст `Resources/Settings/InputDriverSet.asset` (или `Tools/Vortex/Debug/Check Core Assets`). Добавить драйверы (`MouseInputDriver`/`TouchInputDriver`/`DirectInputDriver`/`ActionInputDriver`), назначить id экшенов из дропдауна. Пустой сет → failfast на Play.

### 3. Конфиг скинов
`Create → Vortex/UI/Cursor Skin Settings`. Заполнить `resolutionTiers` (по возрастанию), `defaultSetKey`, `sets` — темы; в каждой теме — паки по тирам, base/hover-скины, `defaultSprite` + разреженные `overrides` (действие→спрайт).

### 4. Input Actions
Экшены под драйверы (позиция мыши, движение, касание, кнопки Action1…Action10). В UI-карте модуля перебиндить `Point → <VirtualUiPointer>/position`, `Left Click → .../leftButton`, `Right/Middle/Forward/Back`, `ScrollWheel → .../scroll`.

### 5. Сцена
- `VirtualCursorBootstrap` (+ `CursorSkinSettings`, параметры проекции) — на постоянном объекте.
- `UiPointerFeeder` — там же. **Драйверы ввода на сцену не ставятся** — они в `InputDriverSet`.
- Оверлей `Canvas` (Screen Space - Overlay, поверх всего UI) + cursor `Image` (Raycast Target off) + `UiImageCursorRenderer`.
- Опц.: `IsOverUiHandler`, `CameraProvider` (на камере), `CursorHoverZone` (на интерактивных UGUI-элементах, ключ hover-скина).

### 6. Персист темы (проектный слой)
`CursorSkinData : IGameData` + мост: на загрузку/новую игру `CursorSkinSelector.Select(data.SelectedSetKey)`, на `Selected.OnUpdate` — запись обратно.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Модуль выключен (`USING_VORTEX_CURSOR` off) | Драйверы не компилируются; позицию никто не подаёт |
| `InputDriverSet` отсутствует / пуст | `CursorInputLoader` кидает исключение (failfast на загрузке) |
| Драйвер не поддерживает платформу | Пропускается при коннекте (`SupportsPlatform`) |
| Активный источник — касание (`Point`) | Курсор скрыт (`HidesCursor`); мышь/геймпад снова показывают |
| Исключение в `Tick` драйвера | Лог только на первое в серии; петля живёт, остальные драйверы тикаются |
| `CursorSkinSettings` не передан в `Init` | `Visual` = None; курсор не рисуется |
| Тема по ключу не найдена | Дефолтная (`defaultSetKey`), иначе первая |
| Разрешение выше всех тиров | Крупнейший тир; ниже всех — минимальный |
| Действие без спрайта в скине | Фолбэк вверх: base-скин → его `defaultSprite`; нигде нет → None |
| Скин с `HideCursor` | Курсор скрыт, спрайт не ставится |
| UI-модуль не забинжен на `<VirtualUiPointer>` | Родной UGUI не следует за виртуальным курсором |
| Alt-tab с зажатой кнопкой | `canceled` снимает бит — залипания нет |
| Проекция без зарегистрированной камеры | Промах (`false`/`null`) |
| Атласный спрайт курсора | `UiImageCursorRenderer` — ок; `OsCursorRenderer` — нужна standalone-текстура |

---

## Файловая структура

```
VirtualCursorSystem/
├── Bus/VirtualCursorBus.cs
├── VirtualCursorController.cs            # static core: модель, резолв Visual, интейк, hide-по-источнику
├── VirtualCursorController.Projection.cs # LIFO-камеры + raycast
├── VirtualCursorBootstrap.cs             # Init + ConfigureProjection
├── Model/
│   ├── PointerAction.cs  PointerSourceKind.cs
│   ├── PointerActionMask.cs  PointerActionMaskData.cs
│   ├── CursorVisual.cs  CursorVisualData.cs  PointerModel.cs
│   ├── CursorSkinResolver.cs  CursorSkinSelector.cs
├── Config/
│   ├── CursorSpriteEntry.cs  CursorSkin.cs  CursorSkinPack.cs
│   ├── CursorSkinSet.cs  CursorSkinSettings.cs
├── Input/
│   ├── VirtualUiPointer.cs  UiPointerFeeder.cs  IsOverUiHandler.cs
├── InputDrivers/                         # #if USING_VORTEX_CURSOR — подключаемый слой ввода
│   ├── InputDriver.cs  InputDriverSet.cs  CursorInputLoader.cs
│   ├── MouseInputDriver.cs  TouchInputDriver.cs  DirectInputDriver.cs  ActionInputDriver.cs
├── Drivers/                              # MonoBehaviour, сценово-привязанные (не драйверы ввода)
│   ├── CursorHoverZone.cs  CameraProvider.cs
├── Render/
│   ├── ICursorRenderer.cs  UiImageCursorRenderer.cs  OsCursorRenderer.cs
├── DefineSettings/                       # SDK-тоггл (вклинивается в сборку SdkSettings через .asmref)
│   ├── SdkSettings.CursorInput.cs  sdk.settings.system.ext.asmref
└── ru.vortex.unity.virtualcursorsystem.asmdef
```

Персист темы (`CursorSkinData : IGameData` + мост) живёт на проектном слое, вне пакета.

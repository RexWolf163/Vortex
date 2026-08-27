# CursorSystemAdvanced

**Namespace:** `Vortex.Unity.UI.CursorSystemAdvanced`
**Сборка:** `ru.vortex.unity.cursorsystemadvanced`

---

## Назначение

Виртуальный курсор с мульти-источником для UGUI-проектов. Единая экранная позиция (`ScreenPosition`) кормится любым источником, назначенным в Input Actions — мышь, геймпад, клавиши, тач — и является единственным источником истины. Позиция **расцеплена от ОС-мыши**: живёт в модели, а не в Mouse-девайсе, поэтому нет warp-хаков и рассинхрона двух устройств.

Родной UGUI (`Button`, `Toggle`, `ScrollRect`, drag, hover, `IPointerXxx`) работает без кода на каждый виджет: пакет предъявляет `InputSystemUIInputModule` виртуальное pointer-устройство (`VirtualUiPointer`), которое ведётся за `ScreenPosition`/действиями.

Внешний вид курсора — **render-агностичная система скинов**: сменные наборы-темы (выбираются в рантайме по ключу), масштаб от разрешения (глобальные тиры), спрайт по состоянию действий, с фолбэком вверх по настройкам. Рендер — через `ICursorRenderer` (по умолчанию UGUI-`Image` в позиции курсора; опционально ОС-курсор через `Cursor.SetCursor`).

**Противопоставление `CursorSystem`:** `CursorSystem` — ОС-курсор + UGUI-hover, mouse-only, упрощённая альтернатива. `CursorSystemAdvanced` — виртуальный курсор + арбитраж источников + render-агностичные сменные скины.

Вне ответственности:
- Игровые триггеры/механики клика — уровень потребителя (`AdvancedButton`/игровой код); пакет отдаёт позицию/действия/проекцию.
- Персист выбранной темы — проектный слой (L2 не зависит от L3-GameCore; см. «Выбор темы»).
- Интерпретация мирового хита — пакет отдаёт сырой `RaycastHit`.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Unity.InputSystem` | `InputAction`/`InputActionProperty`, `Mouse`/`MouseState`, `InputState`, кастомный девайс |
| `UnityEngine.UI` | `Image`/`Canvas` (UI-рендер), `IPointerEnter/Exit`, `EventSystem` (IsOverUI/hover) |
| `Vortex.Core.Extensions.ReactiveValues` | `ReactiveValue<T>`, `EnumData`/`StringData`/`BoolData` с owner-защитой |
| `Vortex.Unity.Extensions.ReactiveValues` | `Vector2Data` |
| `Vortex.Unity.EditorTools` | `[AutoLink]`, `[ClassLabel]` |
| Sirenix Odin Inspector | `[Tooltip]`/атрибуты инспектора |

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
 ├─ ReportPointer / SetAction / SetHover / SetOverUI      (internal — драйверы)
 ├─ Recompute → CursorSkinResolver → Visual (CursorVisualData)
 └─ Projection: RegisterCamera(LIFO) + ленивый raycast (TryGetWorldHit/GetWorldProjection)

[VirtualCursorBus] (static)  → Data / Visual / IsReady / OnReady            (read-only фасад)
[CursorSkinSelector] (static) → Selected(StringData) / Select(key)          (save-агностично)

Драйверы (источники, в пакете):
  MousePointerDriver(Analog+порог) · DirectPointerDriver(Direct) · TouchPointerDriver(Point)
  PointerActionDriver(кнопки→маска) · CursorHoverZone(UGUI→HoverKey) · CameraProvider(LIFO)

UGUI-мост:  VirtualUiPointer (: Mouse, отдельный layout)  ← UiPointerFeeder (LateUpdate: модель→девайс)
Рендер:     ICursorRenderer → UiImageCursorRenderer (дефолт) | OsCursorRenderer (опц.)
```

### Поток данных

```
Источник (мышь/стик/тач/клавиши) → Driver → VirtualCursorController.ReportPointer/SetAction
   → PointerModel (ScreenPosition/Actions/HoverKey)
        ├→ CursorSkinResolver → Visual → ICursorRenderer (рисует курсор)
        ├→ UiPointerFeeder → VirtualUiPointer → InputSystemUIInputModule → родной UGUI
        └→ Projection (по запросу) → RaycastHit
```

---

## Ключевые концепции

### Арбитраж источников (last-source-wins + порог)
`ReportPointer(pos, source)` делает репортящий источник активным. Порог активации — на стороне драйвера (специфика источника): пока источник не активен, он перехватывает управление лишь при заметном сдвиге (джиттер мыши не крадёт курсор у геймпада); активный источник трекается без порога. `PointerSourceKind`: `Analog` (мышь), `Point` (тач), `Direct` (геймпад/клавиши — интеграция скорость×dt, кламп к экрану).

### Маска действий (одновременность + доминанта)
`PointerAction` — последовательный enum-индекс (`None` + `Action1…Action10`, комментарии фиксируют конвенцию: 1=LMB, 2=RMB, 3=MMB, 4=Back, 5=Forward, 6=Scroll↑, 7=Scroll↓, 8–10=запас). `PointerActionMask` — `readonly struct` над `int`: биты = одновременно активные действия (для девайса), `Dominant()` — младший активный бит по приоритету (для выбора спрайта). Драйвер (`PointerActionDriver`) по `started`/`canceled` выставляет/снимает биты; `canceled` при alt-tab снимает их сам.

### Скины: тема → тир → hover → действие, с фолбэком вверх
`CursorSkinResolver.Resolve`:
1. **Тема** — `CursorSkinSelector.Selected` → `CursorSkinSet` (дефолт, если не найдена).
2. **Тир разрешения** — `SelectTierIndex(Screen.height)`: минимальный `resolutionTiers[i] >= height`, иначе крупнейший → `CursorSkinPack`.
3. **Скин** — hover-скин по `HoverKey`, иначе базовый; `HideCursor` → курсор скрыт, спрайт не ставится.
4. **Спрайт** — по `Actions.Dominant()`: `override` скина → его `defaultSprite` → **вверх**: базовый скин пакета → его `defaultSprite`. Клампа нет — незаданное действие поднимается по цепочке до заданного или дефолта.
5. Hotspot — из `Sprite.pivot`, инверсия по Y (`pivot` bottom-left → hotspot top-left).

### Глобальные тиры разрешения
Брейкпоинты (`resolutionTiers`) заданы **один раз** в `CursorSkinSettings` — единый девайсный концерн; каждая тема даёт по одному паку на тир (индекс совпадает, `OnValidate` предупреждает о рассогласовании). Смена разрешения → `VirtualCursorController.RefreshResolution()`.

### Виртуальный UI-указатель и родной UGUI
`VirtualUiPointer` — подкласс `Mouse` с **отдельным layout** (`<VirtualUiPointer>`): `InputSystemUIInputModule` биндится на него и генерит все родные события (клик/hover/drag/scroll) для мыши и геймпада единообразно. `UiPointerFeeder` в `LateUpdate` пишет девайс из модели: `position ← ScreenPosition`, кнопки ← биты маски (Action1→left…Action5→forward), scroll ← Action6/7. `MousePointerDriver` игнорирует события своего же `VirtualUiPointer` (иначе feedback-петля). Реальная мышь кормит Analog-драйвер (`<Mouse>`), UI-модуль — только виртуальный указатель.

### IsOverUI
`IsOverUiHandler` пишет `PointerModel.IsOverUI` из `EventSystem.IsPointerOverGameObject()` — потребители мировой проекции гейтят клик по этому флагу (замена UITK-picking).

### Screen→world проекция
`VirtualCursorController` ведёт LIFO-реестр камер (`CameraProvider`); `TryGetWorldHit`/`GetWorldProjection` — ленивый `Physics.Raycast` по позиции курсора с кэшем на кадр (`InvalidateProjection` сбрасывает). Пакет отдаёт сырой хит.

### Выбор темы (save-агностично)
`CursorSkinSelector` держит реактивный ключ темы (`Selected`) и `Select(key)`. Персист (`IGameData`) — на **проектном слое**: `CursorSkinData` + зеркало, читающее ключ на загрузку/новую игру и пишущее его на изменение `Selected`. Пакет (L2) не зависит от GameCore (L3).

---

## Контракт

### Вход
- Input Actions: позиция (`<Mouse>/position`), вектор движения (`<Gamepad>/leftStick`), позиция касания, кнопки под `Action1…Action10`.
- UI-модуль забинжен на `<VirtualUiPointer>` (Point/Click/Scroll).
- `CursorSkinSettings` (SO) передан в `Init`.

### Выход
- `PointerModel` (позиция/источник/маска/hover/над-UI) — реактивно.
- `CursorVisual` — текущий вид курсора (спрайт+hotspot+hide) для рендера.
- Виртуальный девайс, ведущий родной UGUI.
- `RaycastHit`/точка проекции по запросу.

### Гарантии
- Одна экранная позиция для рендера, UI и проекции — без рассинхрона.
- Одновременность действий на девайсе; одиночная доминанта для спрайта.
- `canceled`/alt-tab снимает биты действий — залипания нет.
- Владение реактивными полями закреплено за контроллером — извне не пишутся.

### Ограничения
- UGUI-модуль **должен** биндиться на `<VirtualUiPointer>`, иначе UI не следует за виртуальным курсором.
- `IsOverUI` использует no-arg `IsPointerOverGameObject()` — при нескольких активных указателях может потребоваться pointerId.
- Проекция требует зарегистрированной камеры; без неё — промах.
- Требуется устройство мыши для UI-модуля-мыши (десктоп); чистая консоль — вне scope.

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
static void RefreshResolution();                       // после смены разрешения/режима окна
static void ConfigureProjection(LayerMask mask, float distance);
static bool TryGetWorldHit(out RaycastHit hit);
static Vector3? GetWorldProjection();
static void InvalidateProjection();
// intake (internal): ReportPointer / SetAction / ClearActions / SetHover / SetOverUI / Register/UnregisterCamera
```

### CursorSkinSelector (static)
```csharp
static StringData Selected;                 // реактивный ключ темы
static void Select(string setKey);
static bool IsSelected(string setKey);
```

---

## Использование

### 1. Конфиг скинов
`Create → Vortex/UI/Cursor Skin Settings`. Заполнить `resolutionTiers` (по возрастанию), `defaultSetKey`, `sets` — темы; в каждой теме — паки по тирам, base/hover-скины, `defaultSprite` + разреженные `overrides` (действие→спрайт).

### 2. Input Actions
Экшены под драйверы: позиция (`<Mouse>/position`), движение (`<Gamepad>/leftStick`), касание, кнопки Action1…Action10 (мышь/геймпад/скролл). В UI-карте модуля перебиндить `Point → <VirtualUiPointer>/position`, `Left Click → .../leftButton`, `Right/Middle/Forward/Back`, `ScrollWheel → .../scroll`.

### 3. Сцена
- `VirtualCursorBootstrap` (+ `CursorSkinSettings`, параметры проекции) — на постоянном объекте.
- `UiPointerFeeder`, драйверы (`MousePointerDriver`/`DirectPointerDriver`/`TouchPointerDriver`/`PointerActionDriver` с привязками) — там же.
- Оверлей `Canvas` (Screen Space - Overlay, sort order выше всего UI) + cursor `Image` (Raycast Target off) + `UiImageCursorRenderer` (ссылки на RectTransform/Image).
- Опц.: `IsOverUiHandler`, `CameraProvider` (на камере), `CursorHoverZone` (на интерактивных UGUI-элементах, ключ hover-скина).

### 4. Персист темы (проектный слой)
`CursorSkinData : IGameData` + мост: на загрузку/новую игру `CursorSkinSelector.Select(data.SelectedSetKey)`, на `Selected.OnUpdate` — запись обратно.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `CursorSkinSettings` не передан в `Init` | `Visual` = None; курсор не рисуется |
| Тема по ключу не найдена | Дефолтная (`defaultSetKey`), иначе первая |
| Разрешение выше всех тиров | Крупнейший тир; ниже всех — минимальный |
| Паков в теме меньше/больше тиров | Кламп к диапазону; `OnValidate` предупреждает |
| Действие без спрайта в скине | Фолбэк вверх: base-скин → его `defaultSprite`; нигде нет → None |
| Скин с `HideCursor` | Курсор скрыт, спрайт не ставится |
| UI-модуль не забинжен на `<VirtualUiPointer>` | Родной UGUI не следует за виртуальным курсором |
| Alt-tab с зажатой кнопкой | `canceled` снимает бит — залипания нет |
| Проекция без зарегистрированной камеры | Промах (`false`/`null`) |
| Реальная мышь и Direct одновременно | Порог гейтит перехват; активный источник трекается без порога |

---

## Файловая структура

```
CursorSystemAdvanced/
├── Bus/VirtualCursorBus.cs
├── VirtualCursorController.cs            # static core: модель, резолв Visual, интейк
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
├── Drivers/
│   ├── MousePointerDriver.cs  DirectPointerDriver.cs  TouchPointerDriver.cs
│   ├── PointerActionDriver.cs  CursorHoverZone.cs  CameraProvider.cs
├── Render/
│   ├── ICursorRenderer.cs  UiImageCursorRenderer.cs  OsCursorRenderer.cs
└── ru.vortex.unity.cursorsystemadvanced.asmdef
```

Персист темы (`CursorSkinData : IGameData` + мост) живёт на проектном слое, вне пакета.

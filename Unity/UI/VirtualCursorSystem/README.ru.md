# VirtualCursorSystem

**Namespace:** `Vortex.Unity.UI.VirtualCursorSystem`
**Сборка:** `ru.vortex.unity.virtualcursorsystem`

---

## Назначение

Виртуальный курсор с мульти-источником для UGUI-проектов. Единая экранная позиция (`ScreenPosition`) кормится любым источником — мышь, геймпад, клавиши, тач — и является единственным источником истины. Позиция **расцеплена от ОС-мыши**: живёт в модели (при желании ОС-мышь синхронно warp'ается — опция `warpSystemMouse` в `DirectInputDriver`).

Родной UGUI (`Button`, `Toggle`, `ScrollRect`, hover, `IPointerXxx`) работает без кода на каждый виджет: `VirtualPointerDispatcher` в кадре события от курсора делает `EventSystem.RaycastAll` под `ScreenPosition` и шлёт стандартные `PointerDown`/`PointerUp`/`PointerClick`/`PointerEnter`/`PointerExit` через `ExecuteEvents`. Фантомного `InputDevice` нет; биндинги в UI Actions-asset остаются на `<Mouse>` — физическая мышь и виртуальный курсор независимо шлют события одним и тем же UGUI-handler'ам.

Внешний вид курсора — **render-агностичная система скинов**: сменные наборы-темы (по ключу в рантайме), масштаб от разрешения (глобальные тиры), спрайт по состоянию действий, с фолбэком вверх. Рендер — через `ICursorRenderer` (по умолчанию UGUI-`Image` в позиции курсора; опционально ОС-курсор через `Cursor.SetCursor`).

**Ввод — подключаемый модуль.** Источники реализованы как драйверы (`InputDriver`), которые перечисляются в настроечном ассете `InputDriverSet` и подключаются на старте контроллером-загрузчиком `CursorInputLoader`. Сам модуль ввода включается тогглом в `SdkSettings` (`USING_VORTEX_CURSOR`). Курсор — **надсистемная сущность**: ситуативного гейта ввода нет, подключённый драйвер активен всегда.

**Фокус-навигация** (подсистема `Focus/`): `FocusGroup` на любом родителе собирает дочерние `FocusTargetComponent`-ы в контекст навигации; группы живут в LIFO-стеке (push/pop на `OnEnable`/`OnDisable` — стандартный паттерн SetActive меню). `UINavigationDriver` слушает 4 action'а на D-pad/стрелки; на нажатие — ближайший target в полуоткрытом конусе ±45° **из активной (топовой) группы** становится активным. Курсор прячется через внешний канал (`HideCursor`) и варпается на точку target'а — hover и клик проходят естественным UGUI-путём. Движение курсора мышью/стиком автоматически сбрасывает фокус. При переключении активной группы (пока курсор в nav mode) фокус автоматически передаётся на `RememberedFocus` новой группы или ближайший к курсору target.

**Противопоставление `CursorSystem`:** `CursorSystem` — ОС-курсор + UGUI-hover, mouse-only, упрощённая альтернатива. `VirtualCursorSystem` — виртуальный курсор + арбитраж источников + render-агностичные сменные скины + подключаемый драйверный слой ввода.

Вне ответственности:
- Игровые триггеры/механики клика — уровень потребителя (`AdvancedButton`/игровой код); пакет отдаёт позицию/действия/проекцию.
- Персист выбранной темы — проектный слой (L2 не зависит от L3-GameCore; см. «Выбор темы»).
- Интерпретация мирового хита — пакет отдаёт сырой `RaycastHit`.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Unity.InputSystem` | `InputAction`, `Mouse` (warp системной мыши в `DirectInputDriver`) |
| `UnityEngine.UI` | `Image`/`Canvas` (UI-рендер) |
| `UnityEngine.EventSystems` | `EventSystem.RaycastAll`, `ExecuteEvents`, `PointerEventData`, `IPointerXxx` интерфейсы |
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
     ├─ DirectInputDriver (Direct, NeedsTick; speedCurve + accelerationTime) · ActionInputDriver (кнопки→маска)
     └─ UINavigationDriver (4 action'а → VirtualCursorFocusController.Navigate)

Фокус-навигация (Focus/):
  [FocusModel] (IReactiveData)       → LIFO Groups[] + ActiveGroup + ReactiveValue<IFocusTarget> CurrentFocus
  [FocusGroup] (MonoBehaviour)        → OnEnable push LIFO · OnDisable pop · OnDestroy cleanup
                                        · Targets[] (регистрация детей) · RememberedFocus · FindNearestEuclidean
  [VirtualCursorFocusController] (static) → Init/Cleanup · PushGroup/RemoveGroup · Navigate(dir)/ClearFocus
                                            · авто-передача фокуса на push/pop если _focusAnchor != null
                                            · подписка на ScreenPosition → авто-ClearFocus на движении
                                            · _focusAnchor абсорбирует суб-пороговый дрейф (per-frame 3 px)
  [IFocusTarget] (контракт)          → ScreenPoint · IsActive · NotifyFocused/NotifyUnfocused

Сценовые MonoBehaviour (не драйверы ввода):
  CursorHoverZone (UGUI→HoverKey) · CameraProvider (камера проекции, LIFO)
  IsOverUiHandler (EventSystem→IsOverUI)
  PointerActionHandler (UGUI-биндинг Action1/2/3 → UnityEvent — опционально, на кнопках)
  FocusGroup (на родителе; LIFO контекст навигации)
  FocusTargetComponent (на кнопке/объекте; UGUI/World → регистрация в родительской FocusGroup + UnityEvent onFocused/onUnfocused)

UGUI-мост:  VirtualPointerDispatcher — подписка на ScreenPosition/Actions, RaycastAll, ExecuteEvents
Рендер:     ICursorRenderer → UiImageCursorRenderer (дефолт) | OsCursorRenderer (опц.)
```

### Поток данных

```
Источник (мышь/стик/тач/клавиши)
   → InputDriver (резолв экшена по id через InputController) → VirtualCursorController.ReportPointer/SetAction
        → PointerModel (ScreenPosition/Actions/HoverKey; hidesCursor по источнику)
             ├→ CursorSkinResolver → Visual → ICursorRenderer (рисует курсор)
             ├→ VirtualPointerDispatcher (OnUpdate→dirty; LateUpdate если dirty)
             │     → EventSystem.RaycastAll → ExecuteEvents(Enter/Exit/Down/Up/Click) → UGUI-handlers
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

### Фокус-навигация (Focus/)
Переключение активного элемента нажатием направлений (D-pad/стрелки) — альтернатива курсорному управлению для гейпада/клавиатуры в UI-сценах.

**LIFO-стек групп (контексты навигации).**
- `FocusGroup` — MonoBehaviour на любом родителе интерактивных элементов. На `OnEnable` push'ится в LIFO; на `OnDisable` — pop; на `OnDestroy` — финальный cleanup. Активная группа (`ActiveGroup`) — верхняя в стеке; только её `Targets` участвуют в `Navigate`.
- Стандартный паттерн — SetActive меню: HUD-группа всегда активна, при открытии меню паузы оно пушится поверх → навигация идёт по его кнопкам; закрыл меню → активной снова становится HUD-группа.
- Nested-группы — вложенные `FocusGroup` допустимы; target цепляется к ближайшей родительской через `GetComponentInParent`.

**Регистрация target'ов.**
- `FocusTargetComponent` на UGUI-кнопке (`TargetKind=UGUI`, ссылка на `RectTransform`) или world-объекте (`TargetKind=World`, `Transform` + активная камера из `CameraProvider`). `OnEnable` → `GetComponentInParent<FocusGroup>` (lazy-cached) → `Register`; `OnDisable` → `Unregister`.
- **Fail-loud:** target без `FocusGroup` в родителях → `LogError` на первом Enable, компонент в систему не регистрируется. Это ошибка настройки сцены.

**Алгоритм Navigate.** Полуоткрытый конус `[-45°..+45°)` относительно направления (4 направления × 90° = покрытие 360° без пересечений, target попадает ровно в одну зону). Среди попавших — **минимум евклидовой дистанции**. Отсчёт от **точной `ScreenPoint` текущего фокуса** (не от позиции курсора — она после `WarpCursorPosition` округляется ОС-мышью до int-пикселя и даёт сдвиг ≈0.5 px, что на коротких дистанциях путает конусную выборку); если фокуса нет — fallback на `ScreenPosition` курсора. Текущий `CurrentFocus` исключается из кандидатов (defensive `ReferenceEquals` поверх дистанции).

**«Захват фокуса» (`captureFocusIfFree`).** Опциональный fallback-режим в `UINavigationDriver` (default `true`): если в конусе никого **и** `CurrentFocus == null` — выбирается ближайший активный target группы по евклидовой дистанции, без учёта направления. Любая клавиша-направление становится точкой входа в nav mode: курсор «прилипает» к ближайшей кнопке. При уже активном фокусе этот fallback **не** применяется — иначе навигация у края группы (нет соседей в этом направлении) молча бы перескакивала на противоположный конец списка.

**Авто-передача фокуса при push/pop группы.** Срабатывает **только** если `_focusAnchor != null` (курсор был «притянут» навигацией и не сдвинут мышью с тех пор):
- Push: старый фокус сохраняется в `RememberedFocus` старой группы + `NotifyUnfocused`; в новой топ-группе фокусом становится `RememberedFocus` (если ещё активен) или ближайший евклидовой дистанции target → `NotifyFocused` + warp.
- Pop: симметрично — старая группа теряет current (сохраняется в её Remembered), новая топ-группа восстанавливает свой.
- Если `_focusAnchor == null` (пользователь двигал мышь, вышел из nav mode) — автоматика выключена, стек просто перестраивается, курсор остаётся свободным.

**Эффекты фокуса:** `IFocusTarget.NotifyFocused`/`NotifyUnfocused` → UnityEvent'ы на компоненте (подсветка/SFX); курсор скрывается через `VirtualCursorController.HideCursor()` (external-канал — независим от Report'ов драйверов); `Mouse.WarpCursorPosition` синхронизирует ОС-мышь с точкой target'а.

**Авто-ClearFocus на движении курсора:** `_focusAnchor` + `AnchorToleranceSqr=9` (3 px) — **per-frame** порог, не кумулятивный. Дельта ниже порога **поглощается в сам anchor** на каждый апдейт `ScreenPosition`: hardware noise ОС-мыши (~1 px/кадр при простое), int-округление `WarpCursorPosition` и warp-эхо не копятся кадр за кадром от исходной точки — иначе случайный спайк за секунду-другую пробивал бы tolerance без действия пользователя (выглядит как «периодический выброс в свободный курсор»). Намеренный жест мышью (5+ px/кадр даже на самом медленном осознанном движении на 60 Гц) пробивает порог за один кадр → `ClearFocus` + `ShowCursor()` + выход из nav mode.

### Внешний канал скрытия курсора (`HideCursor`/`ShowCursor`)
Независимый API поверх источников: `VirtualCursorController.HideCursor()` / `ShowCursor()`. `_externalHidden` входит в OR-композицию `visual.Hide` в `Recompute` (три канала: скин → `_pointerHidden` от источника → `_externalHidden` от внешнего кода). Не сбрасывается `ReportPointer`-ами драйверов — критично для фокус-навигации, где mouse-echo после warp'а иначе бы перезаписал hide back to false.

`ShowCursor` обнуляет **только** свой канал: если скин задан с `HideCursor=true` или активный источник (`TouchInputDriver`) скрывает курсор — он останется скрыт.

### Профиль скорости в DirectInputDriver (curve + разгон)
Итоговая скорость курсора: `speed × speedCurve.Evaluate(stickMagnitude) × accelFactor`.
- **`speedCurve`** (AnimationCurve) — нелинейный ответ на отклонение стика. X ∈ [0..1] = магнитуда стика, Y = множитель к `speed`. Дефолт — константа 1 (кривая не влияет). Для precision-control на малых отклонениях — `Pow(x, 2)` или аналог.
- **`accelerationTime`** (сек) — плавный набор скорости от 0 до максимума при выходе стика из деад-зоны. `0` = мгновенный разгон (bang-bang, как было). Торможение в деад-зоне **мгновенное** (`_accelFactor` сбрасывается в 0) — курсор слушается дизайнера без инерции на остановке; следующий старт пойдёт с 0.
- **Валидация**: на `Connect` проверка `speedCurve.Evaluate(1) ≈ 0` → `LogWarning` с указанием `moveActionId`. Ловит типовую ошибку конфигурации «курсор не двигается на полной амплитуде».

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

### Виртуальный pointer и родной UGUI (`VirtualPointerDispatcher`)
`VirtualPointerDispatcher` подписан на `ScreenPosition.OnUpdate` и `Actions.OnUpdate` в `Bus`; событие ставит `_dirty` — `LateUpdate` обсчитывает цикл **только** на изменениях (в простое — одна проверка булева). За один проход: `EventSystem.RaycastAll` в точке позиции → Enter/Exit diff → переходы Action1/2/3 (LMB/RMB/MMB) с `pointerDownHandler`/`pointerUpHandler`/`pointerClickHandler` через `ExecuteEvents` на найденный target. Поддерживается стандартный UGUI-канон клика (Up на том же `IPointerClickHandler`-таргете, что и Down).

**Нет фантомного `InputDevice`**: биндинги в UI Actions-asset остаются на `<Mouse>` для физической мыши. Виртуальный pointer и физическая мышь шлют события тем же UGUI-handler'ам независимо — никаких петель. Поддерживает UGUI, 2D-/3D-коллайдеры (через стандартные `Physics2DRaycaster`/`PhysicsRaycaster` на камере — `RaycastAll` вернёт и их).

**Ленивая инициализация**: `PointerEventData` создаётся в `LateUpdate` при первом появлении `EventSystem.current`. Диспетчер корректно работает на `Preload`-сцене, когда EventSystem живёт на UI-сцене и загружается позже.

**На кнопках**: для **правой/средней** кнопки — `PointerActionHandler` на UGUI-элементе (dropdown `PointerAction` + `onPressed`/`onReleased`/`onClick`). Для **левой** — обычный `Button.onClick`. Action4..Action10 через UGUI-пайплайн не проходят (`PointerEventData.InputButton` знает только Left/Right/Middle).

### IsOverUI
`IsOverUiHandler` пишет `PointerModel.IsOverUI` из `EventSystem.IsPointerOverGameObject()` — потребители мировой проекции гейтят клик по флагу.

### Screen→world проекция
`VirtualCursorController` ведёт LIFO-реестр камер (`CameraProvider`); `TryGetWorldHit`/`GetWorldProjection` — ленивый `Physics.Raycast` с кэшем на кадр. Пакет отдаёт сырой хит.

### Выбор темы (save-агностично)
`CursorSkinSelector` держит реактивный ключ темы (`Selected`) и `Select(key)`. Персист (`IGameData`) — на **проектном слое**. Пакет (L2) не зависит от GameCore (L3).

### Editor-инструментарий
- **`Tools/Vortex/Virtual Cursor/Focus Stack`** — диагностическое окно: LIFO-стек `FocusGroup` сверху-вниз (топ = `ActiveGroup`), для каждой группы список `Targets`, маркер `●` на текущем `CurrentFocus`, маркер `◉` на `RememberedFocus`, пометка `[inactive]`. Клик по строке группы или target'а — Ping + Select в Hierarchy. Данные только в Play Mode после `VirtualCursorBootstrap.Init`; авто-repaint через `EditorApplication.update`. Палитра стилей переключается по `EditorGUIUtility.isProSkin`.
- **`Tools/Vortex/Configs/Virtual Cursor Skin Settings`** — подсветить ассет `CursorSkinSettings` в Project window.
- **`Tools/Vortex/Configs/Input Driver Set`** — подсветить ассет `InputDriverSet` в Project window.
- Легаси-пункт `Tools/Vortex/Configs/Cursor Settings` (из пакета `CursorSystem`) обёрнут в `#if !USING_VORTEX_CURSOR` — при включённом новом курсоре в меню не показывается.

---

## Контракт

### Вход
- `SdkSettings`: тоггл `cursorInputSdk` включён (дефайн `USING_VORTEX_CURSOR`).
- `InputDriverSet` (SO в `Resources/Settings`): непустой список драйверов с назначенными id экшенов.
- Input Actions: экшены под драйверы (позиция мыши, позиция касания, вектор движения, кнопки Action1…Action10); UI Actions-asset (`InputSystemUIInputModule`) остаётся стандартным — биндится на `<Mouse>`.
- `CursorSkinSettings` (SO) передан в `Init` (через `VirtualCursorBootstrap`).
- `EventSystem` в активных сценах (стандартный UGUI-объект, нужен диспетчеру).

### Выход
- `PointerModel` (позиция/источник/маска/hover/над-UI) — реактивно.
- `CursorVisual` — текущий вид курсора (спрайт+hotspot+hide) для рендера.
- Стандартные UGUI-события на target'ах под курсором (Enter/Exit/Down/Up/Click) через `VirtualPointerDispatcher`.
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
- `VirtualPointerDispatcher` должен быть **в активной сцене** (persistent-сцена рядом с Bootstrap/Renderer — идеально); без него UGUI-handler'ы не получат события от виртуального курсора. `EventSystem` может быть на любой другой сцене — диспетчер подхватится лениво.
- Через UGUI-пайплайн доступны только `Action1`/`Action2`/`Action3` (LMB/RMB/MMB). Для `Action4..Action10` нужен прямой биндинг на `VirtualCursorBus.Data.Actions.OnUpdate` с ручной проверкой hover-зоны.
- Проекция требует зарегистрированной камеры; без неё — промах.
- `OsCursorRenderer` требует **standalone-текстуру** спрайта (`Cursor.SetCursor` берёт целую `Texture2D`). Для атласных курсоров — `UiImageCursorRenderer`.
- `InputController` (шина ввода) должен быть доступен на момент коннекта — он лениво инициализируется по первому обращению (`GetAction`), явного ожидания в `WaitingFor` не требуется.

---

## API

### VirtualCursorBus (static)
```csharp
static PointerModel     Data;      // runtime-модель
static CursorVisualData Visual;    // текущий вид курсора
static FocusModel       Focus;     // runtime-реестр фокус-навигации (CurrentFocus.OnUpdate)
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

// Внешний канал скрытия курсора — независим от Report'ов и от скина (OR-композиция).
static void HideCursor();                 // _externalHidden = true, Recompute
static void ShowCursor();                 // _externalHidden = false; скин/источник остаются
static bool IsCursorHiddenExternally;     // query

// intake (internal): ReportPointer(pos,src) / ReportPointer(pos,src,hidesCursor)
//                    / SetAction / ClearActions / SetHover / SetOverUI / Register/UnregisterCamera
```

### VirtualCursorFocusController (static)
```csharp
static FocusModel Model;                                         // null до Init
static bool IsReady;

static void Init();                                              // зовётся из VirtualCursorBootstrap
static void Cleanup();
static void PushGroup(FocusGroup group);                         // из FocusGroup.OnEnable
static void RemoveGroup(FocusGroup group);                       // из OnDisable/OnDestroy
static void Navigate(Vector2 direction, float coneAngleDeg,
                     bool hideCursor, bool warpSystemMouse,
                     bool captureNearestIfFree);                 // из UINavigationDriver
static void ClearFocus();                                        // + ShowCursor() + NotifyUnfocused
```

### FocusGroup (MonoBehaviour)
```csharp
IReadOnlyList<IFocusTarget> Targets;                             // регистрируются детьми
IFocusTarget RememberedFocus;                                    // сохраняется между активациями
void Register(IFocusTarget target);                              // из FocusTargetComponent.OnEnable
void Unregister(IFocusTarget target);                            // из OnDisable
void ClearRememberedFocus();                                     // явный сброс (опционально)
```

### IFocusTarget (контракт)
```csharp
Vector2 ScreenPoint { get; }   // пересчитывается при каждом запросе
bool IsActive { get; }         // GameObject.activeInHierarchy + локальные гейты
void NotifyFocused();          // зовёт контроллер при переходе фокуса на этот target
void NotifyUnfocused();        // другой target / ClearFocus / OnDisable
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
Экшены под драйверы (позиция мыши, движение стика, касание, кнопки Action1…Action10). **UI Actions-asset для `InputSystemUIInputModule` стандартный** — биндинги `Point/Click/RightClick/ScrollWheel` на `<Mouse>` (физическая мышь). Никаких перебиндингов под виртуальный pointer не требуется — его события идут напрямую через `ExecuteEvents`.

### 5. Сцена
- `VirtualCursorBootstrap` (+ `CursorSkinSettings`, параметры проекции) — на persistent-сцене (`Preload`/boot). `VirtualCursorFocusController.Init` вызывается автоматически в `Awake`.
- `VirtualPointerDispatcher` — там же, рядом с Bootstrap. **Драйверы ввода на сцену не ставятся** — они в `InputDriverSet`.
- Оверлей `Canvas` (Screen Space - Overlay, поверх всего UI) + cursor `Image` (Raycast Target off) + `UiImageCursorRenderer`.
- Опц.: `IsOverUiHandler`, `CameraProvider` (на камере), `CursorHoverZone` (на интерактивных UGUI-элементах, ключ hover-скина), `PointerActionHandler` (на UI-кнопках для RMB/MMB), `FocusTargetComponent` (на UI-кнопках или world-объектах для gamepad-навигации).
- Для 2D/3D-объектов — `Physics2DRaycaster`/`PhysicsRaycaster` на камере + MonoBehaviour c `IPointerClickHandler`/`IPointerEnterHandler` на объекте. Диспетчер работает одинаково для UGUI и world-коллайдеров.

### 7. Фокус-навигация (опционально)
- В `InputDriverSet` добавить `UINavigationDriver`: назначить 4 action-id под направления (Gamepad D-pad / Keyboard arrows), настроить `coneAngleDeg = 45`, `hideCursorOnFocus = true`, `warpSystemMouse = true`, `captureFocusIfFree = true` (при пустом фокусе и отсутствии кандидата в конусе — захватить ближайший target группы; удобная точка входа в nav mode).
- На родителе интерактивных элементов (обычно Canvas или контейнер меню) — `FocusGroup`. Один компонент на один контекст навигации (HUD, меню паузы, модальный диалог — каждый своим).
- На каждом фокусируемом элементе внутри иерархии этой группы — `FocusTargetComponent`:
  - `TargetKind=UGUI` + ссылка на `RectTransform` (слот `rectTarget` виден в инспекторе только для UGUI через Odin `ShowIf`).
  - `TargetKind=World` + ссылка на `Transform` (слот `worldTarget`) — требует зарегистрированной `CameraProvider`-камеры для вычисления screen-point.
- Привязать `onFocused`/`onUnfocused` UnityEvent'ы для подсветки/SFX.
- Переключение между контекстами — стандартный Unity-паттерн: `SetActive(true)` на объекте с `FocusGroup` → push в LIFO, становится активным. `SetActive(false)` → pop, активной становится предыдущая группа. При nav mode (`_focusAnchor != null`) фокус передаётся автоматически — на `RememberedFocus` новой группы или ближайший target.
- Клик по сфокусированному элементу работает автоматически — курсор уже на точке через warp, `VirtualPointerDispatcher` отдаёт `PointerClick` стандартным UGUI-handler'ам.

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
| `VirtualPointerDispatcher` отсутствует в сцене | UGUI не реагирует на виртуальный курсор (физическая мышь работает штатно через InputSystemUIInputModule) |
| `DirectInputDriver.speedCurve.Evaluate(1) == 0` | `LogWarning` на Connect; курсор не двигается при полном отклонении стика — поправить кривую |
| `DirectInputDriver.accelerationTime = 0` | Мгновенный разгон (bang-bang) — старое поведение до правки |
| Нет ни одной `FocusGroup` в сцене | `UINavigationDriver.Navigate` ничего не находит (стек пуст) — молчит |
| `FocusTargetComponent` без родительской `FocusGroup` | `LogError` на первом Enable, компонент не регистрируется — настроить сцену |
| `FocusTargetComponent` с `TargetKind=World` без камеры в `CameraProvider` | `IsActive = false` → target не участвует в выборе |
| Деактивация активной `FocusGroup` (`SetActive(false)`) | `OnDisable` → `RemoveGroup` → если nav mode: авто-передача фокуса на новую топ-группу (RememberedFocus или ближайший); иначе стек просто перестраивается |
| Активация другой `FocusGroup` поверх текущей | `OnEnable` → `PushGroup` → если nav mode: авто-передача фокуса в новую группу; иначе — старый фокус снимается (NotifyUnfocused), новая группа без фокуса |
| Возврат к ранее активной группе | Её `RememberedFocus` вернётся как фокус (если ещё активен); иначе — ближайший евклидовой дистанции |
| `FocusTargetComponent` деактивируется когда он `CurrentFocus` | `OnDisable` → `Unregister` в группе (сбросит её `RememberedFocus`, если указывал сюда); global `CurrentFocus` обнулится через ClearFocus при следующем Navigate |
| Пользователь шевельнул мышь/стик при активном фокусе | Авто-`ClearFocus` на кадре с per-frame дельтой > `AnchorToleranceSqr` (3 px); под-порог поглощается в anchor и не копится. Фокус снимается, курсор становится видим, выход из nav mode (авто-передача на push/pop больше не сработает) |
| В конусе нет кандидатов, `CurrentFocus == null`, `captureFocusIfFree = true` | Fallback «захват фокуса»: ближайший активный target группы по евклидовой дистанции, без учёта направления |
| В конусе нет кандидатов, `CurrentFocus != null` (фокус активен, край списка) | Молчит — fallback-«захват» намеренно не срабатывает (чтобы не прыгать на противоположный конец) |
| `HideCursor` + скин с `HideCursor=false` + `ShowCursor` | `_externalHidden` обнулён, курсор снова показан скином (если `_pointerHidden` тоже false) |
| Nested `FocusGroup` | Target цепляется к ближайшей родительской через `GetComponentInParent`; LIFO работает естественно: вложенная поверх родительской |
| `EventSystem` ещё не загружен при старте | Диспетчер подхватится лениво при появлении EventSystem; события до этого момента теряются (курсор ещё не над UI) |
| Alt-tab с зажатой кнопкой | `canceled` снимает бит — залипания нет |
| Диспетчер отключён (`OnDisable`) | Все подвисшие Enter/Press сняты принудительно — повторный Enable начнёт с чистого листа |
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
│   ├── VirtualPointerDispatcher.cs  PointerActionHandler.cs  IsOverUiHandler.cs
├── Focus/
│   ├── FocusModel.cs  FocusGroup.cs  FocusTargetData.cs  IFocusTarget.cs  VirtualCursorFocusController.cs
├── InputDrivers/                         # #if USING_VORTEX_CURSOR — подключаемый слой ввода
│   ├── InputDriver.cs  InputDriverSet.cs  CursorInputLoader.cs
│   ├── MouseInputDriver.cs  TouchInputDriver.cs  DirectInputDriver.cs  ActionInputDriver.cs  UINavigationDriver.cs
├── Drivers/                              # MonoBehaviour, сценово-привязанные (не драйверы ввода)
│   ├── CursorHoverZone.cs  CameraProvider.cs  FocusTargetComponent.cs
├── Render/
│   ├── ICursorRenderer.cs  UiImageCursorRenderer.cs  OsCursorRenderer.cs
├── DefineSettings/                       # SDK-тоггл (вклинивается в сборку SdkSettings через .asmref)
│   ├── SdkSettings.CursorInput.cs  sdk.settings.system.ext.asmref
├── Editor/                               # editor-only (стандартная папка Unity, без своего asmdef)
│   ├── FocusStackWindow.cs               # Tools/Vortex/Virtual Cursor/Focus Stack — LIFO-дамп + Ping
│   ├── MenuController.cs                 # Tools/Vortex/Configs/Virtual Cursor Skin Settings + Input Driver Set
└── ru.vortex.unity.virtualcursorsystem.asmdef
```

Персист темы (`CursorSkinData : IGameData` + мост) живёт на проектном слое, вне пакета.

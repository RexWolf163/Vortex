# VirtualCursorSystem

**Namespace:** `Vortex.Unity.UI.VirtualCursorSystem`
**Сборка:** `ru.vortex.unity.virtualcursorsystem`

---

## Назначение

Виртуальный курсор с мульти-источником для UGUI-проектов. Единая экранная позиция (`ScreenPosition`) кормится любым источником — мышь, геймпад, клавиши, тач — и является единственным источником истины для рендера курсора, UGUI-ввода, фокус-навигации и screen→world проекции.

Источники реализованы как **драйверы** (`InputDriver`), перечисленные в настроечном ассете `InputDriverSet` и подключаемые на старте загрузчиком `CursorInputLoader`. Курсор — **надсистемная сущность**: ситуативного гейта ввода нет, подключённый драйвер активен всегда.

Внешний вид — **строковый скин**: контроллер сводит итоговое имя скина из флага скрытия (от последнего источника ввода) и LIFO-стека запросов скинов (например, от зон наведения). Рендер показывает скин по имени: `UiCursorRenderer` (UGUI, через `UIStateSwitcher`) или `OsCursorRenderer` (системный ОС-курсор).

**Фокус-навигация** (`Focus/`): `FocusGroup` собирает дочерние `FocusTargetComponent`-ы в контекст навигации; группы живут в стеке с приоритетами. `UINavigationDriver` по направлению выбирает ближайшую цель в конусе, переносит туда курсор (и ОС-мышь) и, по флагу, скрывает его; submit активирует фокусный элемент.

**Весь пакет гейтится тогглом в SDK Settings** (`cursorInputSdk` → дефайн `USING_VORTEX_CURSOR`, `defineConstraints` в asmdef). Исключение — файл-partial с самим тогглом (`DefineSettings/SdkSettings.CursorInput.cs`), который через `.asmref` входит в сборку `SdkSettings` и виден всегда.

**Противопоставление `CursorSystem`:** `CursorSystem` — ОС-курсор + UGUI-hover, mouse-only. `VirtualCursorSystem` — виртуальная позиция + арбитраж источников + подключаемые драйверы + фокус-навигация. При включённом `USING_VORTEX_CURSOR` легаси-меню `CursorSystem` скрыто.

Вне ответственности:
- Игровые триггеры/механики клика — уровень потребителя; пакет отдаёт позицию, действия, скин, проекцию.
- Интерпретация мирового хита — пакет отдаёт сырой `RaycastHit`.
- Включение карт Input Actions — на стороне проекта (см. «Драйверы ввода»).

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Unity.InputSystem` | `InputAction`, `Mouse.WarpCursorPosition`, EnhancedTouch (прямые касания UGUI-модуля) |
| `UnityEngine.UI` / `EventSystems` | UGUI-рендер, `IPointerXxx` в хендлерах, `ExecuteEvents` для submit, `StandaloneInputModule`/`BaseInput` для UGUI-модуля |
| `Vortex.Unity.InputBusSystem` | `InputController` — резолв экшенов по id «Карта/Экшен», LIFO-подписка |
| `Vortex.Core.LoaderSystem` | `IProcess`/`Loader` — подключение драйверов в пайплайне загрузки |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` — авто-провижен `InputDriverSet` в `Resources/Settings` |
| `Vortex.Unity.AppSystem` | `TimeController.Accumulate` — покадровые петли (тик драйверов, сопровождение фокуса) |
| `Vortex.Sdk.SdkSettingsSystem` | тоггл модуля + `DefineSymbol("USING_VORTEX_CURSOR")` |
| `Vortex.Core.Extensions.ReactiveValues` / `Vortex.Unity.Extensions.ReactiveValues` | `ReactiveValue<T>`, `StringData`, `BoolData`, `Vector2Data` с owner-защитой |
| `ru.vortex.unity.ui.misc` | `UIStateSwitcher` (скины `UiCursorRenderer`), `TweenerHub` (`PointerActionTweenerHandler`) |
| `Vortex.Unity.EditorTools` | `[AutoLink]`, `[ValueSelector]` (дропдаун id экшенов), `[ToggleButton]` |
| Sirenix Odin Inspector | атрибуты инспектора, `[SerializeReference]`-список драйверов |

Драйверы **не** используют `InputActionProperty`: биндинг задаётся строковым id экшена и резолвится в рантайме через `InputController`.

---

## Архитектура

```
[VirtualCursorBus] (static, фасад)
 ├─ PointerModel Data      ← владелец полей: VirtualCursorController
 ├─ FocusModel   Focus     ← владелец поля:  VirtualCursorFocusController
 ├─ GestureModel Gesture   ← владелец поля:  VirtualCursorController
 └─ const HideCursor = "hidden"

[GestureModel] (IReactiveData, runtime, НЕ Save)
 └─ GestureFrameData     Frame            ← импульс: Fingers · Phase · Center · Pan · Spread · Scale · Rotation

[PointerModel] (IReactiveData, runtime, НЕ Save)
 ├─ Vector2Data          ScreenPosition   ← истина позиции
 ├─ PointerActionMaskData Actions         ← битовая маска одновременных действий
 ├─ StringData           Skin             ← итоговый скин ("hidden" / запрошенный / "" = базовый)
 ├─ BoolData             IsOverUI
 ├─ Vector2Data          ScrollDelta      ← импульс скролла
 ├─ StringData           LastDevice       ← layout устройства последнего ввода ("" — не определено)
 └─ event OnPointerMoved                  ← значимый сдвиг (фильтр дрейфа)

[VirtualCursorController] (static, partial)
 ├─ ReportPointer(pos, hidesCursor, device) · ReportDevice(device) · SetAction(action, active, source) · ClearActions · SetOverUI · ReportScroll · ReportGesture   (internal — драйверы)
 ├─ OnSkin(skin, owner) / OffSkin(owner)  — расширения PointerModel, LIFO-стек запросов скинов
 └─ Projection: RegisterCamera/UnregisterCamera (LIFO) · TryGetWorldHit · GetWorldProjection · InvalidateProjection

Слой ввода:
  [InputDriverSet] (SO, ICoreAsset)  → [SerializeReference] InputDriver[]   (Resources/Settings)
  [CursorInputLoader] (IProcess)     → Loader.Register · Resources.Load + failfast · connect по платформе
                                        · тик через Accumulate (+анти-спам) · Disconnect на App.OnExit
  [InputDriver] (POCO, abstract): Connect/Disconnect · NeedsTick/Tick · HidesCursor · SupportsPlatform
     ├─ MouseInputDriver   (позиция мыши, без своего фильтра)
     ├─ DirectInputDriver  (вектор движения, NeedsTick, speedCurve по времени удержания × stick², варп ОС-мыши)
     ├─ ActionInputDriver  (кнопки → биты Actions)
     ├─ ScrollInputDriver  (4 направления → ScrollDelta: пульс + удержание)
     ├─ TouchInputDriver   (HideOnly / AbsolutePosition / Delta + платформенный фильтр)
     ├─ MultiTouchInputDriver (палец → экшен: позиция курсора или действие маски + платформенный фильтр)
     ├─ GestureInputDriver (кадр жеста → Gesture.Frame; Tap/Hold N пальцев → действие маски)
     └─ UINavigationDriver (4 направления → Navigate, submit → Submit)

Фокус-навигация (Focus/):
  [FocusModel]  → стек групп с приоритетами · ActiveGroup (первая не-Ignore сверху) · CurrentFocus
  [FocusGroup]  (MonoBehaviour) → push/pop на OnEnable/OnDisable · Priority · Ignore · Targets · RememberedFocus
  [VirtualCursorFocusController] (static) → PushGroup/RemoveGroup · Navigate · Submit
                                            · выход из режима навигации на сдвиге курсора · сопровождение цели
  [IFocusTarget] (контракт) → ScreenPoint · IsActive · NotifyFocused/NotifyUnfocused · Target

Сценовые компоненты (Handlers/):
  CursorHoverZone · CameraProvider · FocusTargetComponent · FocusCenterScrollRect
  PointerActionHandler · PointerActionTweenerHandler

Рендер (Render/): UiCursorRenderer (UGUI + UIStateSwitcher) | OsCursorRenderer (Cursor.SetCursor)

UGUI-модуль (UiModule/):
  [VirtualCursorInputModule] (StandaloneInputModule на EventSystem) → штатная обработка UGUI · прямые касания по платформе
  [VirtualCursorBaseInput]   (BaseInput) → «мышь» UGUI = PointerModel · касания из EnhancedTouch
```

### Поток данных

```
Источник (мышь/стик/тач/клавиши)
   → InputDriver (экшен по id через InputController)
        → VirtualCursorController.ReportPointer / SetAction / ReportScroll / ReportGesture
             → PointerModel (ScreenPosition / Actions / Skin / ScrollDelta)
             │    ├→ рендер (позиция + скин)
             │    ├→ VirtualCursorFocusController (OnPointerMoved → выход из режима навигации)
             │    ├→ PointerActionTweenerHandler (Actions → твинеры)
             │    ├→ Projection (по запросу) → RaycastHit
             │    └→ VirtualCursorBaseInput → VirtualCursorInputModule → UGUI-события (hover/click/drag/scroll)
             └→ GestureModel (Frame) → потребители жестов (зум, панорамирование, поворот)
```

Инициализация: `VirtualCursorController` и `VirtualCursorFocusController` поднимаются через `[RuntimeInitializeOnLoadMethod]`; модели шины создаются статически и доступны с первого обращения.

---

## Ключевые концепции

### Драйверы ввода как подключаемый модуль
- `InputDriver` — абстрактный **POCO**: `Connect()`/`Disconnect()`, `NeedsTick`/`Tick(dt)`, `HidesCursor`, `SupportsPlatform(platform)`. Экшены резолвятся по строковому id «Карта/Экшен» через `InputController` (`[ValueSelector]`-дропдаун в инспекторе).
- **Карты экшенов драйверы не включают.** Включение карты — на стороне проекта (например, `InputMapHandler` на сцене); без включённой карты события экшенов не приходят, а опрос (`ReadValue`) возвращает ноль.
- `InputDriverSet` — SO-список драйверов (`[SerializeReference]`), `ICoreAsset` → авто-создаётся в `Resources/Settings/InputDriverSet.asset`.
- `CursorInputLoader` — `IProcess`: регистрируется в `Loader`, в `RunAsync` грузит сет, подключает драйверы под текущую платформу, заводит покадровый тик для `NeedsTick`. **Failfast**: модуль включён, а ассета нет, список пуст или ни один драйвер не поддерживает текущую платформу → исключение. На `App.OnExit` все драйверы отключаются.
- Пустой или неразрезолвленный id экшена — `LogError` «Ошибка настройки драйвера управления» в `Mouse`/`Direct`/`Action`/`Touch`/`MultiTouch`/`Gesture`/`UINavigation`-драйверах; `ScrollInputDriver` молча выключает направление.

### Арбитраж источников и скрытие
`ReportPointer(pos, hidesCursor)` задаёт позицию (всегда) и видимость: скрывающий источник скрывает курсор на любом репорте, нескрывающий возвращает его только значимым сдвигом (фильтр дрейфа, см. ниже) — дрожание и эхо варпа скрытие не снимают. Драйвер объявляет `HidesCursor` (у `TouchInputDriver` в HideOnly/AbsolutePosition → `true`, в Delta → `false`; у `MultiTouchInputDriver` и `GestureInputDriver` — поле `hidesCursor`); фокус-навигация передаёт свой флаг `hideCursor`. Значимый сдвиг от другого источника (мышь, стик) возвращает курсор.

### Скин курсора
Итоговый `Skin` сводит контроллер (`GetSkinName`):
1. Курсор скрыт последним источником → `VirtualCursorBus.HideCursor` (`"hidden"`).
2. Иначе — верхний запрос LIFO-стека скинов.
3. Иначе — пустая строка (базовый скин рендера).

Запросы: `VirtualCursorBus.Data.OnSkin(skin, owner)` снимает прежний запрос владельца и кладёт новый наверх; `OffSkin(owner)` снимает. Все три писателя `Skin` (`ReportPointer`, `OnSkin`, `OffSkin`) идут через одно правило — скрытый курсор остаётся скрытым при любых запросах.

### Зоны наведения (`CursorHoverZone`)
По `PointerEnter` запрашивает свой скин, по `PointerExit`/`OnDisable` снимает. **Правило размещения: одна зона — один свой рейкаст.** UGUI шлёт `PointerEnter` цели рейкаста и её предкам, поэтому зона не должна быть предком рейкаст-объекта другой зоны (иначе две зоны на один рейкаст, порядок их запросов не определён). Вложенные зоны раскладываются сиблингами, каждая со своим `Graphic` (`raycastTarget = true`).

### Интеграция с UGUI
UGUI получает ввод из модели курсора, а не из устройств: `VirtualCursorInputModule` (наследник `StandaloneInputModule`) ставится на EventSystem вместо `InputSystemUIInputModule`, его источник ввода `VirtualCursorBaseInput` отдаёт:
- позицию — `ScreenPosition`;
- кнопки 0/1/2 — `BaseClick`/`ContextClick`/`AltClick` маски `Actions`; фронты нажатия и отпускания фиксируются по событиям маски и держатся до конца кадра модуля — нажатие и отпускание в одном кадре не теряются;
- скролл — сумму импульсов `ScrollDelta` за кадр;
- оси и кнопки навигации Selectable — 0/false (навигацию заменяет фокус-подсистема пакета).

Рейкаст, Enter/Exit, Down/Up/Click, порог drag и scroll — штатная логика `StandaloneInputModule`. Поэтому клик, hover и drag работают от любого источника, который драйверы сводят в модель (мышь, геймпад, клавиатура), без физической мыши (консоли) и без варпа ОС-курсора. Старый `UnityEngine.Input` не вызывается (кроме IME — как у стандартного `BaseInput`), модуль совместим с режимом «только Input System». Экшены у модуля не настраиваются.

**Касания.** `nativeTouch` + `nativeTouchPlatforms` (`TouchPlatformFilter`, дефолт `MobileOnly`): на подходящей платформе модуль включает EnhancedTouch и отдаёт касания UGUI напрямую — мультитач, каждый палец отдельным указателем; в кадре с касаниями курсорная часть не обрабатывается. На остальных платформах касания в UGUI не идут — тач-экран работает через драйверы (например, `TouchInputDriver` в Delta как трекпад).

**Не поддерживается:** навигация Selectable, перо (давление/наклон), XR-указатели.

Варп ОС-мыши (`DirectInputDriver.warpSystemMouse`, `TouchInputDriver` в AbsolutePosition, фокус-навигация) для UI больше не нужен — он держит ОС-курсор рядом с виртуальным, чтобы физическая мышь продолжала с его места.

**Фильтр дрейфа** — на входе репорта позиции (`VirtualCursorController.ReportPointer`), общий для всех источников, драйверы своих фильтров не держат. Позиция пишется всегда; сдвиг значим, если за один репорт больше 3 px (`DriftStepSqr` = 9 px²) или от центра дрейфа больше 5 px (`DriftRadiusSqr` = 25 px²); центр переносится в точку значимого сдвига. Значимый сдвиг снимает скрытие нескрывающим источником и поднимает `PointerModel.OnPointerMoved` (после позиции и скина, синхронно внутри репорта). Быстрое движение значимо на каждом шаге, медленное — по накоплении радиуса, дрожание на месте — никогда. Свои переносы курсора (переход фокуса, сопровождение) событие тоже поднимают — подписчик отсекает их сам.

Сдвиг накапливается только в пределах одного источника — устройства ввода репорта (переносы без устройства, например фокус, — отдельный источник). Репорт с другого источника, чем прошлый, незначим и переякоривает фильтр: центр дрейфа переносится в его точку. Иначе источник получал бы в зачёт накопление чужого — эхо варпа ОС-мыши добирало бы порог за медленным стиком и засчитывалось мыши. Цена: пока эхо варпа перемежает репорты стика (каждые несколько кадров), медленный стик (меньше 3 px за кадр) радиус не набирает и `OnPointerMoved` не поднимает — из режима навигации и из скрытия выводит уверенное отклонение.

Эхо варпа ОС-мыши приходит с ошибкой округления до пикселя ОС (≈0.7 px; при масштабированном окне, например Game view меньше разрешения рендера, — до размера пикселя ОС, ≈2 px по оси). Эхо идёт после репорта источника варпа, поэтому незначимо по смене источника — не снимает скрытие, не выводит из режима навигации и не меняет `LastDevice`; допуск сопровождения фокуса (2 px) выше ошибки округления в окне без масштаба — эхо, сдвинувшее курсор на долю пикселя, не вызывает повторный варп.

**Последнее устройство ввода** — `PointerModel.LastDevice`: layout устройства последнего пользовательского ввода (`Mouse`, `Keyboard`, `DualShock4GamepadHID`, `Touchscreen`…), пусто — не определено. Пишет контроллер (`ReportDevice`), драйверы передают устройство своего ввода (`InputDriver.DeviceOf(action)` — устройство активного контрола экшена): нажатие экшена (`ActionInputDriver`, шаг `UINavigationDriver` и submit, скролл, нажатия мультитача и жестов) — всегда; движение указателя — на значимом сдвиге и на репорте скрывающего источника; `DirectInputDriver` — на каждом тике вне мёртвой зоны, мимо фильтра дрейфа. Переносы курсора без устройства (фокус) его не меняют. Тот же layout — без события. Поле общее: на нём строятся, например, глифы управления по последнему устройству (`InputActionView` пакета RebindSystem).

`PointerActionHandler` ловит стандартные UGUI-события от любого источника и разделяет их по кнопке (Left/Right/Middle → `BaseClick`/`ContextClick`/`AltClick`). Событие чужой кнопки передаёт дальше по иерархии от родителя — обработчик на потомке `Button` не глушит её клик; своя кнопка поглощается.

### Маска действий
`PointerAction`: `None`, `BaseClick` (LMB), `ContextClick` (RMB), `AltClick` (MMB). **Порядок не менять** — индекс = бит маски и приоритет доминанты. `PointerActionMask` — `readonly struct` над `int`: одновременные действия битами, `Dominant()` — активное с наименьшим индексом. `None` битом не представлен и никогда не активен. Бит держится набором держателей: источник (привязка `ActionInputDriver`, палец `MultiTouchInputDriver`) берёт действие и отпускает его от своего имени, бит активен, пока держатель есть хотя бы один — отпускание одного из двух экшенов или пальцев на одно действие его не снимает. Взятие и отпускание идемпотентны по источнику. `ActionInputDriver` берёт по `performed`, отпускает по `canceled` (при потере фокуса окна Input System шлёт `canceled` — залипания нет).

### DirectInputDriver: скорость
`speed × speedCurve(holdTime) × stick²`, направление — нормализованный вектор движения. `holdTime` — секунды удержания стика вне мёртвой зоны (граница входит в зону, так что при `deadzone = 0` стик в покое курсор не двигает): в ней мгновенно сбрасывается в 0, вне её растёт до времени последнего ключа кривой и дальше не копится. Кривая целиком задаёт профиль разгона: X — секунды, Y — множитель к `speed`; при кривой-константе разгона нет. `stick` — отклонение, перенормированное от края мёртвой зоны (0 на границе, 1 при полном), в квадрате — точность на малых отклонениях. На `Connect` — `LogWarning`, если последний ключ кривой ≈ 0; `deadzone` больше 0.5 — `LogWarning`, 1 и выше — `LogError` (стик не двигает курсор). Тик — на `unscaledDeltaTime`, работает и на паузе. Позиция клампится в экран. Устройство стика уходит в `LastDevice` на каждом тике вне мёртвой зоны — отклонение за ней намеренное, фильтр дрейфа для выбора устройства не нужен.

### ScrollInputDriver: пульс + удержание
Четыре Button-экшена направлений. **Пульс** — на каждый `performed` импульс `pulseStep` (колесо мыши). **Удержание** — каждый кадр, пока направление зажато, `holdSpeed × dt` (D-pad, клавиши, стик как Button). Любую модель выключает 0 в её поле. Импульсы публикуются в `PointerModel.ScrollDelta` (импульс, равный предыдущему, публикуется принудительно — щелчки колеса подряд не теряются); в UGUI их доносит `VirtualCursorInputModule`.

### TouchInputDriver: режимы и платформенный фильтр
- **HideOnly** (дефолт) — касание только скрывает курсор; репортится текущая позиция курсора (без сдвига — уходит только флаг скрытия), значение экшена не читается. Сам клик — прямым касанием `VirtualCursorInputModule` (`nativeTouch` на платформе). Для Android.
- **AbsolutePosition** — курсор прыгает в точку касания и скрывается; опционально варп ОС-мыши.
- **Delta** — трекпад: дельта пальца (× `deltaSpeedMultiplier`) сдвигает курсор, курсор виден, клампится в экран. Варпа нет. Для десктоп-тачскринов.

`TouchPlatformFilter`: `All` / `MobileOnly` (Android/iOS, в редакторе не подключается) / `DesktopOnly` (Standalone + все редакторы). Пара HideOnly + Delta разводится фильтром по платформам — иначе на мобильных Delta (не скрывает) перебивал бы HideOnly.

### MultiTouchInputDriver: пальцы мультикасания
Привязки «палец → экшен». Палец (`TouchFinger`) — порядок прикосновения, слот `Touchscreen.touches[i]` Input System. Экшен биндится на касания целиком (`<Touchscreen>/touch*/position`, `<Touchscreen>/touch*/press`); каждый кадр драйвер берёт его контрол на выбранном пальце и действует по типу контрола:
- **Vector2** (позиция) — курсор ведётся за пальцем: репорт, пока касание идёт и точка сместилась (с началом касания — сразу); скрытие по `hidesCursor`.
- **Кнопка** (press, tap) — состояние ставит или снимает `PointerAction` привязки в маске `Actions`.

Нет контрола позиции или кнопки на пальце — `LogError` один раз на привязку. Выключенный экшен (карта выключена) — касание для драйвера закончилось: действие отпускается, ведение курсора сбрасывается. На `Disconnect` снимает только свои действия. Платформы — `TouchPlatformFilter`. При `VirtualCursorInputModule.nativeTouch` на той же платформе курсорная часть модуля в кадрах с касанием не обрабатывается — действия драйвера до UGUI не дойдут.

### GestureInputDriver: жесты мультикасания
Касания — через экшен на `<Touchscreen>/touch*/position` (контролы идущих касаний, по `touchId`). Два слоя:

**Кадр жеста** (`VirtualCursorBus.Gesture.Frame`, `GestureFrame`) — пока на экране два и больше пальцев:

| Поле | Смысл |
|---|---|
| `Fingers` | число пальцев жеста |
| `Phase` | `Began` — набралось число пальцев (дельты нейтральные) · `Changed` — кадр с изменением · `Ended` — пальцы подняты или набор пальцев изменился |
| `Center` | центр пальцев, px |
| `Pan` | сдвиг центра за кадр, px — панорамирование |
| `Spread` | среднее расстояние пальцев до центра, px |
| `Scale` | `Spread` / `Spread` прошлого кадра: >1 — расширение, <1 — сужение (зум, не зависит от разрешения) |
| `Rotation` | поворот пальцев вокруг центра за кадр, градусы (против часовой — положительный) |

Смена набора пальцев посреди жеста завершает его (`Ended`) и сразу начинает новый (`Began`) — без скачка центра и размаха. Каналы `Pan`/`Scale`/`Rotation` молчат (нейтральные 0/1/0), пока накопленное с начала жеста изменение не превысит порог (`panThreshold` px, `scaleThreshold` доля, `rotationThreshold` градусы); `Changed` публикуется только при ненейтральной дельте. Кадр — импульс: равный предыдущему публикуется принудительно. Толкование (зум камеры, прокрутка карты, поворот) — на потребителе.

**Распознанные жесты → маска** — привязки `{ fingers (2–5), kind, action, moveCursorToCenter }`:
- **Tap** — от первого касания до отрыва последнего пальца не дольше `tapMaxDuration`, ни один палец не сдвинулся дальше `tapMaxMovement`; число пальцев — максимум одновременных. Действие берётся и отпускается в одном кадре (клик — `VirtualCursorInputModule` фронты не теряет).
- **Hold** — действие держится, пока идёт жест ровно с этим числом пальцев (тап тем же числом пальцев тоже кратко его возьмёт).
- `moveCursorToCenter` — перед взятием курсор переносится в центр жеста (скрытие по `hidesCursor`): клик придётся по месту касания.

Действия берутся от имени привязки (набор держателей). `Disconnect` завершает идущий жест и отпускает удержания. Выключенный экшен — касаний нет: жест завершается, удержания отпускаются, незаконченный тап сбрасывается (ложного тапа нет).

### Тик драйверов
Драйверы с `NeedsTick` тикаются самоперепланирующейся петлёй через `TimeController.Accumulate`. Внутренний `catch` изолирует сбойный драйвер, `finally` гарантирует продолжение; исключение логируется только на первое в серии, счётчик сбрасывается на первом успешном кадре.

### Фокус-навигация
**Стек групп.** `FocusGroup` на `OnEnable` встаёт в стек с учётом `Priority` (0..10): группа поднимается к верху, минуя группы со строго меньшим приоритетом; между равными — LIFO. `ActiveGroup` — первая сверху группа без `Ignore`; только её `Targets` участвуют в навигации. `OnDisable` снимает группу, `OnDestroy` — финальная очистка. Изменение `Priority` в рантайме применяется на следующем `OnEnable`. `Ignore` в рантайме уведомляет контроллер: если текущий фокус оказался вне активной группы — он уходит в `RememberedFocus` своей группы; если фокуса нет — запрашивается авто-передача (в режиме навигации), как при push/pop.

**Регистрация целей.** `FocusTargetComponent` (`TargetKind.UGUI` — центр `RectTransform` с учётом render mode канваса; `TargetKind.World` — проекция `Transform` через активную камеру `CameraProvider`) на каждом `OnEnable` ищет ближайшую родительскую `FocusGroup` и регистрируется; без группы — `LogError` (с анти-спамом). `Unregister` чистит `RememberedFocus` группы, только если цель уничтожена.

**Navigate.** Полуоткрытый конус `[-cone..+cone)` от точки текущего фокуса (без фокуса — от позиции курсора); среди попавших — минимум евклидовой дистанции; текущий фокус исключается. `captureNearestIfFree`: без фокуса и без кандидатов в конусе берётся ближайшая цель группы. Переход фокуса: `NotifyUnfocused`/`NotifyFocused` (UnityEvent'ы цели), запоминание в `RememberedFocus` групп, репорт позиции с флагом скрытия и варп ОС-мыши.

**Режим навигации** — отдельный флаг: включается переходом фокуса, выключается только сдвигом курсора пользователем или опустевшим стеком групп. Переживает временную потерю фокуса (уход в `RememberedFocus` при push, отключение фокусной цели) — на нём держится авто-передача фокуса при push/pop/`Ignore`: новой активной группе отдаётся её `RememberedFocus` или ближайшая к курсору цель. Передача отложена до ближайшего тика петли сопровождения (`LateUpdate`): на `OnEnable` группы её дочерние цели ещё не зарегистрированы, а смена меню в одном кадре не роняет режим на промежуточном пустом стеке. Явный `Navigate` в этом окне отменяет отложенную передачу. Значимый сдвиг курсора (`PointerModel.OnPointerMoved`) завершает режим и снимает фокус; дрожание и эхо варпа — нет. Свои переносы курсора контроллер отсекает флагом на время собственного репорта.

**Уведомления модели.** `FocusModel.OnUpdateData` батчится через `TimeController.Accumulate`: серия изменений за вызов (стек → снятие фокуса → новая цель) даёт одно событие в `LateUpdate` с итоговым состоянием. `CurrentFocus.OnUpdate` остаётся синхронным.

**Сопровождение.** Покадровая петля переносит курсор (и ОС-мышь) в точку цели, если та сместилась (скролл, лейаут, анимация) — с тем же флагом скрытия.

**Submit.** `VirtualCursorFocusController.Submit()` эмулирует клик левой кнопкой в точке цели так, будто рейкаст попал в `IFocusTarget.Target` текущего фокуса: `PointerDown` с подъёмом по иерархии до ближайшего обработчика нажатия (без него получатель — ближайший `IPointerClickHandler`), `PointerUp` получателю нажатия, `PointerClick` ближайшему по иерархии `IPointerClickHandler`. Работают обработчики, фиксирующие клик по нажатию/отпусканию/времени, и цель ниже обработчика (зона нажатия кнопки). Без фокуса — ничего.

**Обработчики фокуса.** Смена фокуса (`Navigate`) из `onFocused`/`onUnfocused` отклоняется с `LogError`. Изменения стека групп и регистрации целей оттуда (включение/выключение групп и целей, `Ignore`) не теряются: они откладываются и выполняются по порядку после завершения текущей операции контроллера (не посреди перехода фокуса), каждое перепроверяет сцену на момент выполнения — push пропускается, если группа уже выключена, remove — если снова включена.

`FocusCenterScrollRect` (на `ScrollRect`) центрирует контент на фокусном элементе-потомке `content`: по разрешённым осям, с порогом `axisTolerance` относительно предыдущего фокуса, мгновенно или анимацией на unscaled-времени.

### Твинеры по клавишам (`PointerActionTweenerHandler`)
Глобально подписан на `Actions`: для каждой привязки «действие → `TweenerHub`» нажатие → `Forward`, отпускание → `Back`; на `OnEnable` твинеры мгновенно выравниваются по текущей маске. Привязка к `None` — «покой»: активна, пока не нажата ни одна из клавиш, **привязанных в этом обработчике**; клавиши без привязки покой не сбивают. Её твинер (айдл курсора) в `Forward` без нажатий, уходит в `Back` с нажатием привязанной клавиши и возвращается в `Forward` с отпусканием последней привязанной. Сама маска не меняется: `None` в ней по-прежнему не бывает активен.

Пример — привязки `None → [idle]`, `BaseClick → [click]` (ПКМ не привязана):

| Событие | `[idle]` | `[click]` |
|---|---|---|
| Включение, ничего не нажато | Forward (мгновенно) | Back (мгновенно) |
| Нажали ЛКМ | Back | Forward |
| Отпустили ЛКМ | Forward | Back |
| Нажали ПКМ (не привязана) | остаётся Forward | остаётся Back |

Зацикленность анимации (бесконечный айдл) задаётся настройками самого `TweenerHub` — обработчик только переключает `Forward` / `Back`.

### Рендеры
- **`UiCursorRenderer`** — RectTransform курсора на оверлей-канвасе ставится в `ScreenPosition`; скин — состояние `UIStateSwitcher` с тем же именем (неизвестное имя → состояние 0). Для скрытия нужно состояние с именем `hidden`. Позиция и скин применяются начиная с первого репорта позиции: до него `OnEnable` ставит состояние `hidden` (нет такого — состояние 0), после него повторное включение сразу применяет текущие позицию и скин модели. Опционально скрывает системный курсор.
- **`OsCursorRenderer`** — `Cursor.SetCursor` (ForceSoftware) по набору спрайтов скина: первый набор — скин по умолчанию, флаг `hidden` прячет ОС-курсор, спрайт выбирается по `Actions.Dominant()` (`actionsOverride[0..2]` с фолбэком). Перерисовывается на смене `Skin`. Требует standalone-текстуру спрайта (не атлас).

### Screen→world проекция
LIFO-реестр камер (`CameraProvider` на OnEnable/OnDisable); `TryGetWorldHit`/`GetWorldProjection` — ленивый `Physics.Raycast` из `ScreenPosition` с кэшем на кадр; кэш сбрасывается сменой позиции курсора и реестра камер, вручную — `InvalidateProjection` (сдвинулась геометрия сцены).

### Editor-инструментарий
- **`Tools/Vortex/Virtual Cursor/Focus Stack`** — окно стека групп: приоритет (`P<n>`), статус (`▶ ACTIVE` / `⊘ IGNORED` / глубина), `Targets` с маркерами `●` текущий фокус, `◉` запомненный, `[inactive]`; клик — Ping + Select. Только в Play Mode; авто-перерисовка не чаще раза в секунду, вне Play Mode тик отключён.
- **`Tools/Vortex/Configs/Input Driver Set`** — подсветить `InputDriverSet` в Project.

---

## Контракт

### Вход
- `SdkSettings`: включён `cursorInputSdk` (`USING_VORTEX_CURSOR`).
- `InputDriverSet` в `Resources/Settings`: непустой список драйверов с назначенными id экшенов.
- Input Actions: экшены под драйверы; карты включаются проектом.
- `EventSystem` + `VirtualCursorInputModule` (вместо `InputSystemUIInputModule`) в сценах с UI.
- Рендер курсора на оверлей-канвасе (`UiCursorRenderer`) или `OsCursorRenderer`.

### Выход
- `PointerModel` (позиция / действия / скин / над-UI / скролл / последнее устройство) — реактивно.
- UGUI-события (hover/click/drag/scroll) в позиции курсора от любого источника — через `VirtualCursorInputModule`; прямые касания — по платформе.
- `FocusModel` (стек групп, текущий фокус) — реактивно.
- `GestureModel` (кадр жеста мультикасания) — реактивно.
- `RaycastHit`/точка проекции по запросу.

### Гарантии
- Одна экранная позиция для рендера, UGUI-ввода, фокуса и проекции.
- Одновременность действий; одиночная доминанта для спрайта.
- Скрытие от источника имеет приоритет над запросами скинов.
- Тик драйверов переживает сбой одного драйвера.
- Поля моделей `ScreenPosition`/`Actions`/`Skin`/`IsOverUI`/`ScrollDelta`/`LastDevice`/`CurrentFocus`/`Gesture.Frame` закрыты на контроллеры.

---

## API

### VirtualCursorBus (static)
```csharp
const string HideCursor = "hidden";
static PointerModel Data;      // runtime-модель указателя
static FocusModel   Focus;     // runtime-модель фокус-навигации
static GestureModel Gesture;   // runtime-модель жестов мультикасания (Frame)
```

### VirtualCursorController (static)
```csharp
// Скины (расширения PointerModel): VirtualCursorBus.Data.OnSkin(...)
static void OnSkin(this PointerModel model, string skin, Object skinOwner);
static void OffSkin(this PointerModel model, Object skinOwner);

// Проекция
static bool     TryGetWorldHit(out RaycastHit hit);
static Vector3? GetWorldProjection();
static void     InvalidateProjection();

// internal (драйверы/компоненты пакета):
// ReportPointer(pos, hidesCursor, device) · ReportDevice(device) · SetAction(action, active, source) · ClearActions · SetOverUI
// ReportScroll · ReportGesture
// RegisterCamera / UnregisterCamera · ActiveCamera
```

### VirtualCursorFocusController (static)
```csharp
static bool IsReady;
static void Init();                                         // [RuntimeInitializeOnLoadMethod]
static void PushGroup(FocusGroup group);                    // из FocusGroup.OnEnable
static void RemoveGroup(FocusGroup group);                  // из OnDisable/OnDestroy
static void Navigate(Vector2 direction, float coneAngleDeg,
                     bool hideCursor, bool captureNearestIfFree);
static void Submit();                                       // эмуляция клика (Down/Up/Click) в Target текущего фокуса
```

### FocusGroup (MonoBehaviour)
```csharp
IReadOnlyList<IFocusTarget> Targets;
int  Priority;                         // 0..10
bool Ignore { get; set; }              // setter уведомляет контроллер
IFocusTarget RememberedFocus;
void Register(IFocusTarget target);
void Unregister(IFocusTarget target);
void ClearRememberedFocus();
```

### IFocusTarget (контракт)
```csharp
Vector2    ScreenPoint { get; }   // пересчитывается при каждом запросе
bool       IsActive    { get; }   // готов принимать фокус
void       NotifyFocused();
void       NotifyUnfocused();
GameObject Target      { get; }   // точка «рейкаста» клика от Submit
```

### InputDriver (abstract, POCO)
```csharp
abstract void Connect();
abstract void Disconnect();
virtual  bool NeedsTick { get; }
virtual  void Tick(float unscaledDeltaTime);
virtual  bool HidesCursor { get; }
virtual  bool SupportsPlatform(RuntimePlatform platform);
// helpers: ResolveAction / SubscribeAction / UnsubscribeAction / Report(screen, device) / ReportDevice(device) / DeviceOf(action)
```

---

## Использование

### 1. Включить модуль
В `SdkSettings` включить `cursorInputSdk` → **ApplyChanges** (дефайн `USING_VORTEX_CURSOR`, пересборка).

### 2. Настроить InputDriverSet
`CoreAssetsController` авто-создаст `Resources/Settings/InputDriverSet.asset`. Добавить драйверы, назначить id экшенов из дропдауна. Для Android + Desktop — два `TouchInputDriver`: `HideOnly` + `MobileOnly` (Button `<Touchscreen>/primaryTouch`) и `Delta` + `DesktopOnly` (Value `<Touchscreen>/delta`).

### 3. Input Actions
Экшены под драйверы (позиция мыши, вектор движения, кнопки, направления навигации, submit, скролл, касание). Для `MultiTouchInputDriver` и `GestureInputDriver` — экшены на касания целиком: `<Touchscreen>/touch*/position`, для кнопок пальцев — `<Touchscreen>/touch*/press`. Карту виртуального курсора включить на стороне проекта (например, `InputMapHandler` на постоянной сцене). Экшены кнопок `ActionInputDriver` — тип **Button** (по `performed` действие ставится, по `canceled` снимается); с PassThrough отпускание тоже приходит как `performed`, и действие залипает. Клик по UI — тот же экшен: UGUI-модуль пакета читает маску действий, отдельный экшен для UI не нужен.

### 4. Сцена
- На `EventSystem` — `VirtualCursorInputModule` вместо `InputSystemUIInputModule` (`VirtualCursorBaseInput` добавится сам). Для мобильных — `nativeTouch` с `MobileOnly`.
- Оверлей `Canvas` (Screen Space - Overlay, поверх всего UI) + курсор с `UIStateSwitcher` (состояние 0 — скин по умолчанию, `hidden` — скрытый, остальные по именам скинов) + `UiCursorRenderer`. Raycast у графики курсора отключить.
- Опционально: `PointerActionTweenerHandler` на курсоре (анимация нажатия), `CameraProvider` на камере, `CursorHoverZone` на элементах UI (одна зона — один рейкаст), `PointerActionHandler` для RMB/MMB.

### 5. Фокус-навигация (опционально)
- В `InputDriverSet` — `UINavigationDriver`: 4 направления + submit, `coneAngleDeg`, `hideCursorOnFocus`, `captureFocusIfFree`.
- `FocusGroup` на корне каждого контекста навигации (HUD, меню, диалог); переключение контекстов — `SetActive`.
- `FocusTargetComponent` на каждом фокусируемом элементе внутри группы; `onFocused`/`onUnfocused` — подсветка/SFX. Для кнопки `FocusTargetComponent` ставится на объект с обработчиком клика или ниже него (submit поднимается по иерархии, как рейкаст).
- `FocusCenterScrollRect` на `ScrollRect`-списках.

---

## Известные проблемы

| Проблема | Где |
|---|---|
| `IsOverUI` публикуется, но в пакете его никто не пишет и не читает | `PointerModel` |
| `Skin` не транслируется в `PointerModel.OnUpdateData` | `PointerModel` |
| `OsCursorRenderer` не перерисовывается на смене `Actions`; падает при пустом `cursorSets`, `actionsOverride` короче трёх и `idle == null` | `OsCursorRenderer` |
| World-цель без камеры: `ScreenPoint` бросает NRE, а при наличии фокуса он читается без проверки `IsActive` | `FocusTargetComponent`, `VirtualCursorFocusController` |
| `InputController` отдаёт `canceled` только верхнему подписчику экшена: если между нажатием и отпусканием на тот же экшен подписался другой, отпускание до драйвера не дойдёт — действие залипнет | `InputController` (ядро Vortex), `ActionInputDriver`, `UINavigationDriver` |
| Содержимое `Example/` отстаёт от пакета (нет `VirtualCursorInputModule`, старые поля драйверов) | `Example/` |

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Модуль выключен | Сборка пакета не компилируется; компоненты в сценах — Missing Script |
| `InputDriverSet` отсутствует / пуст | `CursorInputLoader` кидает исключение |
| Ни один драйвер набора не поддерживает текущую платформу | `CursorInputLoader` кидает исключение |
| Драйвер не поддерживает платформу | Не подключается |
| Карта экшенов не включена | События не приходят, опрос возвращает ноль — курсор от этого источника не двигается |
| Касание в HideOnly / AbsolutePosition | Курсор скрыт до значимого сдвига от другого источника |
| Пара HideOnly+Delta с `All` | Delta перебивает скрытие — разводить по платформам |
| Касание при `nativeTouch` на платформе | Идёт в UGUI напрямую (мультитач); курсорная часть модуля в этом кадре не обрабатывается |
| Касание без `nativeTouch` на платформе | В UGUI не идёт — только через драйверы |
| `MultiTouchInputDriver`: у экшена нет контрола на пальце | `LogError` один раз, привязка молчит |
| `MultiTouchInputDriver` + `nativeTouch` на одной платформе | Касание уходит в UGUI напрямую, действия драйвера в UGUI не доходят |
| `MobileOnly` в редакторе | Не подключается |
| Исключение в `Tick` драйвера | Лог на первое в серии; остальные драйверы тикаются |
| Скин не найден рендером | Состояние 0 (`UiCursorRenderer`) / первый набор (`OsCursorRenderer`) |
| Нет состояния `hidden` у `UiCursorRenderer` | Скрытие показывает скин по умолчанию |
| Две зоны наведения на один рейкаст | Порядок запросов скинов не определён — некорректное размещение |
| Нет `FocusGroup` в сцене | `Navigate` ничего не делает |
| `FocusTargetComponent` без родительской группы | `LogError`, цель не регистрируется |
| В конусе нет кандидатов, фокус есть | Ничего (захват ближайшего — только без фокуса) |
| Сдвиг курсора при фокусе | Значимый (`OnPointerMoved`: шаг > 3 px или от центра дрейфа > 5 px) — фокус снимается; меньше — дрейф |
| Репорт позиции с другого источника, чем прошлый | Незначим, центр дрейфа переносится в его точку |
| Эхо варпа ОС-мыши | Незначимо (идёт после репорта источника варпа) — скрытие, режим навигации и `LastDevice` не меняет |
| Медленный стик (< 3 px за кадр) при варпе ОС-мыши | `OnPointerMoved` не поднимает (эхо переякоривает фильтр); `LastDevice` — устройство стика на каждом тике |
| Перенос курсора без устройства (фокус) | `LastDevice` не меняется |
| Цель фокуса сместилась | Курсор переносится следом, если расхождение больше 2 px |
| Submit без фокуса | Ничего |
| Смена фокуса из `onFocused`/`onUnfocused` | Отклоняется с `LogError` |
| Включение/выключение группы или цели, `Ignore` из `onFocused`/`onUnfocused` | Откладывается до конца операции контроллера и выполняется |
| Проекция без камеры | Промах (`false`/`null`) |
| Атласный спрайт курсора | `UiCursorRenderer` — ок; `OsCursorRenderer` — нужна standalone-текстура |

---

## Файловая структура

```
VirtualCursorSystem/
├── Bus/VirtualCursorBus.cs
├── Controllers/
│   ├── VirtualCursorController.cs             # интейк драйверов, сведение скина
│   └── VirtualCursorController.Projection.cs  # LIFO-камеры + raycast
├── Model/
│   ├── PointerModel.cs  PointerAction.cs  PointerActionMask.cs  PointerActionMaskData.cs
│   ├── GestureModel.cs  GestureFrame.cs  GestureFrameData.cs
├── Focus/
│   ├── FocusModel.cs  FocusGroup.cs  FocusTargetData.cs  IFocusTarget.cs  VirtualCursorFocusController.cs
├── InputDrivers/
│   ├── InputDriver.cs  InputDriverSet.cs  CursorInputLoader.cs
│   ├── MouseInputDriver.cs  DirectInputDriver.cs  ActionInputDriver.cs
│   ├── ScrollInputDriver.cs  TouchInputDriver.cs  MultiTouchInputDriver.cs  GestureInputDriver.cs  UINavigationDriver.cs
├── Handlers/
│   ├── CursorHoverZone.cs  CameraProvider.cs  FocusTargetComponent.cs  FocusCenterScrollRect.cs
│   ├── PointerActionHandler.cs  PointerActionTweenerHandler.cs
├── Render/
│   ├── UiCursorRenderer.cs  OsCursorRenderer.cs
├── UiModule/
│   ├── VirtualCursorInputModule.cs  VirtualCursorBaseInput.cs
├── DefineSettings/                            # SDK-тоггл (в сборке SdkSettings через .asmref)
│   ├── SdkSettings.CursorInput.cs  sdk.settings.system.ext.asmref
├── Editor/
│   ├── FocusStackWindow.cs  MenuController.cs  VirtualCursorInputModuleProcessor.cs
├── Example/                                   # пример: Input Actions, InputDriverSet, префаб курсора, пресеты твинов
└── ru.vortex.unity.virtualcursorsystem.asmdef # defineConstraints: ["USING_VORTEX_CURSOR"]
```

# UI Misc

**Namespace:** `Vortex.Unity.UI.Misc`
**Сборка:** `ru.vortex.unity.ui.misc`

## Назначение

Утилитарные UI-компоненты общего назначения: расширенная кнопка, счётчики, слайдер с анимацией, контейнер данных, вспомогательные MonoBehaviour.

---

## Компоненты

### AdvancedButton

Расширенная кнопка с режимами клика, визуальными состояниями и корректной работой внутри `ScrollRect`. Реализует `IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`, `IPointerClickHandler`.

Визуальные состояния (через `UIStateSwitcher`): Free, Hover, Pressed.

| Режим клика | Где обрабатывается | Защита от scroll-drag |
|---|---|---|
| `OnTap` | `OnPointerDown` (мгновенный отклик) | нет (по дизайну — клик до движения) |
| `OnUpInBorders` | `OnPointerClick` (release над тем же объектом) | ✅ автоматически (Unity не вызовет `OnPointerClick`, если EventSystem распознала жест как drag) |
| `OnUpAnywhere` | `OnPointerUp` (любой release на этом объекте) | нет (по контракту режима — release где угодно) |
| `OnClick` | `OnPointerClick` + проверка времени `< TimeForClickMs (200мс)` | ✅ автоматически |

`OnUpInBorders` и `OnClick` идут через канонический `IPointerClickHandler` Unity: EventSystem сама определяет, что это клик, а не drag, по превышению `EventSystem.pixelDragThreshold`. Если кнопка лежит в `ScrollRect`, при скролле родитель получит drag, и `OnPointerClick` на кнопке не вызовется — клик корректно подавится без таймеров и собственных порогов.

Если нужен собственный порог дистанции — выставляется глобально через `EventSystem.current.pixelDragThreshold` (Unity-настройка, влияет на всё UGUI).

Events (Action): `OnClick`, `OnPressed`, `OnReleased`, `OnHover`, `OnExit`.
UnityEvents (массивы): `onClick[]`, `onHover[]`, `onExit[]`.
Внешнее управление: `Press()`, `Release()`, `AddOnClick(UnityAction)`, `RemoveOnClick(UnityAction)`.

Особенности:
- `OnPointerEnter` сохраняет визуал Pressed, если кнопка ещё прижата (сценарий «нажали → увели за пределы → вернули обратно»).
- `AddOnClick(UnityAction)` идемпотентен: повторная подписка одного и того же `UnityAction` игнорируется (`_wrappedActions`-словарь), `RemoveOnClick` корректно снимет wrapper.
- Внешний `Press()`/`Release()` работает в режиме `OnClick`: при `eventData == null` смещение считается нулевым, проверка времени остаётся.
- `OnEnable` сбрасывает внутренние флаги (`_pressed = false`), вызывает `uiStateSwitcher.ResetStates()` и переводит визуал в `Free`. Это исключает ситуацию «кнопку выключили во время удержания → при следующем включении она снова показывает Pressed». `OnDisable` лишь сбрасывает `_inBorders`, реальная инициализация состояния происходит в `OnEnable`.

### DataStorage

Универсальный контейнер данных. Реализует `IDataStorage : IDataSource`. FIFO-поиск по типу.

```csharp
storage.SetData(myModel);                    // полная замена всех данных → OnUpdateLink
storage.SetData(new[] { a, b });             // полная замена набором       → OnUpdateLink
storage.AddData(extraData);                  // добавление/замена по типу    → OnUpdateLink не вызывается
var model = storage.GetData<MyModel>();      // поиск по типу
storage.OnUpdateLink += ReBindAll;           // переподцепить все ссылки
```

`OnUpdateLink` — link-level: вызывается только при полной замене содержимого (`SetData`), которая инвалидирует ранее полученные через `GetData<T>()` ссылки. `AddData` не инвоцирует событие, поскольку существующие ссылки остаются валидными.

**Точки расширения для наследников:**
- `Data` (`protected`) — прямой доступ к внутреннему списку из подклассов. Использовать осторожно: запись мимо `SetData`/`AddData` не инвоцирует `OnUpdateLink`.
- `dataSwitcher` (`protected`) — управление визуальным состоянием из подкласса.
- `OnEnable()` / `OnDisable()` (`virtual`) — переопределяются в наследниках для подключения дополнительной логики (например, реактивного слоя). Используется, в частности, `DataStorageTransport` (`AssetCacheSystem`), который грузит префаб по addressable-ссылке в `OnEnable` и освобождает в `OnDisable`.

### DataCapturer

Late-binding мост: `MonoBehaviour`-источник + имя реактивного свойства → `IDataStorage`. Реализует `IDataStorage : IDataSource`. Конфигурируется в инспекторе; в редакторе выпадающий список свойств собирается через рефлексию по типу `IReactiveData` (включая `IntData`/`BoolData`/`FloatData`/любых наследников `ReactiveValue<T>`).

```csharp
// На префабе:
//   source   = ссылка на любой MonoBehaviour-источник
//   property = имя свойства (выбирается через ValueSelector с фильтром IReactiveData)

// Потребитель (Pool-итем, generic-handler и т.п.):
capturer.OnUpdateLink += () =>
{
    var data = capturer.GetData<IntData>();  // ссылка на ReactiveValue
    data.OnUpdate += v => UpdateView(v);     // подписка на value-level
};
```

Жизненный цикл:
- `Awake` — кеширует `PropertyInfo`. При отсутствии свойства логирует ошибку, выключает компонент (`enabled = false`) и не подписывается на источник.
- Если источник реализует `IDataSource`, подписывается на его `OnUpdateLink` для отслеживания пересоздания реактивных полей.
- `Start` — первый `RefreshLink()` после всех `Awake` сцены, потребители успевают подписаться.
- `RefreshLink` сравнивает новую и старую ссылки через `ReferenceEquals` и инвоцирует `OnUpdateLink` только при реальной смене ссылки.
- `OnDestroy` — отписка от источника, зануление кеша.

Ниша: связки, которые нельзя зашить через `[UIComponentLink]` (атрибутная привязка в коде виджета) — например, generic Pool-итемы, виджеты-шаблоны, сборка связки в инспекторе на префабе. Источник всегда `MonoBehaviour` — для `ScriptableObject`-настроек/пресетов используется прямая `[SerializeField]`-ссылка, не `DataCapturer`.

### CounterViewBase&lt;T&gt; (abstract)

Базовый компонент для отображения счётчика с минимумом/максимумом/текущим значением, слайдером, пульсацией и пороговыми визуальными состояниями.

Наследник реализует:
- `int GetValue()` — текущее значение
- `int GetMinValue()` — минимум диапазона
- `int GetMaxValue()` — максимум диапазона
- `void Init()` / `void DeInit()` — подписка/отписка наследника на доменные события

Доступ к модели через `protected T Data` (кешируется при первом обращении, сбрасывается на `UpdateLink`).

Поля инспектора (все опциональные — компонент работает с любым подмножеством):

| Группа | Поле | Что делает |
|---|---|---|
| Source | `sourceValue` | `IDataStorage` (через `ClassFilter`+`AutoLink`) — источник модели данных |
| Min Value UI | `min` (UIComponent), `patternMin = "{0}"` | Текстовый виджет минимума и его паттерн форматирования |
| Max Value UI | `max` (UIComponent), `patternMax = "{0}"` | Текстовый виджет максимума |
| Current Value UI | `value` (UIComponent), `patternValue = "{2} < {0} < {1}"` | Текстовый виджет текущего значения. Получает три аргумента: `{0}` = value, `{1}` = max, `{2}` = min |
| — | `slider` (SliderView) | Анимированный слайдер |
| — | `tweenPulsation` (TweenerHub) | Pulse-анимация на изменение значения |
| — | `switcher` (UIStateSwitcher по enum `CounterStates`) | Пороговые визуальные состояния |
| Анимации | `onUp` / `onDown` | Анимировать при росте / убывании значения |

Пороги `CounterStates` (по проценту заполнения диапазона):

| Состояние | Условие |
|---|---|
| `Empty` | `value == minValue` |
| `Less20` | < 20% диапазона |
| `Less50` | < 50% |
| `Less80` | < 80% |
| `Less100` | ≥ 80% и < `maxValue` |
| `Fill` | `value == maxValue` |

API наследника:
- `UpdateValue()` / `UpdateMinValue()` / `UpdateMaxValue()` — программная переотрисовка соответствующего блока.

### CounterViewAdvanced

Готовый наследник `CounterViewBase<T>` для типового сценария «модель с тремя `IntData` (current/min/max)». Содержит `[SerializeField]`-ссылки на свойства источника, маппит их на `GetValue`/`GetMinValue`/`GetMaxValue` и подписывается в `Init()`.

### SliderView

Анимированный Slider через `AsyncTween`.

```csharp
sliderView.Set(0.75f, 1f);   // value, max
```

| Поле | Тип | Описание |
|------|-----|----------|
| `slider` | `Slider` | Целевой слайдер |
| `delay` | `float` | Задержка перед анимацией (`[Range(0, 3)]`) |
| `duration` | `float` | Длительность анимации (0..1 сек) |
| `ease` | `EaseType` | Тип easing |

Пропускает обновление если value и max не изменились.

### AutoRectSetter

Автонастройка RectTransform через Inspector. `[ExecuteAlways]` — работает и в Editor, и в Play.

Настраиваемые параметры (каждый включается toggle):
- Borders (left, top, right, bottom, posZ)
- Anchors (anchorMin, anchorMax)
- Pivot
- Rotation (localEulerAngles)

`Apply()` — применить настройки. `ReadFromCurrent()` — захватить текущие значения RectTransform.

### EnableDelayForChild

Отложенная активация дочерних объектов.

| Поле | Тип | Описание |
|------|-----|----------|
| `delay` | `float` | Задержка (0..10 сек) |

`Awake` — деактивирует всех детей. `OnEnable` — планирует активацию через `TimeController.Call()`. `OnDisable` — деактивирует.

### ScrollRectResetHandler

Сброс `ScrollRect` в начальную позицию (`normalizedPosition = Vector2.one`) при `Start`.

### LoaderWaitingHandler

Хэндлер «не показывать UI, пока не догрузятся ресурсы». Принимает массив `MonoBehaviour[]`-объектов (в инспекторе фильтруется атрибутом `[ClassFilter(typeof(INeedDelay))]`) и `UIStateSwitcher`, привязанный к enum `DelayedState` (`Waiting`, `Ready`).

Внутри использует `DelayedObserver` (`Vortex.Core.System.Models`) — ждёт готовности всех `INeedDelay`. По завершении переключает состояние в `Ready`. На каждую ошибку пишет `Debug.LogError` с типом упавшего источника.

```
OnEnable
  ├── stateSwitcher.Set(Waiting)
  └── new DelayedObserver(delayed, OnReady, OnError, callOnError)
       ├── OnReady → stateSwitcher.Set(Ready)
       └── OnError → Debug.LogError("[LoaderWaitingHandler] loading failed for {Type}")

OnDisable
  └── observer.Dispose()
```

Параметры:
- `delayed` (`MonoBehaviour[]` + `ClassFilter`) — компоненты-источники задержки. В инспектор можно положить только объекты, реализующие `INeedDelay`.
- `callOnError` (`bool`, по умолчанию `true`) — при `true` `Ready` поднимется даже после ошибок; при `false` состояние останется `Waiting` навсегда при первой же ошибке.
- `stateSwitcher` (`UIStateSwitcher`) — должен быть сконфигурирован под enum `DelayedState`.

Типичное использование: укладывается рядом с UI-блоком, который зависит от Spine/Audio/Addressable-загрузок. На время ожидания UI скрыт/заменён лоадером, после готовности — раскрывается.

### Bool2StateSwitcherHandler / Int2StateSwitcherHandler

Гонят `UIStateSwitcher` по реактивному значению из связанного `IDataStorage` (потребители, логику не содержат).

- **`Bool2StateSwitcherHandler`** — по `BoolData`: `true` → `SwitcherState.On`, `false` → `Off`. Свитчер конфигурируется под enum `SwitcherState`.
- **`Int2StateSwitcherHandler`** — по `IntData`: номер состояния = `IntData.Value` (свитчер без enum-привязки, любой номер).

Оба: источник — `storage` (`MonoBehaviour` + `[ClassFilter(typeof(IDataStorage))]` + `[AutoLink]`). Переподключаются по `OnUpdateLink` и реагируют на `OnUpdateData` (данные могут прийти, например, через `DataStorageTransport`). Подписка/relink в `OnEnable`, отписка в `OnDisable`. Отсутствие `IDataStorage`/`UIStateSwitcher` — `LogError`.

### DropDown

Компонент выпадающего списка. Состоит из четырёх классов:

- `DropDownComponent` — контроллер: toggle open/close, конфигурация через `SetList(texts, callback, value)`. Поддержка сортировки (`sorting`), `UnityEvent<int> onSelected`, `closeOnSelected`, `scrollSensitivity`. При сортировке строит прямую (`_map`) и обратную (`_mapBack`) карту индексов сортированного → оригинального порядка. Позиция списка перевыставляется на **каждый** открыв (по точке привязки под текущее направление), а не кешируется от первого — список не отстаёт от сдвинувшейся кнопки.
- `DropDownList` — Pool-based список, scroll-позиционирование к выбранному элементу через `ScrollRect.normalizedPosition` (сброс выполняется и на повторном открытии с тем же составом, а не только через `OnEnable` от `SetActive`). Кеширует хэш текстов (`string.Join`) — при том же составе обновляет только `Current` и скролл, не пересоздавая пул. Поле `directionSwitcher` (`UIStateSwitcher`) переключает визуальное состояние под направление раскрытия.
- `DropDownItem` — элемент списка. Получает `DropDownListModel` и `IntData` (индекс) через `IDataStorage`. Визуально выделяет текущий элемент через `UIComponent.SetSwitcher(SwitcherState.On/Off)`. Подписывается на `OnUpdateData` для обновления.
- `DropDownListModel` — `IReactiveData` модель: callbacks (select, close), тексты, текущий выбор, `closeOnSelected`, `ScrollSensitivity`. `Dispose()` очищает подписчиков.

**Направление раскрытия (`DropDownDirection`).** Enum `{ RightDown, RightTop, LeftDown, LeftTop }` (горизонталь + вертикаль; порядок значений = порядок состояний свитчера, RightDown = 0 … LeftTop = 3). По умолчанию — `RightDown` (справа-вниз). Флаг `autoOrientation` включает автоопределение: экранная позиция базовой точки `target` (через `RectTransformUtility.WorldToScreenPoint`, камера канваса; для Overlay — `null`) сравнивается с центром экрана — кнопка левее центра раскрывает список вправо, выше центра — вниз (и наоборот), чтобы он уходил к центру, а не за край. При включённом флаге в инспекторе (Odin `[ShowIf]`) появляются три доп-точки привязки `targetRightTop` / `targetLeftDown` / `targetLeftTop`; отсутствующая точка фолбечится на обязательную `target` (RightDown). Вычисленное направление уходит в `DropDownList.Set(...)`, где `directionSwitcher` принимает соответствующее состояние.

API:
```csharp
dropDown.SetList(texts, OnSelect, currentValue);  // конфигурация
dropDown.SetValue(3);                              // программное переключение
int idx = dropDown.GetValue();                     // оригинальный индекс
string text = dropDown.GetValueItem();             // текст выбранного
```

Callback `Select()` всегда возвращает оригинальный (не сортированный) индекс через `_mapBack`.

Список инстанцируется в `Canvas` при первом открытии, при закрытии деактивируется, при уничтожении контроллера — уничтожается.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Unity.UI.StateSwitcher` | `UIStateSwitcher` — визуальные состояния `AdvancedButton` |
| `Vortex.Unity.UI.TweenerSystem.UniTaskTweener` | `AsyncTween`, `EaseType` — анимация `SliderView` |
| `Vortex.Unity.AppSystem` | `TimeController` — отложенные вызовы |
| `Vortex.Core.System` | `IDataSource`, `IDataStorage`, `IReactiveData` |
| `Vortex.Core.Extensions.ReactiveValues` | `ReactiveValue<T>`, `IntData`, `BoolData`, `FloatData` — фильтр свойств `DataCapturer` |
| `Vortex.Core.Extensions` | `ActionExt.Fire()` |
| Sirenix Odin Inspector | `ValueSelector`, `FoldoutGroup` — UX `DataCapturer` |
| TextMeshPro | TMP в `CounterView` |

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `AdvancedButton.OnClick` в режиме `OnClick`/`OnUpInBorders` внутри `ScrollRect` | При скролле `OnPointerClick` не вызывается — клик подавляется автоматически |
| `AdvancedButton.OnClick` в режиме `OnUpAnywhere` внутри `ScrollRect` | Срабатывает на любой release (по контракту режима, без защиты от drag) |
| `AdvancedButton.Press()` / `Release()` извне | Работает без pointer-событий; в режиме `OnClick` смещение считается нулевым, проверка времени применяется |
| `AdvancedButton.AddOnClick(action)` повторно с тем же `action` | Игнорируется (идемпотентно); `RemoveOnClick` всё равно снимет wrapper |
| `AdvancedButton.OnPointerEnter` после exit с зажатой кнопкой | Визуал возвращается в Pressed, не в Hover |
| `CounterViewBase.OnEnable` при пустом источнике | NRE на `StorageValue.OnUpdateLink += ...` (fail-fast) |
| `DataStorage.GetData<T>()` — тип не найден | Возвращает `null` |
| `DataStorage.AddData()` — добавление | `OnUpdateLink` не вызывается (link-level не нарушен) |
| `DataCapturer` — свойство переименовано или отсутствует | `Debug.LogError` + `enabled = false`; `Start`/`RefreshLink` не выполняются |
| `DataCapturer` — источник реализует `IDataSource` | Подписка на `OnUpdateLink` источника, `RefreshLink` при сигнале |
| `DataCapturer.RefreshLink` — ссылка не изменилась | `OnUpdateLink` не вызывается (`ReferenceEquals`) |
| `DataCapturer` — `source` или property не заданы в инспекторе | NRE в `Awake` (fail-fast по канону) |
| `SliderView.Set()` — те же value/max | Обновление пропускается |
| `EnableDelayForChild` — `OnDisable` до срока | Дети деактивируются, таймер снимается |
| `AutoRectSetter` в Editor | Обновляется при `OnValidate` |

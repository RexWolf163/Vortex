# UI Misc

**Namespace:** `Vortex.Unity.UI.Misc`
**Сборка:** `ru.vortex.unity.ui.misc`

## Назначение

Утилитарные UI-компоненты общего назначения: расширенная кнопка, счётчики, слайдер с анимацией, контейнер данных, вспомогательные MonoBehaviour.

---

## Компоненты

### AdvancedButton

Расширенная кнопка с режимами клика, визуальными состояниями и отслеживанием жестов. Реализует `IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`.

Визуальные состояния (через `UIStateSwitcher`): Free, Hover, Pressed.

| Режим клика | Описание |
|-------------|----------|
| `OnTap` | Срабатывание при нажатии |
| `OnUpInBorders` | Срабатывание при отпускании внутри границ |
| `OnUpAnywhere` | Срабатывание при отпускании в любом месте |
| `OnClick` | Срабатывание при press+release без смещения > 20px |

Events (Action): `OnClick`, `OnPressed`, `OnReleased`, `OnHover`, `OnExit`.
UnityEvents (массивы): `onClick[]`, `onHover[]`, `onExit[]`.
Внешнее управление: `Press()`, `Release()`, `AddOnClick(UnityAction)`, `RemoveOnClick(UnityAction)`.

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
- Если источник реализует `IReactiveData`, подписывается на его `OnUpdateData` для отслеживания пересоздания реактивных полей.
- `Start` — первый `RefreshLink()` после всех `Awake` сцены, потребители успевают подписаться.
- `RefreshLink` сравнивает новую и старую ссылки через `ReferenceEquals` и инвоцирует `OnUpdateLink` только при реальной смене ссылки.
- `OnDestroy` — отписка от источника, зануление кеша.

Ниша: связки, которые нельзя зашить через `[UIComponentLink]` (атрибутная привязка в коде виджета) — например, generic Pool-итемы, виджеты-шаблоны, сборка связки в инспекторе на префабе. Источник всегда `MonoBehaviour` — для `ScriptableObject`-настроек/пресетов используется прямая `[SerializeField]`-ссылка, не `DataCapturer`.

### CounterView (abstract)

Абстрактный компонент для отображения числовых счётчиков с анимацией изменения.

Наследник реализует:
- `int GetValue()` — текущее значение
- `int? GetMaxValue()` — максимальное значение (nullable)

Возможности:
- Поддержка Text, TextMeshPro, TextMeshProUGUI (массивы)
- Отдельные массивы для max-значений
- Паттерны форматирования (`pattern`, `patternMax`)
- Анимация tweener при увеличении (`onUp`) и/или уменьшении (`onDawn`)
- Интеграция с `SliderView`
- Кэширование значений для предотвращения избыточных обновлений
- `Refresh()` — ручное обновление

### SliderView

Анимированный Slider через `AsyncTween`.

```csharp
sliderView.Set(0.75f, 1f);   // value, max
```

| Поле | Тип | Описание |
|------|-----|----------|
| `slider` | `Slider` | Целевой слайдер |
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

### DropDown

Компонент выпадающего списка. Состоит из четырёх классов:

- `DropDownComponent` — контроллер: toggle open/close, конфигурация через `SetList(texts, callback, value)`. Поддержка сортировки (`sorting`), `UnityEvent<int> onSelected`, `closeOnSelected`, `scrollSensitivity`. При сортировке строит прямую (`_map`) и обратную (`_mapBack`) карту индексов сортированного → оригинального порядка.
- `DropDownList` — Pool-based список, scroll-позиционирование к выбранному элементу через `ScrollRect.normalizedPosition`. Кеширует хэш текстов (`string.Join`) — при повторном вызове `Set()` с теми же данными обновляет только `Current`, не пересоздаёт пул.
- `DropDownItem` — элемент списка. Получает `DropDownListModel` и `IntData` (индекс) через `IDataStorage`. Визуально выделяет текущий элемент через `UIComponent.SetSwitcher(SwitcherState.On/Off)`. Подписывается на `OnUpdateData` для обновления.
- `DropDownListModel` — `IReactiveData` модель: callbacks (select, close), тексты, текущий выбор, `closeOnSelected`, `ScrollSensitivity`. `Dispose()` очищает подписчиков.

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
| `AdvancedButton.OnClick` в режиме `OnClick` — свайп | Не срабатывает (смещение > 20px) |
| `AdvancedButton.Press()` / `Release()` извне | Работает без pointer-событий |
| `DataStorage.GetData<T>()` — тип не найден | Возвращает `null` |
| `DataStorage.AddData()` — добавление | `OnUpdateLink` не вызывается (link-level не нарушен) |
| `DataCapturer` — свойство переименовано или отсутствует | `Debug.LogError` + `enabled = false`; `Start`/`RefreshLink` не выполняются |
| `DataCapturer` — источник реализует `IReactiveData` | Подписка на `OnUpdateData` источника, `RefreshLink` при сигнале |
| `DataCapturer.RefreshLink` — ссылка не изменилась | `OnUpdateLink` не вызывается (`ReferenceEquals`) |
| `DataCapturer` — `source` или property не заданы в инспекторе | NRE в `Awake` (fail-fast по канону) |
| `CounterView.Refresh()` — значение не изменилось | Обновление пропускается (кэш) |
| `SliderView.Set()` — те же value/max | Обновление пропускается |
| `EnableDelayForChild` — `OnDisable` до срока | Дети деактивируются, таймер снимается |
| `AutoRectSetter` в Editor | Обновляется при `OnValidate` |

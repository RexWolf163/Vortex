# Vortex Unity

**Namespace:** `Vortex.Unity.*`
**Платформа:** Unity 2021.3+
**Файлов:** ~315 (.cs), 21 подсистема

---

## Зачем нужен слой Unity

Core описывает «что» — контракты, шины, модели. Слой Unity отвечает на вопрос «как» — реализует эти контракты средствами движка: загружает ассеты, рисует интерфейсы, проигрывает звук, сохраняет на диск.

Но это лишь половина задачи. Вторая — и более важная — дать инструменты для **сборки поведения без кода**.

### Философия: тюнинг без кода

Идеальный рабочий процесс во Vortex выглядит так: программист создаёт атомарные компоненты, дизайнер собирает из них поведение в Inspector. Никакого нового C# для типовых задач — открыть окно, проиграть звук, переключить состояние, загрузить сцену, запустить цепочку логики.

Это не абстрактная цель, а практический принцип проектирования. Каждый Handler в слое Unity — это MonoBehaviour с одной конкретной обязанностью, который настраивается полями в Inspector. `CallUIHandler` — открывает UI по GUID из выпадающего списка. `AudioHandler` — проигрывает звук из пресета. `LoadSceneHandler` — загружает сцену. `InputActionHandler` — маршрутизирует ввод в UnityEvent. `LogicChainStarter` — запускает цепочку логики из ScriptableObject.

Ни один из этих компонентов не требует наследования или написания кода для использования. Они **атомарны** — каждый делает ровно одну вещь. Они **комбинируемы** — сложное поведение получается из набора простых компонентов на одном GameObject. Они **настраиваемы** — все параметры доступны в Inspector через типизированные дропдауны, toggle-кнопки и условную видимость.

### Интерфейс — контейнер, не логика

UI во Vortex рассматривается как контейнер без собственной логики принятия решений. `UserInterface` — это MonoBehaviour, который знает, как себя показать и скрыть (через `TweenerHub`), но не знает, почему. Вся логика формируется как набор автономных компонентов:

- `UIComponent` управляет текстами, кнопками, спрайтами — но не решает, что показывать
- `UIStateSwitcher` переключает визуальные состояния — но не решает, когда переключать
- `CallUIHandler` открывает окна — но не решает, можно ли открыть
- Условия (`UserInterfaceCondition`) определяют возможность показа — но не рисуют UI

Каждый элемент отвечает за свою атомарную функцию. Композиция этих элементов на сцене формирует поведение представления. Программист создаёт новый тип `StateItem` или `UserInterfaceCondition` — дизайнер комбинирует их в Inspector.

---

## Что здесь лежит

### Драйверы систем

Каждая Core-система, построенная на `SystemController<T, TD>`, получает здесь драйвер — MonoBehaviour-синглтон, реализующий интерфейс `ISystemDriver`:

| Драйвер | Core-система | Что делает |
|---------|-------------|-----------|
| `SettingsDriver` | `Settings` | Загружает `SettingsPreset` из Resources, копирует через Reflection |
| `SaveSystemDriver` | `SaveController` | PlayerPrefs + XML + GZip-сжатие |
| `MappedParametersDriver` | `ParameterMaps` | Загрузка карт из Resources |
| `AudioDriver` | `AudioController` | AudioSource, микширование, обработка пресетов |
| `LocalizationDriver` | `Localization` | Загрузка локалей, переключение языков |
| `LogDriver` | `Log` | Маршрутизация в `Debug.Log` / `Debug.LogError` |

Драйверы регистрируются автоматически через `RuntimeInitializeOnLoadMethod` и `InitializeOnLoadMethod`. Программисту не нужно вручную вызывать `Init()` — система стартует сама.

### Пресеты (ScriptableObject)

Пресеты — это ScriptableObject, который служит конфигурацией записи в Database. Через `RecordPreset<T>` они получают GUID и регистрируются в шине при загрузке. Каждый пресет — это data-driven конфигурация, которую можно менять без перекомпиляции:

- `UserInterfacePreset` — тип UI, условия показа
- `LogicChainPreset` — шаги логической цепочки
- `SoundSamplePreset` / `MusicSamplePreset` — аудио-клипы с параметрами
- `ParametersMapStorage` — карта параметров
- `TweenPreset` — кривые анимации, длительность, easing
- `SavePreset` — структура слотов сохранения
- `SettingsPreset` — абстрактный базовый тип для расширяемых настроек

### Handlers (атомарные компоненты)

Handlers — ядро компонентного подхода. Каждый Handler — MonoBehaviour без абстрактной логики, с конкретной единственной функцией:

**UI и навигация:**
- `CallUIHandler` — открыть/закрыть/переключить UI по GUID
- `CallUIClose` — закрыть текущий UI
- `UIDragHandler` — перетаскивание окна с ограничением по Canvas

**Аудио:**
- `AudioHandler` — воспроизвести звук (через AudioSource или глобальный AudioPlayer)
- `MusicHandler` — управление музыкой
- `AudioSwitcher` — переключение аудиосостояний
- `AudioValueSlider` — привязка слайдера к громкости

**Ввод:**
- `InputActionHandler` — маршрутизация InputAction в UnityEvent (onPressed, onReleased)
- `InputMapHandler` — переключение карт ввода
- `KeyboardHandler` — маршрутизация клавиатурного ввода

**Сцены и жизненный цикл:**
- `LoadSceneHandler` — загрузка сцены (Single/Additive)
- `UnloadSceneHandler` — выгрузка сцены
- `MonoBehaviourEventsHandler` — UnityEvent-обёртки для Awake/OnDestroy/OnEnable/OnDisable

**Логика и данные:**
- `LogicChainStarter` — запуск логической цепочки из пресета
- `SetLocaleHandler` — смена языка
- `SetTextComponent` — автолокализация текста в UIComponent
- `SetSpriteComponent` — локализованный спрайт

### UI-подсистемы

**UIComponent** — модульный контроллер UI-элемента. Управляет массивами специализированных `UIComponentPart`:

| Part | Назначение |
|------|-----------|
| `UIComponentText` | Текст (Text, TMP, TMP UGUI) |
| `UIComponentButton` | Кнопки (Button, AdvancedButton) |
| `UIComponentGraphic` | Графика (SpriteRenderer, Image) |
| `UIComponentSwitcher` | Состояния (UIStateSwitcher) |

API единообразный: `SetText()`, `SetAction()`, `SetSprite()`, `SetSwitcher()`, `PutData()`. UIComponent — это единая точка входа для контроллера, который хочет настроить представление. Контроллер вызывает `PutData(UIComponentData)` — UIComponent распределяет данные по своим Part'ам.

**UIStateSwitcher** — конечный автомат визуальных состояний. Каждое состояние (`StateData`) содержит массив `StateItem` — полиморфных действий:

| StateItem | Что делает |
|-----------|-----------|
| `GameObjectsSwitch` | Включает/выключает объекты |
| `ColorsSwitch` | Меняет цвета |
| `SpritesSwitch` | Меняет спрайты |
| `AnimatorBoolSwitch` | Управляет bool в Animator |
| `AnimatorStateSwitch` | Переключает слой Animator |
| `TweenerHubSwitch` | Запускает анимации TweenerHub |
| `EventFire` | Вызывает UnityEvent |

StateItem — расширяемый тип: программист создаёт наследника, и он автоматически появляется в dropdown Inspector. Дизайнер комбинирует StateItem'ы внутри состояний, не написав ни строчки кода.

**TweenerSystem** — анимации на UniTask:

- `TweenerHub` — оркестратор: массив `TweenLogic`, методы `Forward()` / `Back()` / `Pulse()`
- `TweenLogic` — абстрактная анимация: `ColorLogic`, `CanvasOpacityLogic`, `RectScaleLogic`, `FillImageLogic`, `PivotLogic`
- `TweenPreset` — ScriptableObject: кривая, длительность, точки включения/выключения
- `AsyncTween` — standalone fluent API: `.Set().SetEase().OnComplete().Run()` для любого float/Vector/Color

### DatabaseSystem (расширения)

- `DbRecordAttribute` — фильтрованный dropdown записей по типу в Inspector. Вместо строкового GUID — выбор из типизированного списка
- `DatabaseSettings` — конфигурация Addressable labels
- `AddressablesDriver` / `ResourcesDriver` — загрузка записей из Addressables или Resources

### EditorTools (~60 файлов)

Пакет для кастомизации Inspector. Предоставляет 20 атрибутов, которые позволяют настроить отображение полей без написания Editor-кода:

- `[AutoLink]` — автопривязка компонента при null
- `[OnChanged("Method")]` — callback при изменении
- `[ValueSelector("Method")]` — SearchablePopup из метода
- `[ToggleButton]` — bool как стилизованная кнопка
- `[ToggleBox("field")]` — условная группировка полей
- `[Show]` / `[Hide]` / `[ShowInPlay]` / `[HideInEditor]` — условная видимость
- `[ClassLabel("$Method")]` — кастомный заголовок элемента коллекции
- `[InfoBubble("text")]` — информационный блок
- `[VortexCollection]` — рендеринг коллекций с drag & drop, fold, контекстными меню

EditorTools работает и с нативным Inspector, и с Odin Inspector (через `#if ODIN_INSPECTOR`).

### InputBusSystem

Маршрутизация ввода. `InputController` — статический контроллер, `InputSubscriber` — MonoBehaviour для регистрации Input Action Map. `InputActionHandler` связывает конкретное действие с UnityEvent — без кода, через dropdown в Inspector.

### Components/Misc

Утилитарные компоненты:

- `MonoBehaviourEventsHandler` — декларативные UnityEvent для жизненного цикла MonoBehaviour
- `LoaderStarter` — точка входа для `Loader.Run()` на сцене
- `SetLocaleHandler` — переключение языка
- `SetTextComponent` / `SetSpriteComponent` / `SetActionComponent` — локализованные компоненты

---

## Как это работает вместе

Типичный UI-экран собирается из готовых компонентов:

1. **UserInterface** на корневом объекте — управляет показом/скрытием через TweenerHub
2. **UIComponent** на каждом элементе — обеспечивает единый API для контроллера
3. **UIStateSwitcher** для визуальных состояний — «активно / неактивно / выделено»
4. **CallUIHandler** на кнопке — открывает другое окно по нажатию
5. **AudioHandler** рядом — проигрывает звук при взаимодействии
6. **SetTextComponent** на текстовых полях — автоматическая локализация
7. **MonoBehaviourEventsHandler** — дополнительная реакция на жизненный цикл

Ни одна из этих связей не требует C#. Всё настраивается через Inspector, с типизированными dropdown и условной видимостью полей. Новый код появляется только когда нужен принципиально новый тип поведения — новый `StateItem`, новый `UserInterfaceCondition`, новый `TweenLogic`.

---

## Границы слоя

Unity **делает**:
- Реализует интерфейсы драйверов из Core (`ISystemDriver`, `IDriver`)
- Предоставляет атомарные компоненты для сборки поведения в Inspector
- Загружает ассеты (Resources, Addressables)
- Управляет MonoBehaviour-жизненным циклом
- Рисует и анимирует UI
- Обрабатывает ввод, звук, сцены

Unity **не делает**:
- Не содержит доменной логики конкретного проекта
- Не принимает решений за контроллер
- Не хранит данные — они в шине Database (Core)
- Не зависит от слоёв AppSDK и AppLocale

Доменная логика — в слоях выше. Unity — это универсальный набор инструментов, одинаковый для всех проектов на Vortex.

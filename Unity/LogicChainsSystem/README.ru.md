# LogicChainsSystem (Unity)

**Namespace:** `Vortex.Unity.LogicChainsSystem.*`
**Сборка:** `ru.vortex.unity.logicchains`
**Платформа:** Unity 2021.3+

---

## Назначение

Unity-слой системы логических цепочек. Предоставляет ScriptableObject-пресеты для визуальной настройки цепочек в Inspector, базовые действия и условия, а также компонент для запуска цепочек со сцены.

Возможности:

- `LogicChainPreset` — ScriptableObject-пресет цепочки (`Database/Logic Chain`)
- `ChainStepPreset`, `ConnectorPreset` — настройка этапов и переходов в Inspector
- `UnityLogicAction` — базовый класс Unity-действий с `[ClassLabel]`
- `LoadScene` — встроенное действие загрузки сцены
- `LogicChainStarter` — MonoBehaviour для запуска цепочки по `Database.OnInit`
- Unity-условия (отдельная сборка `ru.vortex.unity.logicconditions`)

Вне ответственности:

- Логика выполнения цепочки — Core (`LogicChains`)
- Модели `ChainStep`, `Connector`, `LogicAction`, `Condition` — Core

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.LogicChainsSystem` | `LogicChains`, `LogicChain`, `ChainStep`, `Connector`, `LogicAction`, `Condition` |
| `Vortex.Core.DatabaseSystem` | `Record`, `Database`, `RecordTypes` |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid()`, `ObjectExtCopy`, `StringExtensions` |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>`, `DbRecordAttribute` |
| `Vortex.Unity.EditorTools` | `[ClassLabel]` для отображения имён в коллекциях |
| `Vortex.Unity.AppSystem` | `TimeController` (для `LoadScene`) |
| Odin Inspector | `[ValueDropdown]`, `[HideReferenceObjectPicker]`, `[SerializeReference]` |

---

## Архитектура

```
LogicChainPreset : RecordPreset<LogicChain>  (ScriptableObject)
  ├── startStep: string (GUID)
  ├── chainSteps: ChainStepPreset[]
  ├── ChainSteps → Dictionary<string, ChainStep>  ← конвертация через CopyFrom
  └── Editor: GetStepsList(), TestStartStep(), OnValidate()

ChainStepPreset [Serializable, ClassLabel]
  ├── guid, name, description
  ├── actions: LogicAction[]               ← [SerializeReference]
  ├── connectors: ConnectorPreset[]        ← [SerializeReference]
  └── Editor: EditorInit(owner), GetStepName()

ConnectorPreset [Serializable, ClassLabel]
  ├── targetStepGuid: string               ← [ValueDropdown] из этапов цепочки
  ├── conditions: Condition[]              ← [SerializeReference]
  └── Editor: GetTargets(), GetConnectorName()

UnityLogicAction : LogicAction
  └── abstract NameAction → [ClassLabel("@NameAction")]

LoadScene : UnityLogicAction
  ├── SceneName                            ← [ValueDropdown] из Build Settings
  ├── _additiveMode: bool
  └── _async: bool                         ← по умолчанию true

UnLoadScene : UnityLogicAction
  ├── SceneName                            ← [ValueDropdown] из Build Settings
  └── _async: bool                         ← по умолчанию true

LogicChainStarter : MonoBehaviour
  ├── logicChain: string                   ← [DbRecord(LogicChain, MultiInstance)]
  └── Start → Database.OnInit += CallChain
```

### Пресет → Runtime конвертация

`LogicChainPreset` хранит `ChainStepPreset[]` в Inspector. При обращении к `ChainSteps` каждый `ChainStepPreset` конвертируется в `ChainStep` через `ObjectExtCopy.CopyFrom`. `ConnectorPreset` аналогично конвертируется в `Connector`. Это обеспечивает multi-instance — каждый вызов `Database.GetNewRecord<LogicChain>` создаёт независимую копию.

### Inspector-интеграция

- `ChainStepPreset` — `[ClassLabel("@GetStepName()")]` показывает имя этапа в коллекции
- `ConnectorPreset` — `[ClassLabel("@GetConnectorName()")]` показывает цель перехода: `"to «StepName»"`, `"Complete this chain"` или `"Empty Connector"`
- `startStep` — `[ValueDropdown]` из списка этапов, красная подсветка при невалидном GUID
- `targetStepGuid` — `[ValueDropdown]` из этапов цепочки (исключая текущий) + `"_CompleteChain"`
- `LogicAction[]` и `Condition[]` — `[SerializeReference, HideReferenceObjectPicker]` для полиморфизма

---

## Распределение элементов по пакетам

Действия (`LogicAction`) и условия (`Condition`) не сосредоточены в одном пакете — **каждый пакет добавляет свои элементы цепочек**, рядом с функциональностью, которую они задействуют. В Inspector-дропдаунах `[SerializeReference]` они появляются автоматически через type-scanning Odin (никакой ручной регистрации). Это тот же `package-composition-first` принцип, что и в остальном фреймворке (см. `COMPOSITION.md`).

Чтобы воспользоваться элементом, достаточно чтобы его пакет был в проекте — отключение пакета убирает его элементы из дропдаунов, остальная цепочка продолжает компилироваться.

| Элемент | Тип | Пакет | Что делает |
|---------|-----|-------|-----------|
| `LoadScene` | Action | `ru.vortex.unity.logicchains` | Загрузка сцены (single/additive, sync/async) |
| `UnLoadScene` | Action | `ru.vortex.unity.logicchains` | Выгрузка сцены (sync/async) |
| `SceneLoaded` | Condition | `ru.vortex.unity.logicconditions` | Сцена загружена (видит и Additive-сцены) |
| `SystemsLoaded` | Condition | `ru.vortex.unity.logicconditions` | `App.GetState() == Running` (системы Vortex готовы) |
| `MinTimeCondition` | Condition | `ru.vortex.unity.logicconditions` | Прошло ≥ N секунд (минимальная длительность шага) |
| `OpenUI` | Action | `ru.vortex.unity.uiprovider` | Открыть интерфейс через `UIProvider.Open` |
| `CloseUI` | Action | `ru.vortex.unity.uiprovider` | Закрыть интерфейс через `UIProvider.Close` |
| `CloseAllUI` | Action | `ru.vortex.unity.uiprovider` | Закрыть все common-интерфейсы |
| `NaninovelInitialized` | Condition | `ru.vortex.nani.core` | Движок Naninovel инициализирован (`Engine.Initialized`) |

**Соглашение по реентрантности.** Действия, которые трогают сцены или UI (`LoadScene`, `UnLoadScene`, `OpenUI`, `CloseUI`, `CloseAllUI`), откладывают реальную работу на конец кадра через `TimeController.Call`/`Accumulate`. Причина: `Invoke()` вызывается из стека продвижения цепочки (`RunChain → CheckConditions → RunChain`), и синхронная загрузка/закрытие прямо внутри этого вызова приводит к реентрантности. Своё кастомное действие, меняющее сцены/UI, стоит писать по тому же паттерну.

---

## Условия (LogicConditionsSystem)

Отдельная сборка `ru.vortex.unity.logicconditions`. Базовый класс `UnityCondition : Condition` с `[ClassLabel("@ConditionName")]`.

| Условие | Описание | Проверка |
|---------|----------|----------|
| `SceneLoaded` | Ожидание загрузки сцены по имени | `GetSceneByName(name).IsValid() && isLoaded` — видит и Additive-сцены, не только активную; подписка на `SceneManager.sceneLoaded` |
| `SystemsLoaded` | Ожидание `App.GetState() == Running` | Подписка на `App.OnStateChanged` |
| `MinTimeCondition` | Минимальное время ожидания (секунды) | `DateTime.UtcNow >= target` через `TimeController` |

Все условия следуют паттерну: проверка в `Start()` → если уже выполнено, `RunCallback()` сразу; иначе подписка на событие.

`NaninovelInitialized` (условие готовности движка Naninovel) живёт в пакете `ru.vortex.nani.core` — см. таблицу «Распределение элементов по пакетам».

---

## Контракт

### Вход

- `LogicChainPreset` создаётся через `Create > Database > Logic Chain`
- Этапы, действия, условия настраиваются в Inspector
- Запуск: `LogicChainStarter` на сцене или `LogicChains.AddChain(presetGuid)` из кода

### Выход

- Цепочка выполняется согласно логике Core: этапы → действия → условия → переходы

### API

| Компонент | Назначение |
|-----------|-----------|
| `LogicChainPreset` | ScriptableObject, создание через `Database/Logic Chain` |
| `LogicChainStarter` | MonoBehaviour, запуск цепочки при `Database.OnInit` |
| `UnityLogicAction` | Базовый класс для Unity-действий |
| `UnityCondition` | Базовый класс для Unity-условий |

### Встроенные действия

| Действие | Описание |
|----------|----------|
| `LoadScene` | Загрузка сцены (sync/async, single/additive) через `TimeController.Call` |
| `UnLoadScene` | Выгрузка сцены (sync/async) через `TimeController.Call` |

Полный список действий и условий из всех пакетов — в разделе «Распределение элементов по пакетам».

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `LogicChainStarter` запускает по `Database.OnInit` | Требует инициализации Database |
| `LoadScene` выполняется через `TimeController.Call` | Гарантия выполнения в main thread |
| Действия и условия — `[SerializeReference]` | Полиморфизм, но нет drag & drop ассетов |
| GUID этапов генерируются при создании | `Crypto.GetNewGuid()` в инициализаторе поля |

---

## Использование

### Создание цепочки

1. `Create > Database > Logic Chain` — создать пресет
2. Добавить этапы (`ChainStepPreset[]`) с именами и описаниями
3. В каждом этапе добавить действия (`LogicAction[]`) и коннекторы (`ConnectorPreset[]`)
4. В коннекторах указать цель перехода и условия
5. Указать `startStep` — начальный этап

### Запуск со сцены

Добавить `LogicChainStarter` на GameObject, выбрать пресет цепочки через `[DbRecord]` поле.

---

### Пример: загрузка приложения через цепочку

Канонический сценарий старта приложения — цепочка-загрузчик, которая ждёт готовности систем, грузит основную сцену и снимает экран загрузки.

#### Два варианта компоновки

**Классический (без Naninovel).** `Preloader` (экран загрузки) — сама стартовая сцена. Это проще: не нужен отдельный объект, грузящий её, — она уже открыта на старте. Цепочка грузит `Main` аддитивно, а `Preloader` просто выгружает в конце:

```
Preloader (стартовая сцена = экран загрузки)
└── [Autorun]
    ├── LoaderStarter            (запуск Loader → загрузка систем Vortex)
    └── LogicChainStarter        (Logic Chain: LoaderChain)
```
Цепочка: ждёт системы → грузит `Main` (Additive) → выгружает `Preloader`.

**С Naninovel.** Naninovel капризно относится к **выгрузке стартовой сцены** — выгрузить сцену, которая была первой, штатно не получается. Поэтому стартовой делают тонкую служебную сцену `Loading`, которая аддитивно подгружает `Preloader`. Теперь `Preloader` — **не** стартовая сцена, и её можно безопасно выгрузить в конце цепочки. Именно эта компоновка показана ниже (и на первом скриншоте).

#### Сцена и компоненты `[Autorun]` (вариант с Naninovel)

```
Loading (тонкая стартовая сцена)
└── [Autorun]
    ├── LoadSceneHandler         (Scene Name: Preloader, Additive Mode ✓)
    │       └── грузит экран загрузки аддитивно (отдельной сценой, не стартовой)
    ├── LoaderStarter            (запуск Loader → загрузка систем Vortex)
    ├── LogicChainStarter        (Logic Chain: LoaderChain)
    └── MonoBehaviourEventsHandler
            └── On Enable → LoadSceneHandler.Run()
```

Роли компонентов:
- **`LoaderStarter`** — на `App.OnStarting` дёргает `Loader.Run()`: запускает асинхронную загрузку всех систем фреймворка. По завершении `App` переходит в состояние `Running`.
- **`LoadSceneHandler`** (из `Unity/Components/SceneControllers`) — грузит сцену экрана загрузки `Preloader` аддитивно. Вызывается из `MonoBehaviourEventsHandler.OnEnable`.
- **`LogicChainStarter`** — на `Database.OnInit` создаёт инстанс пресета `LoaderChain` и запускает его.

#### Пресет `LoaderChain` (Logic Chain, Multi Instance)

Три этапа, `Start Step = "Waiting Loading"`:

```
1. Waiting Loading  (описание: «Загрузка систем»)
   Actions:    — (пусто, этап только ждёт)
   Connector → LoadScene
      Conditions (AND):
        • SystemsLoaded         → "Wait all systems loading"
        • NaninovelInitialized  → "Wait Naninovel initialization"

2. LoadScene
   Actions:
        • LoadScene(Main, Additive ✓, Async ✓)  → "Call load for «Main» scene"
   Connector → Hide Loader UI
      Conditions:
        • SceneLoaded(Main)     → "Wait Main loading"

3. Hide Loader UI  (описание: «скрыть UI загрузки»)
   Actions:
        • UnLoadScene(Preloader, Async ✓)        → "Call unload for «Preloader» scene"
   Connector → _CompleteChain
      Conditions: — (пусто → автоматический переход → цепочка завершается и удаляется)
```

#### Что происходит по шагам

1. Сцена `Loading` стартует. `[Autorun].OnEnable` → `LoadSceneHandler.Run()` грузит `Preloader` (экран загрузки) поверх. Параллельно `LoaderStarter` запускает загрузку систем, `LogicChainStarter` запускает `LoaderChain`.
2. **Этап «Waiting Loading»** действий не имеет — он ждёт, пока на одном коннекторе выполнятся **оба** условия: системы Vortex поднялись (`SystemsLoaded` → `App.Running`) **и** инициализировался Naninovel (`NaninovelInitialized`). Два условия на одном коннекторе дают конъюнкцию — переход только когда готовы обе подсистемы, независимо от порядка финиша.
3. **Этап «LoadScene»** — действие `LoadScene` грузит основную сцену `Main` аддитивно и асинхронно. Коннектор ждёт `SceneLoaded(Main)` — событие фиксирует именно загруженную (в т. ч. Additive) сцену по имени.
4. **Этап «Hide Loader UI»** — действие `UnLoadScene` выгружает `Preloader` (убирает экран загрузки). Коннектор без условий → автоматический переход на `_CompleteChain` → цепочка завершается и удаляется из реестра.

В итоге: экран загрузки висит ровно от старта до момента, когда `Main` уже загружена, и снимается одним выгрузом сцены. Никакого кода-оркестратора — порядок и условия заданы декларативно в пресете.

> 💡 Если экран загрузки реализован не отдельной сценой, а UI-интерфейсом через `UIProvider`, последний этап вместо `UnLoadScene(Preloader)` использует `CloseUI`/`CloseAllUI` (пакет `ru.vortex.unity.uiprovider`).

---

### Создание кастомного действия

```csharp
public class PlaySound : UnityLogicAction
{
    [SerializeField] private AudioClip clip;

    public override void Invoke()
    {
        AudioSource.PlayClipAtPoint(clip, Vector3.zero);
    }

    protected override string NameAction => $"Play «{(clip ? clip.name : "?")}»";
}
```

### Создание кастомного условия

```csharp
public class ButtonClicked : UnityCondition
{
    [SerializeField] private string buttonId;

    protected override void Start()
    {
        UIEvents.OnButtonClick += OnClick;
    }

    private void OnClick(string id)
    {
        if (id == buttonId) RunCallback();
    }

    public override bool Check() => UIEvents.LastClickedButton == buttonId;
    public override void DeInit() => UIEvents.OnButtonClick -= OnClick;

    protected override string ConditionName => $"Wait click «{buttonId}»";
}
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `startStep` не указан или невалиден | Красная подсветка в Inspector, ошибка при `RunChain` |
| Коннектор без цели | `"Empty Connector"` в Inspector, ошибка при переходе |
| `LogicChainStarter` до инициализации Database | Подписка на `Database.OnInit`, запуск отложен |
| Несколько коннекторов без условий | Первый выполнится, остальные проигнорированы |
| Несколько условий на одном коннекторе | Конъюнкция (AND) — переход только когда `Check()` всех условий вернул true, независимо от порядка их выполнения |
| `LoadScene` / `UnLoadScene` с `_async = false` | Синхронная загрузка/выгрузка, возможна заморозка кадра |
| Этап без действий | Допустимо — сразу переход к проверке условий коннекторов |
| Этап без коннекторов | Цепочка остановится на этом этапе навсегда |

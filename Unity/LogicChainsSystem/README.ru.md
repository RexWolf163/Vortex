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

## Условия (LogicConditionsSystem)

Отдельная сборка `ru.vortex.unity.logicconditions`. Базовый класс `UnityCondition : Condition` с `[ClassLabel("@ConditionName")]`.

| Условие | Описание | Проверка |
|---------|----------|----------|
| `SceneLoaded` | Ожидание загрузки сцены | `SceneManager.GetActiveScene().name == SceneName` |
| `SystemsLoaded` | Ожидание `App.GetState() == Running` | Подписка на `App.OnStateChanged` |
| `MinTimeCondition` | Минимальное время ожидания (секунды) | `DateTime.UtcNow >= target` через `TimeController` |

Все условия следуют паттерну: проверка в `Start()` → если уже выполнено, `RunCallback()` сразу; иначе подписка на событие.

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
| `LoadScene` с `_async = false` | Синхронная загрузка, возможна заморозка кадра |
| Этап без действий | Допустимо — сразу переход к проверке условий коннекторов |
| Этап без коннекторов | Цепочка остановится на этом этапе навсегда |

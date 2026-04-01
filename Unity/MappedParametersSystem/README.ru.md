# MappedParametersSystem (Unity)

**Namespace:** `Vortex.Unity.MappedParametersSystem`, `Vortex.Unity.MappedParametersSystem.Base.Preset`, `Vortex.Unity.MappedParametersSystem.Handlers`, `Vortex.Unity.MappedParametersSystem.Attributes`
**Сборка:** `ru.vortex.unity.mappedparameters`
**Платформа:** Unity 2021.3+

---

## Назначение

Unity-слой системы параметрических карт. Предоставляет ScriptableObject-хранилище для визуальной настройки графа параметров в Inspector, драйвер загрузки из Resources, Inspector-атрибуты для выбора параметров и моделей, MonoBehaviour-хранилище и экспорт графа в DOT-формат.

Возможности:

- `ParametersMapStorage` — ScriptableObject-пресет карты параметров
- `MappedParameterPreset` / `MappedParameterLink` — настройка производных параметров и связей в Inspector
- `MappedParametersDriver` — загрузка карт из Resources, регистрация в `Loader`
- `MappedModelStorage` — абстрактный MonoBehaviour для хранения ссылки на `IMappedModel`
- `[MappedParameter]` / `[MappedModel]` — Inspector-атрибуты с dropdown-выбором
- Валидация: уникальность имён, существование родителей, обнаружение циклов
- Clipboard: JSON export/import карты параметров
- DOT-экспорт графа для визуализации в Graphviz

Вне ответственности:

- Модели данных (`IMappedModel`), шина (`ParameterMaps`) — Core
- Интерфейсы `IParameterMap`, `IParameterLink`, `ParameterLinkCostLogic` — Core
- Интерпретация стоимости — прикладной уровень

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.MappedParametersSystem` | `ParameterMaps`, `ParametersMap`, `GenericParameter`, `IMappedModel`, `IParameterMap`, `IParameterLink` |
| `Vortex.Core.System` | `Singleton<T>`, `IDataStorage` |
| `Vortex.Core.LoaderSystem` | `Loader.Register()` для `IProcess` |
| `Vortex.Core.Extensions` | `StringExtensions`, `DictionaryExt` |
| `Vortex.Unity.EditorTools` | `[ClassLabel]` для отображения в коллекциях |
| `Vortex.Unity.FileSystem` | `File.CreateFolders()` (Editor) |
| `Vortex.Unity.Extensions` | `OdinDropdownTool` (Editor) |
| Odin Inspector | `[ValueDropdown]`, `[HideReferenceObjectPicker]`, `[InfoBox]`, `OdinAttributeDrawer` |
| UniTask | `UniTask`, `UniTask.Yield()` |

---

## Архитектура

```
MappedParametersDriver : Singleton, IDriverMappedParameters, IProcess  (partial)
  ├── Init() → OnInit.Fire()
  ├── SetIndex(Dictionary) → _indexMaps
  ├── RunAsync() → Resources.LoadAll<ParametersMapStorage>
  │    └── GetMap(storage) → ParametersMap
  ├── WaitingFor() → null                    ← нет зависимостей загрузки
  ├── [RuntimeInitializeOnLoadMethod] Register()
  └── [InitializeOnLoadMethod] EditorRegister()  ← #if UNITY_EDITOR

ParametersMapStorage : ScriptableObject  (partial)
  ├── guid: string                           ← [ValueDropdown] из IMappedModel типов
  ├── baseParams: string[]                   ← базовые параметры
  ├── mappedParams: MappedParameterPreset[]  ← [SerializeReference] производные
  ├── Editor: Sort(), GetParamsNames(), ReloadMaps()
  ├── ErrorCheck: CheckErrors(), SearchTop() (цикл)
  └── Clipboard: CopyToClipboardAsJson(), LoadFromJson()

MappedParameterPreset : IParameterMap  [Serializable, ClassLabel]
  ├── name: string
  ├── parents: MappedParameterLink[]         ← [SerializeReference]
  ├── costLogic: ParameterLinkCostLogic      ← [HideIf] при ≤1 родителе
  ├── Cost: int (свойство, 0)
  └── Editor: EditorInit(map), Sort(), GetFoldoutName()

MappedParameterLink : IParameterLink  [Serializable]
  ├── parent: string                         ← [ValueDropdown] из параметров карты
  ├── cost: int                              ← [Min(1)]
  └── Editor: EditorInit(map, owner), GetParentVariants()

MappedModelStorage : MonoBehaviour, IDataStorage  (abstract)
  ├── _data: IMappedModel
  ├── GetData<T>() → _data as T              ← lazy Init()
  ├── OnUpdateLink: Action (abstract)
  └── Init() (abstract)
```

### Загрузка карт

`MappedParametersDriver` реализует `IProcess` и регистрируется в `Loader`. При `RunAsync`:

1. `Resources.LoadAll<ParametersMapStorage>("")` — загрузка всех пресетов
2. Для каждого пресета `GetMap()` конвертирует `baseParams[]` + `mappedParams[]` в `IParameterMap[]`
3. Базовые параметры оборачиваются в `MappedParameterPreset(name)` (без родителей)
4. Результат помещается в `_indexMaps` (словарь от `ParameterMaps`)

`WaitingFor()` возвращает `null` — нет зависимостей от других процессов.

### Inspector-интеграция

- `guid` — `[ValueDropdown]` из всех типов, реализующих `IMappedModel` (через Reflection по всем сборкам)
- `baseParams` — строковый массив, `[InfoBox]` при пустых значениях
- `mappedParams` — `[SerializeReference]`, каждый элемент — `MappedParameterPreset` с `[ClassLabel("$GetFoldoutName")]`
- `parent` в `MappedParameterLink` — `[ValueDropdown]` из всех параметров карты (исключая владельца)
- `costLogic` — отображается только при >1 родителе (`[HideIf]`)
- Кнопка `Sort` — сортировка производных параметров по родителям, затем по имени

### Валидация (Editor)

При изменении списков (`OnListChanged`) выполняется `CheckErrors()`:

1. Проверка уникальности имён базовых и производных параметров (дубликаты переименовываются: `Name` → `Name_1`)
2. Проверка существования родителей (несуществующие обнуляются)
3. Обнаружение циклов (`SearchTop`) — рекурсивный обход по родителям; циклический указатель обнуляется
4. Флаг `Error` — пустые имена или пустые родители → красный `[InfoBox]`

### Clipboard (Editor)

- `CopyToClipboardAsJson()` — сериализует `baseParams` + `mappedParams` в JSON через DTO, копирует в буфер обмена
- `LoadFromJson()` — десериализует из буфера, восстанавливает `MappedParameterPreset[]` + `MappedParameterLink[]`, запускает валидацию

---

## Контракт

### Вход

- `ParametersMapStorage` создаётся через `Create > Vortex > Parameters Map`
- `guid` указывает на `FullName` типа `IMappedModel`
- Базовые и производные параметры настраиваются в Inspector

### Выход

- При загрузке драйвер заполняет `_indexMaps` в `ParameterMaps`
- Runtime-доступ через `ParameterMaps.GetModel<T>()`, `GetParameters<T>()`, `InitMap()`

### Inspector-атрибуты

| Атрибут | Назначение |
|---------|-----------|
| `[MappedParameter(typeof(T))]` | Dropdown-выбор параметра из карты типа `T` (Odin drawer) |
| `[MappedModel]` | Dropdown-выбор типа модели из всех `ParametersMapStorage` (Odin drawer) |

Оба drawer'а поддерживают кнопку `Find` — навигация к `ParametersMapStorage` ассету.

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Загрузка только из Resources | `Resources.LoadAll<ParametersMapStorage>` |
| Odin Inspector обязателен | `[ValueDropdown]`, `OdinAttributeDrawer` |
| `WaitingFor() → null` | Нет зависимостей от других процессов |
| `Cost` на `MappedParameterPreset` — всегда 0 | Стоимость хранится в `MappedParameterLink`, а не в самом параметре |
| Editor-валидация при каждом изменении | Рекурсивный `SearchTop` для обнаружения циклов |

---

## Использование

### Создание карты параметров

1. `Create > Vortex > Parameters Map` — создать `ParametersMapStorage`
2. В `guid` выбрать тип модели (`IMappedModel`) из dropdown
3. Добавить базовые параметры (`baseParams`) — строковые имена
4. Добавить производные параметры (`mappedParams`) — имя, родители с стоимостями, `CostLogic` при >1 родителе
5. Кнопка `Sort` — упорядочить по зависимостям

### MonoBehaviour-хранилище

```csharp
public class CharacterStatsStorage : MappedModelStorage
{
    public override event Action OnUpdateLink;

    protected override void Init()
    {
        _data = ParameterMaps.GetModel<CharacterStats>();
        _data.OnUpdate += () => OnUpdateLink?.Invoke();
    }
}

// Использование
var storage = GetComponent<CharacterStatsStorage>();
var stats = storage.GetData<CharacterStats>();
```

### Inspector-атрибуты

```csharp
public class SkillButton : MonoBehaviour
{
    [MappedParameter(typeof(CharacterStats))]
    public string targetParameter;

    [MappedModel]
    public string modelType;
}
```

### Clipboard

- Inspector → кнопка `To Clipboard` — JSON в буфер обмена
- Inspector → кнопка `From Clipboard` — загрузка из буфера с валидацией

---

## Редакторные инструменты

### Экспорт графа в DOT

`Menu: Vortex > Debug > Export Mapped Parameters into Graph`

Экспортирует каждую `ParametersMapStorage` в файл `.dot` (Graphviz). Базовые параметры выделены цветом `#b3e5fc`. Связи помечены стоимостью. Визуализация: [Graphviz Online](https://dreampuf.github.io/GraphvizOnline/).

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Дублирующееся имя параметра | Автопереименование `Name` → `Name_1`, лог ошибки |
| Родитель не существует в карте | Обнуляется, лог ошибки |
| Циклическая зависимость | Циклический указатель обнуляется, лог ошибки |
| Пустой буфер обмена при `LoadFromJson` | Предупреждение, no-op |
| Невалидный JSON в буфере | Лог ошибки, состояние не изменено |
| Нет `ParametersMapStorage` в Resources | Предупреждение при Editor-загрузке, пустой `_indexMaps` |
| `MappedParameterPreset` с 0 родителями | Допустимо — фактически базовый параметр в списке производных |
| `[MappedParameter]` с абстрактным/интерфейсным типом | `PresetType = null`, красная подсветка |
| `cost < 1` в `MappedParameterLink` | Ограничен `[Min(1)]` в Inspector |

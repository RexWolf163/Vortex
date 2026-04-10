# DataOrchestratorSystem

**Namespace:** `Vortex.Unity.UI.Misc.DataOrchestratorSystem`
**Assembly:** `ru.vortex.unity.ui.misc`

## Назначение

Автоматизация распределения модели данных по контейнерам `DataStorage` на уровне GameObject-иерархии.

Возможности:
- Базовый generic-класс с lifecycle управления привязкой данных к контейнерам
- Кодогенерация оркестратора по модели данных (контекстное меню)
- Генерация дочерних GameObject с компонентами `DataStorage` (Odin-кнопка)
- Автоматическая подписка на `IReactiveData.OnUpdateData` в базовом классе

Вне ответственности:
- Хранение и предоставление данных (`DataStorage`, `IDataStorage`)
- Логика изменения данных (контроллер мини-игры или иной источник)
- Визуальное отображение (`CounterView`, `SliderView` и пр.)

## Зависимости

### Core
- `Vortex.Core.System.Abstractions` — `IDataStorage`
- `Vortex.Core.Extensions.ReactiveValues` — `IReactiveData`, `IntData`, `FloatData`, `BoolData`

### Unity
- `Vortex.Unity.UI.Misc` — `DataStorage`
- `Vortex.Unity.EditorTools` — `ClassFilter`, `AutoLink`
- `Sirenix.OdinInspector` — `Button` (Editor-кнопка генерации иерархии)

## Архитектура

```
DataOrchestrator<T> (abstract MonoBehaviour)
├── source: IDataStorage                    ← источник модели данных
├── Data: T                                 ← кешированная модель
├── _storagesIndex: DataStorage[]           ← кеш полей (рефлексия в Awake)
├── Map(T) / Unmap()                        ← abstract, привязка/отвязка данных
├── OnDataUpdate()                          ← abstract, обновление обёрток
├── IReactiveData → OnUpdateData            ← авто-подписка если T реализует
└── [Button] GenerateHierarchy()            ← Editor: создание дочерних GO

OrchestratorScriptGenerator (Editor-only)
└── ПКМ по .cs → Create → Vortex Templates → DataOrchestrator
    ├── Рефлексия public properties целевого типа
    ├── Генерация наследника DataOrchestrator<T>
    └── Файл создаётся рядом с исходником модели
```

## Контракт

### Кодогенерация — правила маппинга свойств

| Тип свойства | Обработка | Контейнер |
|--------------|-----------|-----------|
| `ReactiveValue<T>` наследники | `SetData` напрямую | — |
| `IReactiveData` реализации | `SetData` напрямую | — |
| Ссылочные типы (class, interface) | `SetData` напрямую | — |
| `string` | `SetData` напрямую | — |
| `int` | Обёртка | `IntData` |
| `float` | Обёртка | `FloatData` |
| `bool` | Обёртка | `BoolData` |
| `enum` | Обёртка с кастом `(int)` | `IntData` |
| `Func<>`, `Action<>`, делегаты | Пропускаются | — |
| `struct` (не примитив, не enum) | Пропускаются | — |

### Кодогенерация — исключения

Свойства с именами `Value`, `State` пропускаются. Обрабатываются только свойства, объявленные непосредственно в целевом типе (`DeclaringType == type`), унаследованные не включаются.

### Генерация иерархии (Odin-кнопка)

- Для каждого поля типа `DataStorage` с незаполненной ссылкой создаётся дочерний GameObject
- Имя дочернего GO: `_{fieldName} [DataStorage]`
- `RectTransform` удаляется, остаётся `Transform`
- Компонент `DataStorage` добавляется и линкуется в поле
- Уже заполненные поля пропускаются
- Дочерние GO размещаются в начале иерархии (`SetAsFirstSibling`)

### Lifecycle базового класса

```
Awake     → кеш DataStorage-полей через рефлексию
OnEnable  → подписка на IDataStorage.OnUpdateLink → Init()
Init      → GetData<T>() → Map(data) → [IReactiveData подписка] → OnDataUpdate()
OnDisable → DeInit() → Unmap() → [IReactiveData отписка] → ClearStorages()
```

### Гарантии
- `ClearStorages` обнуляет все `DataStorage` при `DeInit` — View-компоненты получат null
- `IReactiveData`-подписка автоматическая — если `T` реализует интерфейс, `OnDataUpdate` вызывается при каждом обновлении
- `OnDataUpdate` вызывается сразу после `Map` — начальное заполнение обёрток
- Рефлексия полей — один раз в `Awake`, далее итерация по массиву

## Использование

### 1. Кодогенерация оркестратора

1. В Project выбрать .cs файл с классом модели данных
2. ПКМ → `Create → Vortex Templates → DataOrchestrator`
3. Файл `{ClassName}Orchestrator.cs` создаётся рядом с исходником
4. Внести ручные правки в TODO-секции при необходимости

### 2. Генерация иерархии

1. Добавить сгенерированный оркестратор на GameObject
2. Нажать кнопку `Generate Hierarchy` в Inspector
3. Дочерние GO с `DataStorage` создадутся и пролинкуются автоматически
4. Подключить `source` — компонент с `IDataStorage`

### 3. Ручное создание оркестратора

```csharp
public class MyDataOrchestrator : DataOrchestrator<MyGameData>
{
    [SerializeField] private DataStorage score;
    [SerializeField] private DataStorage playerName;

    private IntData _scoreValue = new(0);

    protected override void Map(MyGameData data)
    {
        _scoreValue.Set(data.Score);
        score?.SetData(_scoreValue);
        playerName?.SetData(data.PlayerName);
    }

    protected override void Unmap()
    {
        _scoreValue.Set(0);
    }

    protected override void OnDataUpdate()
    {
        _scoreValue.Set(Data.Score);
    }
}
```

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `source` == null | NRE в `OnEnable` (fail-fast) |
| `GetData<T>()` → null | `Map` не вызывается, контейнеры не заполняются |
| `UpdateLink` (смена данных) | `DeInit` → `Init` — полный пересброс |
| `T` не реализует `IReactiveData` | `OnDataUpdate` вызывается только при `Map` (начальное заполнение) |
| Повторный `Generate Hierarchy` | Пропускает уже заполненные поля |
| Унаследованные свойства модели | Не попадают в кодогенерацию (только `DeclaringType == type`) |
| Делегаты, struct в модели | Пропускаются при кодогенерации |

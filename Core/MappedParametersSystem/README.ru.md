# MappedParametersSystem (Core)

**Namespace:** `Vortex.Core.MappedParametersSystem.Bus`, `Vortex.Core.MappedParametersSystem.Base`
**Сборка:** `ru.vortex.core.mappedparameters`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Система параметрических карт. Описывает граф зависимостей между именованными параметрами с направленными связями и стоимостями. Карта определяет структуру (какие параметры существуют и как связаны), модель — хранит конкретные значения.

Возможности:

- Граф параметров: базовые (корневые) и производные (с родителями)
- Связи (`IParameterLink`) с числовой стоимостью (`Cost`)
- Логика объединения стоимостей при нескольких родителях (`And`, `Or`, `Sum`)
- Получение массива `GenericParameter[]` для runtime-использования
- Создание и инициализация `IMappedModel` через шину `ParameterMaps`
- GUID карты — `FullName` типа модели

Вне ответственности:

- Хранение карт (ScriptableObject-пресеты) — Unity-слой
- Inspector-редактирование, валидация, атрибуты — Unity-слой
- Интерпретация стоимости — контроллер прикладного уровня

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `Singleton<T>` |
| `Vortex.Core.Extensions` | `StringExtensions`, `DictionaryExt.AddNew()` |
| `Vortex.Core.Extensions.LogicExtensions` | `ActionExt.Fire()` |

---

## Архитектура

```
ParameterMaps : SystemController<ParameterMaps, IDriverMappedParameters>
  ├── _parametersMaps: Dictionary<string, ParametersMap>
  ├── GetParameters<T>() → GenericParameter[]
  ├── GetParameters(Type) → GenericParameter[]
  ├── GetParameters(string fullName) → GenericParameter[]
  ├── GetModel<T>() → IMappedModel          ← Activator.CreateInstance + Init
  ├── GetModel(Type) → IMappedModel
  └── InitMap(IMappedModel) → void           ← инициализация существующего экземпляра

ParametersMap
  ├── Guid: string                           ← FullName типа модели
  ├── Parameters: IParameterMap[]
  ├── GetParameterMap(name) → IParameterMap
  └── GetParameters() → GenericParameter[]   ← новый массив при каждом вызове

GenericParameter [Serializable]
  ├── Name: string
  ├── Value: int
  ├── SetValue(int) → OnUpdate event
  └── OnUpdate: Action

IParameterMap
  ├── Name: string
  ├── Parents: IParameterLink[]
  ├── Cost: int
  └── CostLogic: ParameterLinkCostLogic

IParameterLink
  ├── Parent: string                         ← имя родительского параметра
  └── Cost: int

IMappedModel
  ├── OnUpdate: Action
  ├── GetParameters() → string[]
  ├── GetValue(string) → int
  ├── GetParents(string) → IParameterLink[]
  ├── GetParameterAsContainer(string) → GenericParameter
  └── Init(ParametersMap)

IDriverMappedParameters : ISystemDriver
  └── SetIndex(Dictionary<string, ParametersMap>)
```

### Карта vs Модель

| Сущность | Назначение | Хранит значения |
|----------|-----------|-----------------|
| `ParametersMap` | Схема связей (blueprint) | Нет |
| `IMappedModel` | Экземпляр данных | Да |

`ParametersMap` — immutable описание графа. `IMappedModel` — mutable состояние, созданное по карте. Разделение позволяет создавать множество независимых экземпляров по одной схеме.

### ParameterLinkCostLogic

| Значение | Описание |
|----------|----------|
| `And` | Все условия родителей должны быть выполнены |
| `Or` | Достаточно одного родителя |
| `Sum` | Суммирование стоимостей |

Интерпретация `Cost` определяется прикладным контроллером: порог, множитель, очки прокачки и т.д.

### GenericParameter — реактивность

`SetValue(int)` проверяет изменение значения. При `Value != value` обновляет и вызывает `OnUpdate` через `ActionExt.Fire()`. Повторная установка того же значения — no-op.

---

## Контракт

### Вход

- Драйвер (`IDriverMappedParameters`) получает ссылку на `_parametersMaps` через `SetIndex()`
- Драйвер заполняет словарь: ключ — `FullName` типа `IMappedModel`, значение — `ParametersMap`

### Выход

- `GetParameters<T>()` — массив `GenericParameter[]` из карты (новые экземпляры при каждом вызове)
- `GetModel<T>()` — создаёт экземпляр `T` через `Activator.CreateInstance`, инициализирует картой
- `InitMap(model)` — инициализирует существующий экземпляр `IMappedModel`

### API

| Метод | Описание |
|-------|----------|
| `ParameterMaps.GetParameters<T>()` | Массив параметров по типу модели |
| `ParameterMaps.GetParameters(Type)` | Массив параметров по `Type` |
| `ParameterMaps.GetParameters(string)` | Массив параметров по `FullName` |
| `ParameterMaps.GetModel<T>()` | Новый экземпляр модели, инициализированный картой |
| `ParameterMaps.GetModel(Type)` | Новый экземпляр модели по `Type` |
| `ParameterMaps.InitMap(IMappedModel)` | Инициализация существующего экземпляра |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Значения `GenericParameter` — только `int` | Достаточно для большинства игровых механик |
| GUID карты = `FullName` типа модели | Однозначная привязка тип → карта |
| `GetParameters()` создаёт новый массив | Каждый вызов — независимые экземпляры `GenericParameter` |
| `GetModel<T>()` требует безпараметрический конструктор | `Activator.CreateInstance(type)` |
| `null` при ошибках | Тип не `IMappedModel`, карта не найдена, `FullName == null` |

---

## Использование

### Реализация модели данных

```csharp
public class CharacterStats : IMappedModel
{
    public event Action OnUpdate;

    private Dictionary<string, GenericParameter> _parameters = new();
    private ParametersMap _map;

    public void Init(ParametersMap map)
    {
        _map = map;
        _parameters.Clear();
        foreach (var param in map.GetParameters())
        {
            param.OnUpdate += () => OnUpdate?.Invoke();
            _parameters[param.Name] = param;
        }
    }

    public string[] GetParameters() => _parameters.Keys.ToArray();
    public int GetValue(string name) => _parameters.TryGetValue(name, out var p) ? p.Value : 0;
    public GenericParameter GetParameterAsContainer(string name) => _parameters.GetValueOrDefault(name);
    public IParameterLink[] GetParents(string name) => _map?.GetParameterMap(name)?.Parents ?? Array.Empty<IParameterLink>();
}
```

### Получение модели в runtime

```csharp
// Новый экземпляр
var stats = ParameterMaps.GetModel<CharacterStats>() as CharacterStats;

// Инициализация существующего
var stats = new CharacterStats();
ParameterMaps.InitMap(stats);

// Работа с параметрами
var strength = stats.GetParameterAsContainer("Strength");
strength.OnUpdate += () => Console.WriteLine($"Strength: {strength.Value}");
strength.SetValue(10);
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Тип не реализует `IMappedModel` | `GetParameters` / `GetModel` → `null` |
| Карта не найдена по `FullName` | `GetParameters` → `null`, `GetModel` → `null` |
| `FullName == null` | `GetModel` / `InitMap` → `null` / no-op |
| `SetValue` с тем же значением | Не вызывает `OnUpdate` |
| `GetParameters()` на `ParametersMap` | Каждый вызов — новый массив с новыми `GenericParameter` |
| Пустой словарь `_parametersMaps` | Все запросы → `null` |

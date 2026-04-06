# ReactiveValues

**Namespace:** `Vortex.Core.Extensions.ReactiveValues`
**Сборка:** `ru.vortex.extensions`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Реактивные обёртки над простыми типами данных. При изменении значения уведомляют подписчиков.

Возможности:
- Типизированное событие `OnUpdate` с новым значением
- Нетипизированное событие `OnUpdateData` (интерфейс `IReactiveData`)
- Implicit-оператор для чтения без `.Value`
- Интерфейс `IReactiveData` помечен `[POCO]` — все реализации автоматически сериализуемы через `SerializeController`

Вне ответственности:
- Потокобезопасность
- Валидация значений
- Подавление дубликатов (событие при каждом `Set`, даже если значение не изменилось)

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.Extensions.LogicExtensions.SerializationSystem` | Атрибут `[POCO]` на `IReactiveData` |

---

## Архитектура

```
IReactiveData [POCO]                     ← интерфейс: event OnUpdateData
└── ReactiveValue<T> (abstract)          ← Value, Set(), OnUpdate, OnUpdateData, implicit operator T
    ├── IntData                          ← ReactiveValue<int>
    ├── FloatData                        ← ReactiveValue<float>
    ├── BoolData                         ← ReactiveValue<bool>
    └── StringData                       ← ReactiveValue<string>, ToString()
```

### Компоненты

| Класс | Назначение |
|-------|-----------|
| `IReactiveData` | Интерфейс с `event Action OnUpdateData`. Помечен `[POCO]` |
| `ReactiveValue<T>` | Абстрактная обёртка: `Value`, `Set(T)`, `OnUpdate`, implicit operator |
| `IntData` | `ReactiveValue<int>` |
| `FloatData` | `ReactiveValue<float>` |
| `BoolData` | `ReactiveValue<bool>` |
| `StringData` | `ReactiveValue<string>`, переопределяет `ToString()` |

---

## Контракт

### API

| Метод / Свойство | Описание |
|------------------|----------|
| `Value` | Текущее значение (public get, protected set) |
| `Set(T value)` | Устанавливает значение и вызывает оба события |
| `OnUpdate` | `event Action<T>` — типизированное уведомление |
| `OnUpdateData` | `event Action` — нетипизированное уведомление (из `IReactiveData`) |
| `implicit operator T` | Чтение значения без `.Value` |

### Гарантии
- `Set()` всегда вызывает `OnUpdate` и `OnUpdateData`, даже если значение не изменилось
- `implicit operator` позволяет использовать `ReactiveValue<T>` везде где ожидается `T`
- Все наследники конструируются с начальным значением: `new IntData(0)`
- `[POCO]` на `IReactiveData` делает все реализации сериализуемыми через `SerializeController`

### Ограничения
- Нет конструктора без параметров — десериализация через `FormatterServices.GetUninitializedObject()`
- `Set()` не проверяет равенство — событие при каждом вызове
- Не потокобезопасен

---

## Использование

### Объявление

```csharp
public class PlayerModel
{
    public IntData Level { get; set; } = new IntData(1);
    public StringData Name { get; set; } = new StringData("Player");
    public BoolData IsAlive { get; set; } = new BoolData(true);
}
```

### Подписка на изменения

```csharp
var model = new PlayerModel();

// Типизированная подписка
model.Level.OnUpdate += newLevel => Debug.Log($"Level: {newLevel}");

// Нетипизированная подписка (IReactiveData)
model.Level.OnUpdateData += () => Debug.Log("Level changed");
```

### Implicit operator

```csharp
int level = model.Level;           // implicit operator
string name = model.Name;          // implicit operator
if (model.IsAlive) { /* ... */ }   // implicit operator
```

### Изменение значения

```csharp
model.Level.Set(5);   // вызовет OnUpdate(5) и OnUpdateData
```

### Использование с QuestController

```csharp
// IReactiveData позволяет подписаться на изменения для перепроверки условий квестов
QuestController.SetListener(model.Level, this);
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `Set()` с тем же значением | Событие вызывается |
| `implicit operator` на null | NRE — `ReactiveValue` не nullable |
| Десериализация без конструктора | Fallback на `FormatterServices.GetUninitializedObject()` |
| `[POCO]` на `IReactiveData` | Все `ReactiveValue<T>` наследники сериализуемы автоматически |

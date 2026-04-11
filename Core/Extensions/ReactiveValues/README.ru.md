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
- Владелец контейнера — только владелец может изменять значение через `Set()`
- Интерфейс `IReactiveData` помечен `[POCO]` — все реализации автоматически сериализуемы через `SerializeController`

Вне ответственности:
- Потокобезопасность
- Валидация значений
- Потокобезопасная подписка/отписка

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.Extensions.LogicExtensions.SerializationSystem` | Атрибут `[POCO]` на `IReactiveData` |
| `Vortex.Core.LoggerSystem` | `Log.Print` для ошибок владения |

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
| `ReactiveValue<T>` | Абстрактная обёртка: `Value`, `Set(T, owner)`, `SetOwner()`, `ForceUpdate()`, `OnUpdate`, implicit operator |
| `IntData` | `ReactiveValue<int>`. Конструкторы: `(int)`, `(int, object owner)` |
| `FloatData` | `ReactiveValue<float>`. Конструкторы: `(float)`, `(float, object owner)` |
| `BoolData` | `ReactiveValue<bool>`. Конструкторы: `(bool)`, `(bool, object owner)` |
| `StringData` | `ReactiveValue<string>`, `ToString()`. Конструкторы: `(string)`, `(string, object owner)` |

---

## Контракт

### API

| Метод / Свойство | Описание |
|------------------|----------|
| `Value` | Текущее значение (public get, protected set) |
| `Set(T value, object owner = null)` | Устанавливает значение. Если значение не изменилось — игнорируется. Если назначен владелец — только он может менять значение |
| `SetOwner(object owner)` | Назначить владельца контейнера. Повторное назначение запрещено |
| `ForceUpdate()` | Принудительный вызов `OnUpdate` и `OnUpdateData` без изменения значения |
| `OnUpdate` | `event Action<T>` — типизированное уведомление |
| `OnUpdateData` | `event Action` — нетипизированное уведомление (из `IReactiveData`) |
| `implicit operator T` | Чтение значения без `.Value` |

### Гарантии
- `Set()` вызывает события только при изменении значения (дедупликация через `EqualityComparer<T>.Default`)
- `Set()` с неверным владельцем логирует ошибку и не меняет значение
- `SetOwner()` запрещает повторное назначение — логирует ошибку
- Без владельца (`_owner == null`) `Set()` работает без ограничений
- `ForceUpdate()` вызывает события без проверки на изменение значения
- `implicit operator` позволяет использовать `ReactiveValue<T>` везде где ожидается `T`
- Все наследники конструируются с начальным значением: `new IntData(0)` или с владельцем: `new IntData(0, owner)`
- `[POCO]` на `IReactiveData` делает все реализации сериализуемыми через `SerializeController`

### Ограничения
- Нет конструктора без параметров — десериализация через `FormatterServices.GetUninitializedObject()`
- Владелец назначается однократно и не может быть снят
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
model.Level.Set(5);   // повторный вызов — значение не изменилось, события НЕ вызываются
```

### Владелец контейнера

```csharp
// Контроллер назначает себя владельцем
model.Level.SetOwner(this);

// Только владелец может менять значение
model.Level.Set(10, this);    // OK
model.Level.Set(10, other);   // Error: "Trying to change value from outer Object."
model.Level.Set(10);          // Error: owner = null != this
```

### Конструктор с владельцем

```csharp
// Контейнер с владельцем сразу при создании
var hp = new IntData(100, this);
hp.Set(90, this);    // OK
hp.Set(90, other);   // Error
```

### Принудительное обновление

```csharp
// Вызвать события без изменения значения
model.Level.ForceUpdate();
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
| `Set()` с тем же значением | Игнорируется, события не вызываются |
| `ForceUpdate()` | Вызывает `OnUpdate` и `OnUpdateData` с текущим значением |
| `implicit operator` на null | NRE — `ReactiveValue` не nullable |
| Десериализация без конструктора | Fallback на `FormatterServices.GetUninitializedObject()` |
| `[POCO]` на `IReactiveData` | Все `ReactiveValue<T>` наследники сериализуемы автоматически |
| `Set()` без владельца при назначенном `_owner` | Ошибка — `owner = null` не равен `_owner` |
| `SetOwner(null)` | Игнорируется (ранний return) |
| Повторный `SetOwner()` | Ошибка, владелец не переназначается |

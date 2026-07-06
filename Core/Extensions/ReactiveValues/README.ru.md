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
- Editor-генератор реактивной обёртки для произвольного класса (правый клик по `.cs` → Create → Vortex Templates → Create ReactiveData)

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
├── ReactiveValue<T> (abstract)          ← одиночное значение
│   ├── IntData                          ← ReactiveValue<int>
│   ├── FloatData                        ← ReactiveValue<float>
│   ├── BoolData                         ← ReactiveValue<bool>
│   ├── StringData                       ← ReactiveValue<string>, ToString()
│   └── EnumData<TEnum>                  ← ReactiveValue<TEnum> where TEnum : Enum
│
└── ReactiveCollection<T> (abstract)     ← реактивный List<T>: Add/Remove/Insert/Sort/...
    └── ListData<T>                      ← конструкторы (пустой / из List<T>)
```

### Компоненты

| Класс | Назначение |
|-------|-----------|
| `IReactiveData` | Интерфейс с `event Action OnUpdateData`. Помечен `[POCO]` |
| `ReactiveValue<T>` | Обёртка одиночного значения: `Value`, `Set(T, owner)`, `SetOwner()`, `ForceUpdate()`, `OnUpdate`, implicit operator |
| `IntData` | `ReactiveValue<int>`. Конструкторы: `(int)`, `(int, object owner)` |
| `FloatData` | `ReactiveValue<float>`. Конструкторы: `(float)`, `(float, object owner)` |
| `BoolData` | `ReactiveValue<bool>`. Конструкторы: `(bool)`, `(bool, object owner)` |
| `StringData` | `ReactiveValue<string>`, `ToString()`. Конструкторы: `(string)`, `(string, object owner)` |
| `EnumData<TEnum>` | `ReactiveValue<TEnum>` для любого `Enum`. Конструкторы: `(TEnum)`, `(TEnum, object owner)` |
| `ReactiveCollection<T>` | Абстрактный реактивный `List<T>`: `OnUpdate(IReadOnlyList<T>)`, мутаторы Add/Remove/Insert/Sort/Reverse/Clear/RemoveAt/RemoveRange/Set(index, …)/Set(List<T>), `GetList()`, индексатор по чтению, `SetOwner`/`ReleaseOwner`/`ForceUpdate`. Identity внутреннего списка сохраняется навсегда. Конструкторов нет — использовать `ListData<T>` |
| `ListData<T>` | Канонический наследник `ReactiveCollection<T>` с инициализирующими конструкторами: `()` — пустой, `(List<T>)` — копия переданного |

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

### Обёртка для своего типа

Встроенные `IntData`/`BoolData`/… покрывают примитивы. Для доменного типа реактивная обёртка пишется по тому же паттерну — наследник `ReactiveValue<T>` с двумя конструкторами:

```csharp
public class ItemData : ReactiveValue<Item>
{
    public ItemData(Item value) => Value = value;

    public ItemData(Item value, object owner)
    {
        Value = value;
        _owner = owner;
    }
}
```

Чтобы не писать это вручную — генератор: правый клик по `.cs`-скрипту в Project → **Create → Vortex Templates → Create ReactiveData**. Рядом с исходником создаётся `{ClassName}Data.cs` с такой обёрткой; `namespace` берётся из исходного класса. Если файл уже существует — спросит про перезапись.

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

### Запирание приватным ключом (рекомендуется)

Передавать `this` в качестве владельца — допустимо, но **протекаемо**: ссылка на контроллер обычно публично доступна, и любой код может вызвать `data.Set(value, controller)`, выдавая себя за владельца. Замок теряет смысл.

Чистый паттерн — отдельный приватный ключ внутри контроллера:

```csharp
public class HealthController
{
    private readonly object _key = new();
    public IntData Hp { get; } = new IntData(100);

    public HealthController()
    {
        Hp.SetOwner(_key);
    }

    public void Damage(int amount)
    {
        Hp.Set(Hp - amount, _key);   // OK
    }
}

// Снаружи:
controller.Hp.Set(0, controller);  // Error — `controller` не ключ
controller.Hp.Set(0);              // Error — null не ключ
```

`_key` — `private`, никем извне не виден, инстанцируется внутри контроллера и нигде не публикуется. Ни рефлексия (по типу `object`), ни случайная утечка ссылки не дадут постороннему обойти замок.

Тот же подход работает для `ReactiveCollection<T>` (`ListData<T>` и т. д.) и для конструкторов с владельцем:

```csharp
public IntData Hp { get; }

public HealthController()
{
    Hp = new IntData(100, _key);   // владелец сразу из ключа
}
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

### Реактивная коллекция

```csharp
public class InventoryModel
{
    public ListData<ItemId> Items { get; private set; } = new();
}

inventory.Items.OnUpdate += list => RefreshUI(list);   // IReadOnlyList<ItemId>
inventory.Items.OnUpdateData += () => Counter.Refresh(); // нетипизированная нотификация

inventory.Items.Add(itemId);                 // OnUpdate срабатывает
inventory.Items.Remove(missingId);           // не найден → OnUpdate НЕ срабатывает
inventory.Items.SetOwner(this);              // далее только this может мутировать
inventory.Items.Add(itemId, this);           // OK
inventory.Items.Add(itemId, other);          // Error: чужой owner

var snapshot = inventory.Items.GetList();    // IReadOnlyList<ItemId>, доступ только для чтения
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
| `ReactiveValue<T>` над ссылочным типом (обёртка своего класса) | `Set()` дедуплицирует через `EqualityComparer<T>.Default`: для класса без переопределённого `Equals` это ссылочное равенство — мутация полей того же экземпляра не вызовет `OnUpdate`, нужна установка нового экземпляра или `ForceUpdate()` |
| `Set()` без владельца при назначенном `_owner` | Ошибка — `owner = null` не равен `_owner` |
| `SetOwner(null)` | Игнорируется (ранний return) |
| Повторный `SetOwner()` | Ошибка, владелец не переназначается |
| `ReactiveCollection<T>` инстанцирование напрямую | Невозможно — класс абстрактный. Использовать наследник `ListData<T>` |
| `ReactiveCollection.Remove(v)` для отсутствующего элемента | События не вызываются (изменений не было) |
| `ReactiveCollection.Set(index, value)` с тем же значением | Дедупликации нет — событие всё равно вызывается (в отличие от `ReactiveValue<T>.Set`) |
| `ReactiveCollection.Set(index, value)` / `RemoveAt(index)` / `RemoveRange(...)` / `Insert(...)` с невалидным индексом | `ArgumentOutOfRangeException` / `ArgumentException` (стандартное поведение `List<T>`). Событие не вызывается |
| `ReactiveCollection.Sort()` для `T` без `IComparable<T>` | `InvalidOperationException`. Перегрузка с компаратором отсутствует |
| `ReactiveCollection.SetOwner` повторно | Ошибка, владелец не переназначается. Сначала `ReleaseOwner(currentOwner)` |
| `ReactiveCollection.ReleaseOwner` с чужим ключом | Ошибка, владелец не сбрасывается |
| `ReactiveCollection.GetList()` | Возвращает `ReadOnlyCollection<T>`-обёртку над внутренним списком. Снэпшот **живой** — отражает все последующие мутации (Add/Remove/Insert/Sort/...) и полную замену через `Set(List<T>)` (которая использует Clear+AddRange и сохраняет identity внутреннего списка). Напрямую изменить нельзя |
| `new ListData<T>(list)` | Хранит копию переданного списка (через `.ToList()`); мутации источника не влияют |
| `new ListData<T>(null)` | `NullReferenceException`. Для пустого списка использовать параметрless-конструктор |

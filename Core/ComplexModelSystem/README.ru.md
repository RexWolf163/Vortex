# ComplexModelSystem

Базовый класс для составных моделей данных, чья структура определяется подключёнными пакетами.

## Назначение

Автоматическая сборка модели из всех конкретных реализаций интерфейса/базового класса `T`, найденных в загруженных сборках. Сериализация и десериализация через `SerializeController`.

- Сканирование `AppDomain.CurrentDomain.GetAssemblies()` на реализации `T`
- Индексация экземпляров по типу: `Dictionary<Type, T>`
- Доступ к компонентам по типу: `Get<TU>()`
- Кэширование найденных типов (по `T`) — повторные `Init()` не сканируют сборки
- Сериализация/десериализация через `SerializeController` (property-based JSON)

Вне ответственности: создание экземпляров с параметрами конструктора, регистрация в Database, Unity-специфичная логика.

## Зависимости

- `Vortex.Core.Extensions.LogicExtensions.SerializationSystem` — `SerializeProperties()`, `DeserializeProperties<T>()`
- `Vortex.Core.LoggerSystem` — логирование ошибок

## Архитектура

```
ComplexModel<T> (abstract, Serializable)
├── Cache          — static Dictionary<Type, Type[]> (кэш типов по T)
├── Index          — Dictionary<Type, T> (экземпляры компонентов)
├── Init()         — сканирование сборок или восстановление из кэша
├── Get<TU>()      — доступ к компоненту по типу
├── Serialize()    — → JSON (вызывает BeforeSerialization/AfterSerialization)
├── Deserialize()  — ← JSON (вызывает BeforeDeserialization/AfterDeserialization)
└── abstract hooks — BeforeSerialization, AfterSerialization, BeforeDeserialization, AfterDeserialization
```

### Init()

1. Очистка `Index`
2. Если `Cache` содержит типы для `T` — создание экземпляров из кэша (`Activator.CreateInstance`)
3. Иначе — сканирование всех сборок: поиск не-abstract, не-interface типов, assignable от `T`, с безпараметрическим конструктором
4. Сохранение найденных типов в `Cache[typeof(T)]`

### Требования к T

- Конкретные реализации должны иметь безпараметрический конструктор
- Не abstract, не interface
- `class` constraint

## Контракт

### Вход
- Вызов `Init()` для сканирования и создания экземпляров
- `Deserialize(string)` для восстановления из JSON

### Выход
- `Get<TU>()` — типизированный доступ к компоненту
- `Serialize()` — JSON-строка через `SerializeController`

### Гарантии
- Кэш типов — повторные `Init()` не сканируют сборки (для одного `T`)
- `Get<TU>()` при отсутствии типа — `null` + лог `Error`
- `Deserialize(null/empty)` — лог `Error`, индекс не изменяется
- Исключения при сканировании сборок перехватываются — лог `Warning`

### Ограничения
- Один экземпляр на тип — дубликаты типов невозможны
- `Activator.CreateInstance` — только безпараметрические конструкторы
- Кэш статический по `T` — общий для всех экземпляров `ComplexModel<T>`
- Сериализация через `SerializeController` (experimental)

## Использование

### Определение модели

```csharp
public interface IPlayerData { }

public class HealthData : IPlayerData
{
    public int Hp { get; set; } = 100;
}

public class InventoryData : IPlayerData
{
    public List<string> Items { get; set; } = new();
}

public class PlayerModel : ComplexModel<IPlayerData>
{
    protected override void BeforeSerialization() { }
    protected override void AfterSerialization() { }
    protected override void BeforeDeserialization() { }
    protected override void AfterDeserialization() { }
}
```

### Инициализация и доступ

```csharp
var model = new PlayerModel();
model.Init(); // найдёт HealthData, InventoryData

var health = model.Get<HealthData>();
health.Hp -= 10;

var inventory = model.Get<InventoryData>();
inventory.Items.Add("sword");
```

### Сериализация

```csharp
string json = model.Serialize();

var restored = new PlayerModel();
restored.Init();
restored.Deserialize(json);
```

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `Get<TU>()` с незарегистрированным типом | `null` + лог `Error` |
| `Init()` без реализаций `T` | Пустой `Index`, работает без ошибок |
| Повторный `Init()` | `Index` очищается и пересоздаётся из кэша |
| Сборка выбрасывает исключение при `GetTypes()` | Перехват, лог `Warning`, продолжение сканирования |
| `Deserialize("")` / `Deserialize(null)` | Лог `Error`, `Index` не изменяется |
| Два разных `ComplexModel<T>` с одним `T` | Общий `Cache`, независимые `Index` |

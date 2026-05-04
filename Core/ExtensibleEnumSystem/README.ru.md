# StateAxisSystem (Core)

**Namespace:** `Vortex.Core.StateAxisSystem.Abstractions`, `Vortex.Core.StateAxisSystem.Extensions`
**Сборка:** `ru.vortex.stateaxis`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Type-safe enum-подобные оси состояния, расширяемые проектом без правки фреймворка.
Альтернатива обычному `enum` для случаев, когда набор значений зависит от проекта или предметной области (состояния персонажа, типы боевых стоек, режимы взгляда), но требуется compile-time эргономика.

Возможности:

- `StateAxis` — абстрактная база с авто-реестром инстансов через ctor
- `StateValue<T>` — реактивное значение оси (`ReactiveValue<T>`-наследник)
- Сериализация/десериализация через формат `"{Namespace.AxisName}.{Key}"`
- Lookup по ключу, индексу, типу через статические методы базы

Вне ответственности:

- Кодогенерация конкретных классов осей — Layer 2 (`Vortex.Unity.StateAxisSystem`)
- Inspector-атрибуты, дропдауны, парные ассеты — Layer 2
- UI-интеграция (`UIStateSwitcher`) — пакет `Vortex.Unity.UI.StateSwitcher`

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.Extensions` | `ReactiveValue<T>`, `IReactiveData`, `SerializeController` |

Регистрация custom-конвертера в `SerializeController` происходит автоматически в static-конструкторе `StateAxis` — это гарантирует, что при первой загрузке любого наследника `StateAxis` сериализатор уже знает, как с ним работать.

---

## Архитектура

### StateAxis

```
StateAxis (abstract, IEquatable<StateAxis>)
  ├── Key: string                                     ← стабильный идентификатор значения
  ├── Order: int                                      ← позиция в осевом порядке
  ├── ctor(string key, int order)                     ← protected, регистрирует this в ByKey/Ordered
  ├── ToString() → "{TypeName}.{Key}"                 ← короткое представление для логов
  ├── Serialize() → "{FullName}.{Key}"                ← формат для save/load
  ├── Equals(StateAxis other)                         ← сравнение по типу + ключу
  └── static {
        ByKey:    Dictionary<Type, Dictionary<string, StateAxis>>
        Ordered:  Dictionary<Type, List<StateAxis>>
        GetByKey<T>(string)        → T
        GetByKey(Type, string)     → StateAxis
        GetAll<T>()                → IReadOnlyList<T>
        GetAll(Type)               → IReadOnlyList<StateAxis>
        GetMap(Type)               → IReadOnlyDictionary<string, StateAxis>
        Deserialize(string)        → StateAxis
        Deserialize<T>(string)     → T
      }
```

Конкретный класс оси создаётся как `sealed`-наследник со статическими `readonly`-полями:

```csharp
public sealed class MoveState : StateAxis
{
    public static readonly MoveState Idle = new(nameof(Idle), 0);
    public static readonly MoveState Walk = new(nameof(Walk), 1);
    public static readonly MoveState Run  = new(nameof(Run),  2);

    public static IReadOnlyList<MoveState> All => GetAll<MoveState>();

    private MoveState(string key, int order) : base(key, order) { }
}
```

При первой ссылке на `MoveState` сработают:
1. Static-инициализатор базы `StateAxis` → регистрация custom-конвертера в `SerializeController`.
2. Static-инициализаторы полей `MoveState` → каждый `new MoveState(...)` вызывает базовый ctor → запись в `ByKey[typeof(MoveState)]` и `Ordered[typeof(MoveState)]`.

После этого `StateAxis.GetAll<MoveState>()` возвращает все четыре значения в порядке `Order`.

### StateValue\<T\>

```
StateValue<T> : ReactiveValue<T> where T : StateAxis
  ├── Key: string                ← Value?.Key
  ├── Index: int                 ← Value?.Order ?? -1
  ├── Is(T other)                ← ReferenceEquals(Value, other)
  └── IsKey(string key)          ← сравнение по ключу
```

`Value` — singleton-инстанс типа `T`. Сравнение по ссылке корректно, потому что значения всегда одни и те же объекты (`MoveState.Run` — единственный экземпляр).

### StateAxisTypeCache (internal)

Lazy-кеш `FullName → Type` для всех не-абстрактных наследников `StateAxis`, заполняется при первом обращении из `Deserialize`. Скан `AppDomain.CurrentDomain.GetAssemblies()`, фильтр по `IsAssignableFrom(typeof(StateAxis))`. Кеш строится один раз; не инвалидируется на перекомпиляции в Editor — для рантайма это не нужно, в Editor домен пересоздаётся.

---

## Контракт сериализации

### Формат

```
"{Namespace.AxisName}.{Key}"
```

Примеры:
- `"MyGame.States.MoveState.Run"`
- `"Vortex.Demo.CombatState.Block"`

Разделитель — **последняя точка** строки. Левая часть — `Type.FullName` (включая namespace), правая — `Key`.

### Ограничения на имена

Чтобы разделитель работал однозначно:
- `AxisName` (имя класса) **не должно содержать точек** (это требование C# и так выполняется автоматически).
- `Key` (имена значений) **не должны содержать точек**. Layer 2 генератор `StateAxisCodeGenerator` enforce'ит это правило при сохранении пресета.

### Регистрация в SerializeController

Конвертер регистрируется один раз в static-ctor `StateAxis`:

```csharp
SerializeController.RegisterCustomSerializer(
    matches:     t => typeof(StateAxis).IsAssignableFrom(t),
    serialize:   obj => ((StateAxis)obj).Serialize(),
    deserialize: (t, s) => Deserialize(s)
);
```

После регистрации любое свойство типа `StateAxis`-наследника или `StateValue<T>.Value` сериализуется как обычная JSON-строка, без раскрытия в объект.

---

## Использование

### Объявление оси (вручную или сгенерированно)

```csharp
public sealed class CombatState : StateAxis
{
    public static readonly CombatState Idle  = new(nameof(Idle),  0);
    public static readonly CombatState Block = new(nameof(Block), 1);
    public static readonly CombatState Parry = new(nameof(Parry), 2);

    public static IReadOnlyList<CombatState> All => GetAll<CombatState>();

    private CombatState(string key, int order) : base(key, order) { }
}
```

### Использование в модели

```csharp
public class CharacterModel
{
    public StateValue<MoveState>   MoveMode   { get; } = new(MoveState.Idle);
    public StateValue<CombatState> CombatMode { get; } = new(CombatState.Idle);
}
```

### Чтение, установка, проверки

```csharp
character.MoveMode.Set(MoveState.Run);                      // type-safe ✓
if (character.MoveMode.Value == MoveState.Run) { ... }      // reference equality
if (character.MoveMode.Is(MoveState.Run)) { ... }           // явная проверка
character.MoveMode.OnUpdate += v => Refresh(v);             // реактивность
int slot = character.MoveMode.Index;                        // → UIStateSwitcher
string key = character.MoveMode.Key;                        // → save без обёртки StateValue
```

### Сериализация / десериализация

```csharp
var json = character.SerializeProperties();
// поле MoveMode: "Value" : "MyGame.States.MoveState.Run"

var restored = json.DeserializeProperties<CharacterModel>();
restored.MoveMode.Value == MoveState.Run;                    // ✓ ссылочное равенство
```

### Lookup по ключу

```csharp
var run = StateAxis.GetByKey<MoveState>("Run");              // → MoveState.Run
var any = StateAxis.GetByKey(typeof(MoveState), "Walk");     // → MoveState.Walk

foreach (var s in StateAxis.GetAll<MoveState>())
    Console.WriteLine($"{s.Order}: {s.Key}");
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `GetByKey<T>("…")` для незарегистрированного ключа | Возвращает `null` |
| `GetAll<T>()` до первой ссылки на `T` | Возвращает пустой массив (static-инициализатор T ещё не сработал) |
| `Deserialize("…")` для несуществующего типа | Возвращает `null` |
| `Deserialize("…")` для существующего типа, но неизвестного ключа | Возвращает `null` |
| `Deserialize("BadString")` без точки | Возвращает `null` |
| `StateValue<T>.Value == null` | `Key = null`, `Index = -1`, `Is(...) = false` |
| `StateValue<T>.Set(null)` | Допустимо; всё реактивное поведение `ReactiveValue<T>` сохраняется |
| Дублирование Key в наследнике | Последний `new` перезатирает предыдущий в `ByKey` (некорректное использование, диагностируется L2 валидатором) |
| Параллельные потоки | Реестр не потокобезопасен; static-инициализаторы выполняются один раз в гарантированной последовательности .NET, но изменения после инициализации (повторные ctor) — не защищены |

---

## Контракт публичного API

```csharp
// Identity + сериализация
public abstract class StateAxis : IEquatable<StateAxis>
{
    public string Key { get; }
    public int Order { get; }
    
    protected StateAxis(string key, int order);
    
    public string Serialize();
    
    public static T          GetByKey<T>(string key)  where T : StateAxis;
    public static StateAxis  GetByKey(Type axisType, string key);
    public static IReadOnlyList<T>          GetAll<T>()  where T : StateAxis;
    public static IReadOnlyList<StateAxis>  GetAll(Type axisType);
    public static IReadOnlyDictionary<string, StateAxis> GetMap(Type axisType);
    
    public static StateAxis  Deserialize(string serialized);
    public static T          Deserialize<T>(string serialized) where T : StateAxis;
}

// Реактивная обёртка
public class StateValue<T> : ReactiveValue<T> where T : StateAxis
{
    public StateValue();
    public StateValue(T initial);
    
    public string Key { get; }
    public int    Index { get; }
    public bool   Is(T other);
    public bool   IsKey(string key);
}
```

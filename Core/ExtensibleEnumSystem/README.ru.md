# ExtensibleEnumSystem (Core)

**Namespace:** `Vortex.Core.ExtensibleEnumSystem.Abstractions`, `Vortex.Core.ExtensibleEnumSystem.Extensions`
**Сборка:** `ru.vortex.extenums`
**Платформа:** Unity / .NET Standard 2.1+

---

## Назначение

Type-safe enum-подобные расширяемые наборы значений, дополняемые проектом без правки фреймворка.
Альтернатива обычному `enum` для случаев, когда набор значений зависит от проекта или предметной области (состояния персонажа, типы боевых стоек, режимы взгляда), но требуется compile-time эргономика.

Возможности:

- `ExtensibleEnum` — абстрактная база с авто-реестром инстансов через ctor.
- `ExtEnumData<T>` — реактивное значение типа-наследника (`ReactiveValue<T>`-наследник).
- Сериализация/десериализация через формат `"{Namespace.TypeName}.{Key}"`.
- Eager-инициализация реестра до загрузки сцены и при старте Editor-домена.
- Lookup по ключу, по типу через статические методы базы.

Вне ответственности:

- Кодогенерация, парные SO-ассеты, валидаторы — **не предусмотрены**. Конкретные классы пишутся руками: `sealed class MoveState : ExtensibleEnum { … }`.
- Inspector-атрибуты, дропдауны — Layer 2 (`Vortex.Unity.ExtensibleEnumSystem`).
- UI-интеграция (`UIStateSwitcher`) — пакет `Vortex.Unity.UI.StateSwitcher`.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.Extensions` | `ReactiveValue<T>`, `IReactiveData`, `SerializeController` |
| UnityEngine | `[RuntimeInitializeOnLoadMethod]` для eager-инициализации в рантайме |
| UnityEditor (под `#if UNITY_EDITOR`) | `[InitializeOnLoadMethod]` для eager-инициализации в Editor-домене |

Регистрация custom-конвертера в `SerializeController` происходит автоматически в static-конструкторе `ExtensibleEnum` — это гарантирует, что при первой загрузке базы (что произойдёт при первой же `Initialize`) сериализатор уже знает, как с ней работать.

---

## Архитектура

### ExtensibleEnum

```
ExtensibleEnum (abstract, IEquatable<ExtensibleEnum>)
  ├── Key: string                                     ← стабильный идентификатор значения
  ├── Order: int                                      ← позиция в логическом порядке
  ├── ctor(string key, int order)                     ← protected, регистрирует this в ByKey/Ordered
  ├── ToString() → "{TypeName}.{Key}"                 ← короткое представление для логов
  ├── Serialize() → "{FullName}.{Key}"                ← формат для save/load
  ├── Equals(ExtensibleEnum other)                    ← сравнение по типу + ключу
  └── static {
        ByKey:       Dictionary<Type, Dictionary<string, ExtensibleEnum>>
        Ordered:     Dictionary<Type, List<ExtensibleEnum>>
        ByFullName:  Dictionary<string, Type>          ← для Deserialize, заполняется eager-init
        
        [RuntimeInitializeOnLoadMethod] InitializeOnRuntime() → Initialize()
        [InitializeOnLoadMethod]        InitializeOnEditor()  → Initialize()  (#if UNITY_EDITOR)
        Initialize() — скан AppDomain + RunClassConstructor для всех наследников + populate ByFullName
        
        GetByKey<T>(string)        → T
        GetByKey(Type, string)     → ExtensibleEnum
        GetAll<T>()                → IReadOnlyList<T>
        GetAll(Type)               → IReadOnlyList<ExtensibleEnum>
        GetMap(Type)               → IReadOnlyDictionary<string, ExtensibleEnum>
        Deserialize(string)        → ExtensibleEnum
        Deserialize<T>(string)     → T
      }
```

Конкретный набор значений создаётся как наследник `ExtensibleEnum` со статическими `readonly`-полями. Поддерживаются два режима объявления — выбор зависит от того, должен ли набор быть открыт для расширения.

#### Закрытый набор — `sealed` + `private` ctor

Когда набор фиксированный и не должен дополняться извне:

```csharp
public sealed class MoveState : ExtensibleEnum
{
    public static readonly MoveState Idle = new(nameof(Idle), 0);
    public static readonly MoveState Walk = new(nameof(Walk), 1);
    public static readonly MoveState Run  = new(nameof(Run),  2);

    public static IReadOnlyList<MoveState> All => GetAll<MoveState>();

    private MoveState(string key, int order) : base(key, order) { }
}
```

#### Расширяемый набор — `partial` + `public` ctor

Когда набор задаёт минимум базовых значений и должен дополняться проектом или другим частичным файлом в той же сборке:

```csharp
// Sdk/.../MoveState.cs — базовые значения
public partial class MoveState : ExtensibleEnum
{
    public static readonly MoveState Stay = new(nameof(Stay), 0);
    public static readonly MoveState Move = new(nameof(Move), 1);

    public MoveState(string key, int order) : base(key, order) { }
}

// Project/States/MoveState.Extra.cs — расширение в той же сборке
public partial class MoveState
{
    public static readonly MoveState Run  = new(nameof(Run),  2);
    public static readonly MoveState Jump = new(nameof(Jump), 3);
}
```

Правила:
- Конструктор объявляется `public` (или `internal`) — иначе вторая partial-часть не сможет вызвать `new`.
- `Order` в расширении **продолжается** с последнего занятого индекса базовой части. Дубликаты `Order` допустимы технически, но ломают слот-маппинг для `UIStateSwitcher`.
- Дубликаты `Key` в любой partial-части перезатирают друг друга в реестре `ByKey` — авторская ошибка, eager-init её не диагностирует.
- Расширение через `partial` работает **в пределах одной сборки**. Для расширения из проектной сборки, использующей пакет как зависимость, см. ниже.

#### Канонический референс

[`Vortex.Sdk.CharacterViewSystem.Models.States`](../../Sdk/CharacterViewSystem/Models/States/) — пример минимальных базовых наборов, открытых для расширения через `partial`:
- `MoveState`: `Stay`, `Move` — базовый минимум; проект добавляет `Run`, `Jump`, `Crouch` под свою механику.
- `DirectionState`: 6 канонических направлений (`North`/`East`/`South`/`West`/`Up`/`Down`); проект добавляет диагонали при необходимости.
- `ActionState`: `Idle`, `Speak`, `Use` — типовые игровые действия.
- `CombatState`: `Idle`, `Attack`, `Defence` — базовая боевая модель.
- `MoveSubStateDirection`: `Forward`, `Back`, `Side` — поднабор направления движения относительно взгляда.

Каждый класс объявлен как `public partial class … : ExtensibleEnum` с публичным конструктором — это и есть канонический способ задавать расширяемые наборы во фреймворке.

#### Расширение из чужой сборки

Если проект-потребитель сидит в отдельной asmdef и хочет добавить свои значения к существующему набору, варианта два:

- **Создать инстанс через public ctor в своём static-классе**, гарантировав, что static-инициализатор сработает до использования реестра — например, через `[RuntimeInitializeOnLoadMethod]` или явную ссылку из стартового кода:
  ```csharp
  public static class MyMoveStates
  {
      public static readonly MoveState Sprint = new MoveState("Sprint", 100);
      public static readonly MoveState Climb  = new MoveState("Climb",  101);
  }
  ```
  Без явного триггера static-init `Sprint`/`Climb` могут не появиться в `MoveState.GetAll<>()` до первого обращения к `MyMoveStates`.

- **Объявить собственный наследник** — он становится отдельным набором (`typeof(MyMoveState) != typeof(MoveState)`), это другой реестр в `ByKey`. Подходит, когда нужна изоляция, а не дополнение.

### Eager-инициализация (нет lazy)

`ExtensibleEnum.Initialize()` вызывается:
- в рантайме — через `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`;
- в Editor-домене — через `[InitializeOnLoadMethod]` под `#if UNITY_EDITOR`.

Что делает:
1. Сканирует `AppDomain.CurrentDomain.GetAssemblies()`.
2. Для каждого не-абстрактного `ExtensibleEnum`-наследника:
   - заносит `FullName → Type` в `ByFullName` (для `Deserialize`);
   - вызывает `RuntimeHelpers.RunClassConstructor(type.TypeHandle)`, что прогоняет static-инициализаторы наследника и наполняет `ByKey`/`Ordered` зарегистрированными инстансами.

После этого `GetAll<T>()`, `GetByKey<T>()` и `Deserialize` работают предсказуемо вне зависимости от того, в каком порядке загружаются типы и какой код первым к ним обратился. Lazy-кеши не используются.

### ExtEnumData\<T\>

```
ExtEnumData<T> : ReactiveValue<T> where T : ExtensibleEnum
  ├── Key: string                ← Value?.Key
  ├── Index: int                 ← Value?.Order ?? -1
  ├── Is(T other)                ← ReferenceEquals(Value, other)
  └── IsKey(string key)          ← сравнение по ключу
```

`Value` — singleton-инстанс типа `T`. Сравнение по ссылке корректно, потому что значения всегда одни и те же объекты (`MoveState.Run` — единственный экземпляр).

---

## Контракт сериализации

### Формат

```
"{Namespace.TypeName}.{Key}"
```

Примеры:
- `"MyGame.States.MoveState.Run"`
- `"Vortex.Demo.CombatState.Block"`

Разделитель — **последняя точка** строки. Левая часть — `Type.FullName` (включая namespace), правая — `Key`.

### Ограничения на имена

Чтобы разделитель работал однозначно:
- Имя типа-наследника **не должно содержать точек** (это требование C#, выполняется автоматически).
- `Key` (имена значений) **не должны содержать точек** — ответственность автора класса.

### Регистрация в SerializeController

Конвертер регистрируется один раз в static-ctor `ExtensibleEnum`:

```csharp
SerializeController.RegisterCustomSerializer(
    matches:     t => typeof(ExtensibleEnum).IsAssignableFrom(t),
    serialize:   obj => ((ExtensibleEnum)obj).Serialize(),
    deserialize: (t, s) => Deserialize(s)
);
```

После регистрации любое свойство типа `ExtensibleEnum`-наследника или `ExtEnumData<T>.Value` сериализуется как обычная JSON-строка, без раскрытия в объект.

---

## Использование

### Объявление набора

```csharp
public sealed class CombatState : ExtensibleEnum
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
    public ExtEnumData<MoveState>   MoveMode   { get; } = new(MoveState.Idle);
    public ExtEnumData<CombatState> CombatMode { get; } = new(CombatState.Idle);
}
```

### Чтение, установка, проверки

```csharp
character.MoveMode.Set(MoveState.Run);                      // type-safe ✓
if (character.MoveMode.Value == MoveState.Run) { ... }      // reference equality
if (character.MoveMode.Is(MoveState.Run)) { ... }           // явная проверка
character.MoveMode.OnUpdate += v => Refresh(v);             // реактивность
int slot = character.MoveMode.Index;                        // → UIStateSwitcher
string key = character.MoveMode.Key;                        // → save без обёртки ExtEnumData
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
var run = ExtensibleEnum.GetByKey<MoveState>("Run");          // → MoveState.Run
var any = ExtensibleEnum.GetByKey(typeof(MoveState), "Walk"); // → MoveState.Walk

foreach (var s in ExtensibleEnum.GetAll<MoveState>())
    Console.WriteLine($"{s.Order}: {s.Key}");
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `GetByKey<T>("…")` для незарегистрированного ключа | Возвращает `null` |
| `GetAll<T>()` сразу после старта | Возвращает все значения — eager-инициализация уже отработала |
| `Deserialize("…")` для несуществующего типа | Возвращает `null` (в `ByFullName` нет записи) |
| `Deserialize("…")` для существующего типа, но неизвестного ключа | Возвращает `null` |
| `Deserialize("BadString")` без точки | Возвращает `null` |
| `ExtEnumData<T>.Value == null` | `Key = null`, `Index = -1`, `Is(...) = false` |
| `ExtEnumData<T>.Set(null)` | Допустимо; всё реактивное поведение `ReactiveValue<T>` сохраняется |
| Дублирование Key в наследнике | Последний `new` перезатирает предыдущий в `ByKey` (некорректное использование автором класса) |
| Чистый .NET без Unity (тесты) | `[RuntimeInitializeOnLoadMethod]` не сработает; вызвать `RunClassConstructor` вручную или сослаться хотя бы на одно значение перед сериализацией |
| Параллельные потоки | Реестр не потокобезопасен; eager-init выполняется один раз до пользовательского кода, повторные ctor-вызовы не защищены |

---

## Контракт публичного API

```csharp
// Identity + сериализация
public abstract class ExtensibleEnum : IEquatable<ExtensibleEnum>
{
    public string Key { get; }
    public int Order { get; }
    
    protected ExtensibleEnum(string key, int order);
    
    public string Serialize();
    
    public static T               GetByKey<T>(string key) where T : ExtensibleEnum;
    public static ExtensibleEnum  GetByKey(Type type, string key);
    public static IReadOnlyList<T>               GetAll<T>() where T : ExtensibleEnum;
    public static IReadOnlyList<ExtensibleEnum>  GetAll(Type type);
    public static IReadOnlyDictionary<string, ExtensibleEnum> GetMap(Type type);
    
    public static ExtensibleEnum  Deserialize(string serialized);
    public static T               Deserialize<T>(string serialized) where T : ExtensibleEnum;
}

// Реактивная обёртка
public class ExtEnumData<T> : ReactiveValue<T> where T : ExtensibleEnum
{
    public ExtEnumData();
    public ExtEnumData(T initial);
    
    public string Key { get; }
    public int    Index { get; }
    public bool   Is(T other);
    public bool   IsKey(string key);
}
```

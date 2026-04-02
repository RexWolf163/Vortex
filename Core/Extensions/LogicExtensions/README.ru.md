# LogicExtensions (Core)

Набор утилитарных расширений общего назначения для работы с делегатами, коллекциями, строками, криптографией и сериализацией.

## Назначение

- Безопасный вызов делегатов (`Action`, `Func<bool>`) с null-проверкой и агрегацией результатов
- Шлюз инициализации (`InitValve`) — потокобезопасная отложенная подписка
- Безопасные операции над коллекциями (`Dictionary`, `List`) с логированием конфликтов
- Конвертация `DateTime` ↔ Unix-время (миллисекунды)
- SHA256-хеширование и генерация детерминированных GUID
- Сжатие строк через ZipArchive + Base64
- Копирование публичных свойств между объектами через рефлексию
- Property-based JSON-сериализация (экспериментальная)

Вне ответственности: валидация бизнес-логики, Unity-зависимые утилиты, потокобезопасность коллекций.

## Зависимости

- `Vortex.Core.LoggerSystem` — `Log`, `LogData`, `LogLevel` (логирование ошибок и предупреждений)

## Компоненты

```
LogicExtensions/
├── Actions/
│   ├── ActionExt.cs          → безопасный вызов делегатов
│   └── InitValve.cs          → шлюз инициализации
├── Crypto.cs                 → SHA256, генерация GUID
├── DateTimeExtConvert.cs     → Unix-время
├── DictionaryExtAdding.cs    → безопасные операции словаря
├── IndexFabric.cs            → фабрика case-insensitive словарей
├── ListExt.cs                → AddOnce, IndexOfItem
├── ObjectExtDeepClone.cs     → глубокое клонирование объектов
├── SimpleTypeMarker.cs       → partial-маркер платформенных примитивов для DeepCopy
├── StringExtCompress.cs      → сжатие строк
├── StringExtCommons.cs       → IsNullOrWhitespace
├── ObjectExtCopy.cs          → копирование свойств через рефлексию
└── SerializationSystem/
    └── SerializeController.cs → property-based JSON
```

---

## ActionExt

Расширения делегатов `Action` и `Func` для безопасного вызова без проверки на `null`.

### API

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `Fire` | `Action.Fire()` ... `Action<T1..T5>.Fire(args)` | Null-safe вызов делегата (0–5 параметров) |
| `FireAnd` | `Func<.., bool>.FireAnd(args, returnOnZero)` | AND-агрегация: `false` при первом `false`. Раннее прерывание (0–5 параметров) |
| `FireOr` | `Func<.., bool>.FireOr(args, returnOnZero)` | OR-агрегация: `true` если хотя бы один `true`. Все подписчики вызываются (0–5 параметров) |
| `Accumulate<T>` | `Func<T[]>.Accumulate()`, `Func<T>.Accumulate()` | Сбор результатов всех подписчиков в единый массив |
| `FirstNotNull<T>` | `Func<T>.FirstNotNull()`, `Func<T1,T2>.FirstNotNull(arg)` | Первый ненулевой результат среди подписчиков |
| `AddSafe` | `Action.AddSafe(value)` | Подписка с защитой от дублирования |

### Использование

```csharp
// Безопасный вызов
onComplete.Fire();
onDataChanged.Fire(newData);

// AND-агрегация условий (раннее прерывание)
bool canProceed = canActivate.FireAnd();

// OR-агрегация (все подписчики выполняются)
bool anyMatch = hasMatch.FireOr(inputData);

// Сбор данных от подписчиков
string[] allTags = getTags.Accumulate();

// Первый ненулевой ответ
var handler = findHandler.FirstNotNull(request);

// Подписка без дублей
onUpdate = onUpdate.AddSafe(MyHandler);
```

### Граничные случаи

- `FireAnd` / `FireOr` при отсутствии подписчиков возвращают `returnOnZero` (по умолчанию `true`)
- `FireAnd` прерывается при первом `false`; `FireOr` вызывает всех подписчиков
- Исключения в подписчиках перехватываются и логируются через `Log.Print`
- `AddSafe` логирует предупреждение при попытке повторной подписки

---

## InitValve

Потокобезопасный шлюз инициализации. Накапливает подписки до момента открытия; после открытия новые подписки вызываются немедленно.

### Жизненный цикл

```
Create(out openValve)  →  valve += handler  →  openValve()  →  valve += lateHandler (вызов немедленно)
                                                                  │
                                                             valve.Dispose()
```

### API

| Метод | Описание |
|-------|----------|
| `InitValve.Create(out Action openValve)` | Фабрика. Возвращает экземпляр и `Action` для открытия |
| `operator +` | Подписка. До открытия — накопление; после — немедленный вызов |
| `operator -` | Отписка (только до открытия) |
| `Dispose()` | Блокирует дальнейшие операции |

### Использование

```csharp
public class MySystem
{
    public InitValve OnReady { get; } = InitValve.Create(out var open);
    private Action _open = open;

    public void Initialize()
    {
        // ... инициализация ...
        _open(); // все накопленные подписки вызываются
    }
}

// Подписчик:
mySystem.OnReady += () => Debug.Log("System ready");
```

### Граничные случаи

- Повторное открытие — логирует предупреждение
- Операции после `Dispose()` — логируют ошибку, операция игнорируется
- Потокобезопасность обеспечивается через `lock`; вызов подписчика происходит вне блокировки

---

## Crypto

Хеширование, генерация идентификаторов, упаковка данных (сжатие + шифрование).

### API

| Метод | Описание |
|-------|----------|
| `GetHashSha256(string text)` | SHA256-хеш строки (hex, lowercase) |
| `GetNewGuid()` | Детерминированный GUID: `SHA256(timestamp + counter + random + random)` |
| `SetCryptoPack(string data, string pass)` | Сжатие (GZip) + шифрование (AES-256-CBC). Результат — Base64-строка |
| `GetCryptoPack(string cryptoData, string pass)` | Расшифровка + распаковка. Обратная операция к `SetCryptoPack` |

### Формат SetCryptoPack / GetCryptoPack

```
Base64( salt[16] | iv[16] | ciphertext )
```

- Ключ деривируется из `pass` через PBKDF2-SHA256 (100 000 итераций)
- Соль (16 байт) генерируется случайно при каждом вызове `SetCryptoPack`
- IV (16 байт) генерируется AES
- Неверный `pass` при расшифровке → `CryptographicException`

### Использование

```csharp
// Упаковка
string packed = Crypto.SetCryptoPack(jsonData, "my_secret");

// Распаковка
string original = Crypto.GetCryptoPack(packed, "my_secret");
```

### Граничные случаи

- `GetNewGuid()` использует счётчик для уникальности при вызовах в пределах одного тика
- Не криптографически стойкий GUID — `Random` вместо `RandomNumberGenerator`
- `SHA256Managed` — устаревший класс; работает, но не рекомендуется для новых проектов
- `SetCryptoPack` / `GetCryptoPack`: `null` или пустая строка `data` → возвращается без изменений
- `SetCryptoPack` / `GetCryptoPack`: `null` или пустой `pass` → `ArgumentNullException`
- `GetCryptoPack`: данные короче 33 байт (salt + iv + 1) → `ArgumentException`

---

## DateTimeExtConvert

Конвертация `DateTime` ↔ Unix-время в миллисекундах.

### API

| Метод | Описание |
|-------|----------|
| `date.ToUnixTime()` | `DateTime` → `long` (мс от epoch) |
| `date.FromUnixTime(ms)` | `long` → `DateTime` (UTC) |

### Граничные случаи

- `FromUnixTime` — extension-метод, но значение `date` игнорируется (создаётся новый epoch)
- Результат `FromUnixTime` всегда в `DateTimeKind.Utc`

---

## DictionaryExtAdding

Безопасные операции над `IDictionary` с логированием конфликтов.

### API

| Метод | Описание |
|-------|----------|
| `dict.AddNew(key, value)` | Добавление с проверкой на существование ключа. `Error` при конфликте значений, `Warning` при дублировании |
| `dict.Get(key)` | Получение с `Warning` при отсутствии ключа. Возвращает `default` |

---

## IndexFabric

Фабрика для создания `Dictionary<string, T>` с `StringComparer.InvariantCultureIgnoreCase`.

| Метод | Описание |
|-------|----------|
| `IndexFabric.Create<T>()` | Пустой case-insensitive словарь |
| `IndexFabric.Create<T>(source)` | Case-insensitive словарь, заполненный из `IDictionary<string, T>` |

---

## ListExt

| Метод | Описание |
|-------|----------|
| `list.AddOnce(data)` | Добавить элемент, если его нет в списке (`Contains`-проверка) |
| `list.IndexOfItem(value)` | Индекс элемента в `IReadOnlyList<T>` (возвращает `-1` если не найден) |

---

## StringExtCompress

Сжатие и восстановление строк через `ZipArchive` + Base64.

### API

| Метод | Описание |
|-------|----------|
| `str.Compress(key)` | Строка → ZipArchive → Base64. `key` — имя entry в архиве (по умолчанию `"data"`) |
| `str.Decompress(key)` | Base64 → ZipArchive → строка. Поиск entry по суффиксу `key` |

### Граничные случаи

- `Decompress` возвращает `null`, если entry не найден или архив пуст
- `key` при декомпрессии ищется через `EndsWith`, а не точное совпадение

---

## StringExtCommons

| Метод | Описание |
|-------|----------|
| `str.IsNullOrWhitespace()` | `string.IsNullOrEmpty(str?.Trim())` |

---

## ObjectExtDeepClone

Глубокое клонирование объектов через рефлексию с обнаружением циклических ссылок.

### API

| Метод | Описание |
|-------|----------|
| `source.DeepCopy<T>(returnOriginalOnError)` | Глубокая копия объекта. При `returnOriginalOnError = true` — возвращает оригинал вместо `null` при ошибке |

### Порядок обработки типов

| Приоритет | Тип | Поведение |
|-----------|-----|-----------|
| 1 | `null` | `default(T)` |
| 2 | Примитивы, `string`, `decimal`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, `Uri`, `Version`, `enum` | Возврат as-is |
| 3 | Платформенные примитивы (`SimpleTypeMarker`) | Возврат по ссылке (не клонируются) |
| 4 | Объект уже в `visited` | Возврат ранее созданной копии (защита от циклов) |
| 5 | `Array` | Поэлементное рекурсивное копирование |
| 6 | `IDictionary` | Рекурсивное копирование значений; ключи передаются as-is |
| 7 | `IList` | Рекурсивное копирование элементов |
| 8 | `ICloneable` | Вызов `Clone()`. **Контракт: реализация должна выполнять deep copy** |
| 9 | Прочие объекты | `Activator.CreateInstance` (fallback: `FormatterServices.GetUninitializedObject`) + копирование всех полей (включая private и наследованные) |

### SimpleTypeMarker — платформенные примитивы

`SimpleTypeMarker` — partial-класс. В Core пуст. Unity-partial добавляет типы (`Sprite`, `GameObject`), которые не должны клонироваться — передаются по ссылке. Проверка через `IsAssignableFrom` — наследники маркерных типов тоже считаются платформенными примитивами. Типы кешируются при первом вызове через рефлексию по статическим полям класса.

### Граничные случаи

- Циклические ссылки обнаруживаются через `ReferenceEqualityComparer` — повторный объект возвращает уже созданную копию
- `ICloneable`: если `Clone()` делает shallow copy — вложенные ссылки будут разделяться с оригиналом
- `returnOriginalOnError = true` подмешивает оригинал в граф копии — мутации оригинала будут видны через "копию"
- Создание экземпляра: `Activator.CreateInstance`, при неудаче — fallback на `FormatterServices.GetUninitializedObject` (без вызова конструктора); если оба провалились — ошибка или возврат оригинала
- Ключи словарей передаются as-is (не клонируются) — защита контракта `GetHashCode`/`Equals`
- `readonly` поля копируются через `SetValue` (рефлексия обходит readonly)
- Кеш `FieldInfo[]` и платформенных типов — статический, не очищается
- `null` на входе → `default(T)`

---

## ObjectExtCopy

Копирование публичных свойств между объектами через рефлексию.

### API

| Метод | Описание |
|-------|----------|
| `target.CopyFrom(source)` | Копирует значения свойств из `source` в `target` по совпадению имён. Только `public` readable свойства `source` → `CanWrite` свойства `target` |

### Граничные случаи

- Типы `source` и `target` могут не совпадать — копируются только свойства с совпадающими именами
- Свойства без сеттера в `target` пропускаются
- Исключения перехватываются; при ошибке возвращает `false`

---

## SerializeController (экспериментальный)

Property-based JSON-сериализатор. Обрабатывает публичные свойства с `public` геттером и любым сеттером.

### API

| Метод | Описание |
|-------|----------|
| `model.SerializeProperties()` | Объект → JSON-строка с маркерами типов (`"__": "AssemblyQualifiedName"`) |
| `data.DeserializeProperties<T>()` | JSON-строка → объект типа `T` |

### Поддерживаемые типы

- Примитивы (`int`, `long`, `float`, `double`, `decimal`, `bool`, `byte`, `short` и unsigned-варианты)
- `string`, `char`, `DateTime`, `Guid`, `enum`, `Nullable<T>`
- Коллекции: `IList`, массивы
- Словари: `IDictionary` с простыми ключами (`string`, `int`, `Type`)
- Вложенные объекты (рекурсивно)

### Ограничения

- Поля (`field`) игнорируются — только свойства
- Циклические ссылки вызывают ошибку (защита через `HashSet<object>`)
- Ключи словарей — только простые типы или `Type`
- `VisitedObjects` — статический, не потокобезопасный
- Статус: экспериментальный

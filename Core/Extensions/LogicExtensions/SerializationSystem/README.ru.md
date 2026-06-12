# SerializeController

**Namespace:** `Vortex.Core.Extensions.LogicExtensions.SerializationSystem`
**Сборка:** `ru.vortex.extensions`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Сериализация и десериализация объектов в JSON-строку без внешних библиотек.

Возможности:

- Преобразование объекта в форматированную JSON-строку и обратно
- Работа по свойствам с getter + setter: public-getter по умолчанию, непубличные — при маркировке `[IsPOCO]`
- Контроль сериализуемости через атрибут `[POCO]` на типах
- Исключение отдельных свойств через `[NotPOCO]`
- Custom-конвертеры для типов вне стандартных правил (`RegisterCustomSerializer`)
- Поддержка вложенных объектов, коллекций, словарей
- Типобезопасная десериализация с валидацией через `IsAssignableFrom`
- Защита от циклических ссылок
- Поддержка `Nullable<T>`, `enum`, `DateTime`, `Guid`

Вне ответственности:

- Сериализация полей (только свойства)
- Словари со сложными ключами (только простые типы и `Type`)
- Потокобезопасность
- Сериализация графов с множественными ссылками на один объект

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.LoggerSystem` | Логирование ошибок через `Log.Print()` |
| `System.Reflection` | Обнаружение свойств и атрибутов |
| `System.Collections` | `IDictionary`, `IList` |
| `System.Globalization` | `CultureInfo.InvariantCulture` для чисел и `DateTime` |

---

## Архитектура

Partial-класс `SerializeController` разбит на 5 файлов:

| Файл | Назначение |
|------|-----------|
| `SerializeController.cs` | Общая инфраструктура: кеши, POCO-валидация, парсинг JSON, простые типы |
| `SerializeControllerExtSerialization.cs` | Сериализация объектов в JSON-строку |
| `SerializeControllerExtDeserialization.cs` | Десериализация JSON-строки в новые объекты |
| `SerializeControllerExtUploading.cs` | Загрузка данных из JSON в существующие объекты |
| `SerializeControllerExtRegistration.cs` | Регистрация custom-конвертеров (`RegisterCustomSerializer`) |

Дополнительно — три атрибута в `POCOAttribute.cs`.

### Атрибуты

| Атрибут | Цель | Применение |
|---------|------|-----------|
| `[POCO]` | Маркер сериализуемого типа | Класс, структура, интерфейс |
| `[NotPOCO]` | Исключение свойства из сериализации | Свойство |
| `[IsPOCO]` | Принудительное включение непубличного свойства (internal/protected/private) в сериализацию | Свойство |

### Правила фильтрации типов

| Категория | Правило |
|-----------|---------|
| Простые типы (примитивы, string, enum, DateTime, Guid) | Сериализуются всегда |
| Коллекции (`IList`, массивы) | Сериализуются если тип элемента сериализуем |
| Словари (`IDictionary`) | Ключ — простой тип или `Type`; значение сериализуемо |
| Сложные типы | Только если тип помечен `[POCO]` |
| Свойства с `[NotPOCO]` | Пропускаются всегда |

`[POCO]` на интерфейсе распространяется на все его реализации.

### Правила отбора свойств

Свойство попадает в сериализацию, если:

- есть getter **и** setter (видимость setter'а не важна — рефлексия вызовет любой);
- тип свойства сериализуем (см. правила фильтрации типов);
- отсутствует атрибут `[NotPOCO]`;
- getter публичный **ИЛИ** свойство помечено `[IsPOCO]`.

Непубличные свойства без `[IsPOCO]` в сериализацию не попадают — это позволяет держать
internal/private state класса (кеши, шедоу-стейты) изолированным от сейва, оставляя
наружу IReadOnly-фасады.

### Custom-конвертеры

Для типов, не подпадающих под `IsSimpleType` и атрибут `[POCO]`, регистрируются
custom-конвертеры через `RegisterCustomSerializer`. Зарегистрированное семейство типов
ведёт себя как simple-тип: сериализуется в JSON-строку и десериализуется обратно через
предоставленные функции.

```csharp
SerializeController.RegisterCustomSerializer(
    matches:     type => typeof(ExtensibleEnum).IsAssignableFrom(type),
    serialize:   obj => ((ExtensibleEnum)obj).Key,
    deserialize: (type, str) => ExtensibleEnum.Deserialize(type, str));
```

Применение — типы, чьи singleton-инстансы нельзя пересоздавать через
`Activator.CreateInstance` (например, type-safe enum-подобные классы), но которые имеют
стабильное строковое представление. Регистрация идемпотентна по комбинации трёх
делегатов (повторный вызов с теми же ссылками не дублирует запись), поэтому безопасна
из static-инициализатора при домен-релоадах.

### Процесс сериализации

```
SerializeProperties(object)
  SerializeClass(object, depth)
    IsSimpleType?   -> GetSimple()
    Cyclic?         -> Error, ""
    IDictionary?    -> SerializeDictionary()
    IList/Array?    -> SerializeArray()
    IsPOCO(type)?   -> GetReadablePropertiesList() -> рекурсия по свойствам
    !IsPOCO(type)?  -> Warning, "null"
```

### Процесс десериализации

```
DeserializeProperties<T>(string)
  DeserializeClass(Type, string)
    IsSimpleType?  -> SetSimple()
    "null"?        -> null
    IDictionary?   -> DeserializeDictionary()
    Array?         -> DeserializeArray()
    IList?         -> DeserializeCollection()
    Object         -> SeparateText() -> читает "__" маркер
                      Type.GetType() -> IsPOCO? -> Activator.CreateInstance()
                      fallback -> FormatterServices.GetUninitializedObject()
                      -> рекурсия по свойствам (пропуск [NotPOCO])
```

### Процесс загрузки (Upload)

В отличие от десериализации, не создаёт новые экземпляры, а обновляет существующие.
Ключи словарей, которых нет в данных, сохраняются (мерж).

```
UploadProperties<T>(string, T)
  IDictionary?    -> UploadDictionary()  (мерж: существующие обновляются, новые добавляются)
  Array?          -> UploadArray()       (пересоздаёт массив)
  IList?          -> UploadCollection()  (clear + заполнение)
  Object          -> UploadClass()       (обновляет свойства target)
    пустые данные -> ClearClass()        (сброс свойств в default/null)
    свойство null -> fallback на DeserializeClass() (создание нового)
    свойство есть -> рекурсивный Upload в существующий объект
```

#### Семантика контейнеров при Upload

Поведение принципиально разное по типу коллекции — это важно при выборе структуры
сохраняемой модели:

| Контейнер | Семантика | Поведение для ключей/элементов, отсутствующих в сейве |
|-----------|-----------|------------------------------------------------------|
| `IDictionary<K,V>` | **Merge** | Сохраняются. Значения по существующим ключам обновляются через рекурсивный `UploadClass`, подписки на event'ы значения **сохраняются**. |
| `T[]` (массив) | **Replace** | Массив пересоздаётся строго размера из сейва, каждый элемент инстанцируется заново через `Activator.CreateInstance`. Существующая структура и все подписки на её элементы **теряются**. |
| `IList<T>` (List) | **Replace** | `Clear()` + заполнение из сейва. Элементы инстанцируются заново. |
| Object (POCO) | **Merge** | Свойства обновляются поверх существующего инстанса. Подписки на сам инстанс **сохраняются**. |

#### Implications для проектирования сохраняемой модели

«Эталонная структура» модели создаётся при `ComplexModel<T>.Init()` через
`Activator.CreateInstance(t)` для каждого зарегистрированного типа. Десериализация затем
накладывает значения из сейва поверх этой структуры. Если модель содержит коллекции,
размер/состав которых меняется между версиями конфига (например, добавился новый параметр
в `ParametersMap` после релиза), эволюция корректно работает **только для Dictionary** —
массивы и списки полностью перетираются сейвом и теряют новые элементы свежей структуры.

Правила выбора контейнера:

- **Структурная коллекция с устойчивыми ключами** (параметры из карты, реестр квестов,
  навыки персонажа, словарь характеристик) → `Dictionary<K, V>`, где `K` — стабильный
  идентификатор (`string`, `enum`, `Guid`). Тогда новые ключи из обновлённой карты
  автоматически появляются в загруженной модели с дефолтами, а старые удалённые из
  карты — отбрасываются на этапе мерджа (если ключа нет в карте — он просто не дополнит
  существующий target).
- **Списочные данные без идентичности** (история событий, лог попыток, лидерборд)
  → `T[]` или `List<T>`. Replace-семантика тут корректна — сейв полностью описывает
  актуальный список.
- **Фиксированная структура с именованными полями** → отдельные `[POCO]`-свойства,
  не массив/список. Каждое поле эволюционирует независимо: появилось новое
  свойство — берётся дефолт из конструктора и не затирается отсутствующим в сейве
  JSON-ключом.

#### Подписки на события и Upload

Merge-семантика Dictionary и Object сохраняет существующие инстансы — поэтому подписки
(`event` / `Action`), сделанные на этих инстансах **до** Upload, продолжают работать
после загрузки сейва. Replace-семантика массива/списка инстансы пересоздаёт — все
прежние подписки нужно либо восстанавливать вручную после Upload, либо избегать
структурных коллекций как мест хранения объектов с подписками.

#### Когда сейв содержит лишние/устаревшие данные

- **Dictionary:** существующий target-словарь пополняется новыми ключами из сейва,
  обновляются совпадающие. Ключи, которые **есть в target, но отсутствуют в сейве**
  — остаются (свежие из конфига). Ключи, которые **есть в сейве, но не в target** —
  добавляются (могут быть устаревшими, контроллер должен сам их отфильтровать при
  необходимости).
- **Object:** свойства из сейва, которых нет в текущем типе — пропускаются с warning
  (DebugMode), не ломают загрузку. Свойства типа, которых нет в сейве — сохраняют
  значение из эталонной структуры (дефолт конструктора).
- **Array/List:** содержимое строго из сейва. Текущая «эталонная» длина и элементы
  не влияют на результат.

### Формат JSON

Каждый сложный объект содержит маркер типа `"__"` с `AssemblyQualifiedName`:

```json
{
  "__" : "MyApp.Player, MyAssembly, Version=1.0.0.0, ...",
  "Name" : "Player1",
  "Level" : 5,
  "Items" : [
    {
      "__" : "MyApp.Item, MyAssembly, ...",
      "Id" : "sword_01",
      "Count" : 1
    }
  ]
}
```

| Тип | Формат в JSON |
|-----|--------------|
| `string` | `"text"` (с экранированием `\"`, `\\`) |
| `int`, `long` и др. целые | `42` |
| `float`, `double`, `decimal` | `3.14` (InvariantCulture) |
| `bool` | `true` / `false` |
| `enum` | `"Active"` (строковое имя) |
| `DateTime` | `"yyyy-MM-dd HH:mm:ss"` (InvariantCulture, без timezone) |
| `Guid` | `"00000000-0000-0000-0000-000000000000"` |
| `null` | `null` |
| `Type` (ключ словаря) | `"AssemblyQualifiedName"` |

### Кеширование

| Кеш | Содержимое |
|-----|-----------|
| `CacheFields` | `Dictionary<Type, PropertyInfo[]>` — свойства типа (с учётом `[NotPOCO]` и `IsSerializableType`) |
| `CachePOCO` | `Dictionary<Type, bool>` — результат проверки `[POCO]` на типе и его интерфейсах |

Оба кеша заполняются при первом обращении и не очищаются. Реестр посещённых объектов `VisitedObjects` (защита от циклов) — напротив, очищается в начале каждого `SerializeProperties` и в `finally` по завершении.

### Интеграция с фреймворком

**ComplexModel<T>** — использует `SerializeProperties()` / `DeserializeProperties<T>()` для персистенции составных моделей (`Dictionary<Type, T>`). Интерфейс `T` может быть помечен `[POCO]`, тогда все его реализации автоматически сериализуемы.

**RecordScriptGenerator** — генерирует код сохранения Record-подклассов:

```csharp
public override string GetDataForSave() => this.SerializeProperties();
public override void LoadFromSaveData(string data)
{
    var temp = data.DeserializeProperties<MyRecord>();
    this.CopyFrom(temp);
}
```

---

## Контракт

### Вход

Объект, тип которого помечен `[POCO]` (или реализует интерфейс с `[POCO]`), со свойствами, у которых есть getter + setter: public-getter по умолчанию, непубличные — при маркировке `[IsPOCO]`.

### Публичный API

| Метод | Назначение |
|-------|-----------|
| `string SerializeProperties(this object)` | Сериализация объекта в JSON-строку |
| `T DeserializeProperties<T>(this string)` | Десериализация JSON-строки в новый экземпляр |
| `void UploadProperties<T>(this string data, T target)` | Загрузка данных JSON в существующий объект `target` (мерж, без пересоздания) |
| `void RegisterCustomSerializer(matches, serialize, deserialize)` | Регистрация custom-конвертера для семейства типов |

### Выход

- Сериализация: форматированная JSON-строка с маркерами типов
- Десериализация: типизированный экземпляр объекта или `default(T)` при ошибке
- Upload: данные накладываются на переданный `target` (возврат `void`)

### Поддерживаемые типы свойств

| Категория | Типы |
|-----------|------|
| Числа | `int`, `long`, `short`, `byte`, `sbyte`, `uint`, `ulong`, `ushort`, `float`, `double`, `decimal` |
| Текст | `string`, `char` |
| Логические | `bool` |
| Дата/Идентификаторы | `DateTime`, `Guid` |
| Перечисления | Любой `enum` |
| Nullable | `Nullable<T>` для всех простых типов |
| Коллекции | `T[]`, `List<T>`, любой `IList` (если элемент сериализуем) |
| Словари | `Dictionary<K,V>` — ключ: простой тип или `Type`; значение: сериализуемый тип |
| Вложенные объекты | Рекурсивно — только типы с `[POCO]` |

### Гарантии

- Типы без `[POCO]` не сериализуются (warning в лог, требует включённого DebugMode)
- Свойства с `[NotPOCO]` игнорируются в обоих направлениях
- Несериализуемые типы свойств отсекаются на этапе сбора `PropertyInfo[]`
- Десериализация проверяет совместимость типов через `IsAssignableFrom`
- Циклические ссылки обнаруживаются через `HashSet<object>`
- Числа с плавающей точкой через `InvariantCulture`
- Неизвестные свойства в JSON при десериализации пропускаются с warning (не ломают загрузку)
- Warning-сообщения выводятся только при включённом DebugMode (`SettingsModelExtDebug`)

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Только свойства, не поля | Контроль сериализуемой поверхности, фильтрация системных полей |
| Сложные типы требуют `[POCO]` | Защита от затягивания UnityEngine.Object и прочих непредназначенных типов |
| Нет циклических ссылок | Обнаружение через `HashSet`; цикл — ошибка |
| Ключи словарей — простые типы или `Type` | Сложные ключи не поддерживаются |
| `Type.GetType()` при десериализации | Тип должен быть доступен в текущем AppDomain |
| `DateTime` без timezone | Фиксированный формат `yyyy-MM-dd HH:mm:ss` |
| `VisitedObjects` — static | Не потокобезопасен |

---

## Использование

### Разметка типов

```csharp
[POCO]
public class PlayerData
{
    public string Name { get; set; }
    public int Level { get; set; }
    public float Score { get; set; }
}
```

### Исключение свойств

```csharp
[POCO]
public class QuestModel : Record
{
    public QuestState State { get; internal set; }
    public byte Step { get; internal set; }

    // Не сериализуется — неизменяемые данные из пресета
    [NotPOCO] public bool Autorun { get; private set; }
    [NotPOCO] public bool UnFailable { get; internal set; }

    // Не сериализуется — QuestLogic не помечен [POCO]
    public QuestLogic[] Logics { get; private set; }
}
```

### Разметка через интерфейс

```csharp
[POCO]
public interface IGameData { }

// Автоматически сериализуем — реализует интерфейс с [POCO]
public class InventoryData : IGameData
{
    public int Gold { get; set; }
    public int Gems { get; set; }
}
```

### Сериализация

```csharp
var player = new PlayerData { Name = "Test", Level = 42, Score = 3.14f };
string json = player.SerializeProperties();
```

### Десериализация

```csharp
PlayerData restored = json.DeserializeProperties<PlayerData>();
```

### Полиморфизм

Десериализация восстанавливает конкретный тип из маркера `"__"`. Целевой тип `T` может быть базовым классом или интерфейсом:

```csharp
IGameData data = json.DeserializeProperties<IGameData>();
// data будет конкретного типа, записанного в "__"
```

### Интеграция с SaveSystem через Record

```csharp
[POCO]
public class PlayerRecord : Record
{
    public string Name { get; set; }
    public int Level { get; private set; }

    // Ссылка на ScriptableObject — не затянется (тип без [POCO])
    public WeaponConfig Weapon { get; set; }

    public override string GetDataForSave() => this.SerializeProperties();
    public override void LoadFromSaveData(string data)
        => this.CopyFrom(data.DeserializeProperties<PlayerRecord>());
}
```

### Интеграция с ComplexModel

```csharp
[POCO]
public interface IGameData { }

public class GameModel : ComplexModel<IGameData>
{
    // Все реализации IGameData автоматически сериализуемы
}
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `null` на входе `SerializeProperties` | `string.Empty` |
| Тип без `[POCO]` | `LogLevel.Warning`, `"null"` (сериализация) / `null` (десериализация) |
| Свойство с `[NotPOCO]` | Пропускается в обоих направлениях |
| Свойство с несериализуемым типом | Отфильтровано из `PropertyInfo[]`, не попадает в JSON |
| Циклическая ссылка (A.B = obj, A.C = obj) | `LogLevel.Error`, `""` |
| Несовместимый тип при десериализации | `LogLevel.Error`, `default(T)` |
| Отсутствует маркер `"__"` в JSON | `LogLevel.Error`, `null` |
| Свойство без setter | Пропускается при сборе свойств |
| Свойство есть в JSON, но удалено из типа | `LogLevel.Warning`, пропуск (не ломает загрузку) |
| Свойство есть в JSON, но без setter | `LogLevel.Warning`, пропуск |
| Пустая строка при десериализации | `null` |
| `null`-элемент в коллекции | Пропускается при десериализации |
| `[POCO]` на интерфейсе | Все реализации считаются сериализуемыми |
| Тип без конструктора по умолчанию | Fallback на `FormatterServices.GetUninitializedObject()` — поля не инициализируются конструктором |

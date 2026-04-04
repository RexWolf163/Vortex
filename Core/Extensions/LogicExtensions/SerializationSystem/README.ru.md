# SerializeController

**Namespace:** `Vortex.Core.Extensions.LogicExtensions.SerializationSystem`
**Сборка:** `ru.vortex.extensions`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Сериализация и десериализация объектов в JSON-строку без внешних библиотек.

Возможности:

- Преобразование объекта в форматированную JSON-строку и обратно
- Работа по публичным свойствам (getter + любой setter)
- Контроль сериализуемости через атрибут `[POCO]` на типах
- Исключение отдельных свойств через `[NotPOCO]`
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

Статический класс с двумя extension-методами и два атрибута для управления сериализацией.

### Атрибуты

| Атрибут | Цель | Применение |
|---------|------|-----------|
| `[POCO]` | Маркер сериализуемого типа | Класс, структура, интерфейс |
| `[NotPOCO]` | Исключение свойства из сериализации | Свойство |

### Правила фильтрации типов

| Категория | Правило |
|-----------|---------|
| Простые типы (примитивы, string, enum, DateTime, Guid) | Сериализуются всегда |
| Коллекции (`IList`, массивы) | Сериализуются если тип элемента сериализуем |
| Словари (`IDictionary`) | Ключ — простой тип или `Type`; значение сериализуемо |
| Сложные типы | Только если тип помечен `[POCO]` |
| Свойства с `[NotPOCO]` | Пропускаются всегда |

`[POCO]` на интерфейсе распространяется на все его реализации.

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
                      -> рекурсия по свойствам (пропуск [NotPOCO])
```

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

Оба кеша заполняются при первом обращении и не очищаются.

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

Объект, тип которого помечен `[POCO]` (или реализует интерфейс с `[POCO]`), с публичными свойствами (getter + любой setter).

### Выход

- Сериализация: форматированная JSON-строка с маркерами типов
- Десериализация: типизированный экземпляр объекта или `default(T)` при ошибке

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

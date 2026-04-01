# SerializeController

**Namespace:** `Vortex.Core.Extensions.LogicExtensions.SerializationSystem`
**Сборка:** `ru.vortex.extensions`
**Платформа:** .NET Standard 2.1+
**Статус:** экспериментальный

---

## Назначение

Сериализация и десериализация объектов в JSON-строку без внешних библиотек.

Возможности:

- Преобразование объекта в форматированную JSON-строку и обратно
- Работа по публичным свойствам (getter + любой setter)
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
| `System.Reflection` | Обнаружение свойств |
| `System.Collections` | `IDictionary`, `IList` |
| `System.Globalization` | `CultureInfo.InvariantCulture` для чисел и `DateTime` |

---

## Архитектура

Статический класс с двумя extension-методами. Единственный файл, нет интерфейсов или наследования.

### Процесс сериализации

```
SerializeProperties(object)
  SerializeClass(object, depth)
    IsSimpleType? -> GetSimple()
    Cyclic?       -> Error, ""
    IDictionary?  -> SerializeDictionary()
    IList/Array?  -> SerializeArray()
    Object        -> GetReadablePropertiesList() -> рекурсия по свойствам
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
                      Type.GetType() -> Activator.CreateInstance()
                      -> рекурсия по свойствам
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

`Dictionary<Type, PropertyInfo[]>` — кеш свойств по типу. Заполняется при первом обращении, не очищается.

### Интеграция с фреймворком

**ComplexModel<T>** — использует `SerializeProperties()` / `DeserializeProperties<T>()` для персистенции составных моделей (`Dictionary<Type, T>`).

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

Любой объект с публичными свойствами, имеющими getter и любой setter (`public`, `protected`, `private`).

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
| Коллекции | `T[]`, `List<T>`, любой `IList` |
| Словари | `Dictionary<K,V>` — ключ: простой тип или `Type` |
| Вложенные объекты | Рекурсивно по свойствам |

### Гарантии

- Десериализация проверяет совместимость типов через `IsAssignableFrom`
- Циклические ссылки обнаруживаются через `HashSet<object>` и прерывают сериализацию
- Числа с плавающей точкой сериализуются/десериализуются через `InvariantCulture`

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Только свойства, не поля | Разработчик контролирует сохраняемую поверхность |
| Нет циклических ссылок | Обнаружение через `HashSet`; цикл — ошибка |
| Ключи словарей — простые типы или `Type` | Сложные ключи не поддерживаются |
| `Type.GetType()` при десериализации | Тип должен быть доступен в текущем AppDomain |
| `DateTime` без timezone | Фиксированный формат `yyyy-MM-dd HH:mm:ss` |
| `VisitedObjects` — static | Не потокобезопасен |
| Кеш `PropertyInfo[]` не очищается | Потенциальная утечка при большом количестве типов |

---

## Использование

### Сериализация

```csharp
var player = new Player { Name = "Test", Level = 42, Score = 3.14f };
string json = player.SerializeProperties();
```

### Десериализация

```csharp
Player restored = json.DeserializeProperties<Player>();
```

### Полиморфизм

Десериализация восстанавливает конкретный тип из маркера `"__"`. Целевой тип `T` может быть базовым классом или интерфейсом:

```csharp
IUnit unit = json.DeserializeProperties<IUnit>();
// unit будет конкретного типа, записанного в "__"
```

### Интеграция с SaveSystem через Record

Свойства Record, подлежащие сохранению, объявляются как публичные property с setter:

```csharp
public class PlayerRecord : Record
{
    public string Name { get; set; }
    public int Level { get; private set; }  // private set — сериализуется
    public float Score { get; set; }
}
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `null` на входе `SerializeProperties` | `string.Empty` |
| Циклическая ссылка (A.B = obj, A.C = obj) | `LogLevel.Error`, `""` |
| Несовместимый тип при десериализации | `LogLevel.Error`, `default(T)` |
| Отсутствует маркер `"__"` в JSON | `LogLevel.Error`, `null` |
| Свойство без setter | Пропускается |
| Свойство есть в JSON, но удалено из типа | `LogLevel.Error`, `null` |
| Пустая строка при десериализации | `null` |
| `null`-элемент в коллекции | Пропускается при десериализации (`continue`) |

# PoolSystem

**Namespace:** `Vortex.Unity.UI.PoolSystem`
**Сборка:** `ru.vortex.unity.ui.misc`

## Назначение

Пул объектов с data-ключами и переиспользованием. `Pool` управляет экземплярами `PoolItem`, обеспечивая создание, переиспользование и деактивацию элементов.

Возможности:
- Создание/переиспользование элементов по data-ключу
- Деактивация с возвратом в очередь свободных
- Типизированный доступ к данным через `IDataStorage`
- Событие `OnUpdateLink` при смене данных

Вне ответственности:
- Логика отображения элементов (реализуется в наследниках `PoolItem`)
- Предзагрузка пула

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `IDataStorage` — интерфейс хранилища данных |
| Odin Inspector | `[ShowInInspector]` — отображение данных в Editor |

---

## Архитектура

```
PoolSystem/
├── Pool.cs         # Менеджер пула (MonoBehaviour)
└── PoolItem.cs     # Контейнер элемента (MonoBehaviour, IDataStorage)
```

### Pool

MonoBehaviour-менеджер. Хранит:
- `PoolItem preset` — шаблон для инстанцирования
- `Dictionary<object, PoolItem> _index` — активные элементы по data-ключу
- `Queue<PoolItem> _freeItems` — очередь свободных элементов

При `Awake` собирает существующие дочерние `PoolItem` в очередь свободных.

### PoolItem

MonoBehaviour-контейнер, связывающий данные с визуалом. Реализует `IDataStorage`. Хранит `List<object> _data` с FIFO-поиском по типу.

---

## API

### Pool

```csharp
pool.AddItem(model, intData);               // params object[] — множественные данные
pool.AddItem(data);                          // создать/переиспользовать
pool.AddItemBefore(data);                    // вставить в начало (SetAsFirstSibling)
var item = pool.AddItem<MyView>(data);       // с приведением к MonoBehaviour

var existing = pool.GetItem<MyView>(data);   // получить по ключу (или null)

pool.RemoveItem(data);                       // деактивировать, вернуть в очередь
pool.RemoveByCallback(key => key is MyType); // удалить все элементы по условию
pool.Clear();                                // деактивировать все, вернуть в очередь
```

При передаче `params object[]` массив используется как ключ в `_index`, а `PoolItem.MakeLink` распаковывает его: каждый элемент массива добавляется в `_data` отдельно, обеспечивая `GetData<T>()` по любому из переданных типов.

### PoolItem

```csharp
var model = poolItem.GetData<MyModel>();     // FIFO-поиск по типу
poolItem.OnUpdateLink += OnDataChanged;      // событие обновления данных
```

---

## Контракт

### Вход
- `PoolItem preset` — шаблон в Inspector
- `object data` — ключ и данные элемента

### Выход
- Активный `PoolItem` с привязанными данными
- `OnUpdateLink` — событие при смене данных

### Гарантии
- `AddItem` с существующим ключом возвращает существующий элемент
- `RemoveItem` возвращает элемент в очередь, не уничтожает
- `RemoveByCallback` итерирует по снимку ключей — безопасен при модификации `_index` во время обхода
- `Clear` деактивирует все элементы и возвращает в очередь свободных

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `AddItem()` с существующим ключом | Возврат существующего элемента |
| `RemoveItem()` с несуществующим ключом | Тихий пропуск |
| `GetItem()` с несуществующим ключом | Возвращает `null` |
| Очередь свободных пуста | Инстанцирование нового из `preset` |
| `Clear()` | Все элементы деактивируются и возвращаются в очередь свободных, индекс очищается |
| `RemoveByCallback()` — callback не совпал ни с одним ключом | Ничего не удаляется |
| `PoolItem.GetData<T>()` — тип не найден | Возвращает `null` |

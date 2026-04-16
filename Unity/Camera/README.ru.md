# Camera System

Пакет управления ортографическими камерами: позиционирование, фокус на объектах, ограничение перемещения границами.

Assembly: `ru.vortex.unity.camera`  
Namespace: `Vortex.Unity.Camera`  
Слой: 2 (Unity)

## Архитектура

```
CameraBus (шина)
├── Controllers (static, extension methods)
│   ├── CameraMoveController   — покадровое обновление позиции и цели
│   ├── CameraFocusController  — управление группами фокуса
│   └── CameraBordersController — управление границами перемещения
├── Model
│   ├── CameraModel            — реактивная модель данных камеры
│   └── CameraFocusTarget      — группа объектов фокуса
└── View
    ├── CameraDataStorage      — MonoBehaviour-хранилище модели (IDataStorage)
    └── Handlers
        ├── CameraHandler      — базовый класс хендлеров камеры
        ├── CameraMoveHandler  — плавное приведение камеры к цели
        ├── FocusHandler       — декларативная привязка фокуса
        └── BordersHandler     — декларативная привязка границ
```

### Поток данных

1. `CameraDataStorage` регистрируется в `CameraBus` при `Awake`
2. `CameraMoveController` подписывается на регистрацию, захватывает ownership над `Position`/`Target`
3. Каждый `FixedUpdate` контроллер обновляет позицию и цель по данным фокуса
4. `CameraMoveHandler` (View) получает `OnUpdateData`, вычисляет итоговую позицию с учётом границ и плавно перемещает камеру

## CameraBus

Статическая шина регистрации камер. Ключ — `gameObject.name`.

```csharp
// Регистрация (автоматически в CameraDataStorage.Awake)
CameraBus.Registration(storage);

// Получение
var cam = CameraBus.Get("Map Camera");        // с LogError при отсутствии
if (CameraBus.TryGet("Map Camera", out var cam)) { }  // без ошибки
var any = CameraBus.GetAny();                 // первая доступная (null если пусто)
```

События:
- `OnRegistration` — камера зарегистрирована
- `OnRemove` — камера снята с регистрации

## CameraModel

Реактивная модель данных камеры. Реализует `IReactiveData`.

| Свойство | Тип | Описание |
|----------|-----|----------|
| `CameraRect` | `Vector2Data` | Размер видимой области в мировых единицах (width, height) |
| `Position` | `Vector2Data` | Текущая позиция камеры |
| `Target` | `Vector2Data` | Целевая позиция (центр группы фокуса) |
| `FocusedObjects` | `IReadOnlyList<CameraFocusTarget>` | Стек групп фокуса, приоритет у последней |
| `Borders` | `IReadOnlyList<RectTransform>` | Стек границ, активна последняя |
| `IsBordered` | `bool` | Включены ли ограничения (default: true) |

Ownership: `Position` и `Target` принадлежат `CameraMoveController` — изменение только через контроллер.

## Контроллеры

### CameraMoveController

Статический контроллер покадрового обновления. Работает через `TimeController.AddCallback` (FixedUpdate).

**Логика:**
- Без фокуса: `Position` следует за `transform.position` (камера управляется извне)
- С фокусом: `transform.position` задаётся из `Position` (камера управляется моделью)
- `Target` всегда вычисляется как центр масс последней группы фокуса

```csharp
// Extension method для установки позиции из View
data.SetPosition(new Vector2(1, 2));
```

### CameraFocusController

Extension methods на `CameraDataStorage` для управления фокусом.

```csharp
var cam = CameraBus.Get("Map Camera");

// Добавить объект в текущую группу (создаст группу если нет)
cam.AddInFocus(transform);
cam.AddInFocus(transforms);  // ICollection<Transform>

// Заменить фокус новой группой
cam.SetNewFocusGroup(transform);
cam.SetNewFocusGroup(transforms);

// Удалить
cam.RemoveLastFocusGroup();     // удалить последнюю группу
cam.RemoveTargetFromFocus(transform);  // удалить объект из всех групп
cam.ResetFocus();               // очистить все группы
```

**Стек фокуса:** группы работают по принципу LIFO. Камера центрируется на последней группе. При удалении последней — фокус возвращается к предыдущей.

### CameraBordersController

Extension methods на `CameraModel` для управления границами.

```csharp
// Добавить/удалить границу (RectTransform)
camera.Data.AddBorder(rectTransform);
camera.Data.RemoveBorder(rectTransform);
camera.Data.ClearBorders();
```

Границы работают аналогично фокусу — стек, активна последняя.

## View

### CameraDataStorage

MonoBehaviour-компонент камеры. Реализует `IDataStorage` для совместимости с `DataStorageView<T>`.

- Автоматически регистрируется/снимается в `CameraBus`
- Обновляет `CameraRect` при изменении `orthographicSize`
- `RequireComponent(Camera)`

### CameraHandler

Базовый абстрактный класс для хендлеров, привязывающихся к камере по имени. Автоматически подписывается на `CameraBus` и переподключается при регистрации/удалении камер.

```csharp
public class MyHandler : CameraHandler
{
    protected override void SetData()
    {
        // Camera доступна, привязать данные
    }

    protected override void RemoveData()
    {
        // Отвязать данные
    }
}
```

Настройки в Inspector:
- `cameraName` — имя GameObject камеры
- `useAnyIfNotFoundKey` — fallback на первую доступную камеру

### CameraMoveHandler

Плавное приведение камеры к целевой позиции с учётом границ. Наследует `DataStorageView<CameraModel>`.

| Параметр | Диапазон | Default | Описание |
|----------|----------|---------|----------|
| `easeDuration` | 0–3 сек | 1 | Время анимации при дальнем перемещении |
| `easeType` | EaseType | Linear | Функция плавности |
| `followingRange` | 0–30 | 5 | Радиус мгновенного следования |

**Логика:**
- Если расстояние до цели <= `followingRange` — мгновенное перемещение
- Если дальше — плавная анимация через `AsyncTween`
- Позиция ограничивается активной границей (`Borders[^1]`) с учётом размера камеры

### FocusHandler

Декларативная привязка `transform` к фокусу камеры. `OnEnable` добавляет, `OnDisable` удаляет.

Режимы (`FocusMode`):
- `AddToFocus` — добавить в текущую группу
- `NewFocus` — создать новую группу (заменить фокус)

### BordersHandler

Декларативная привязка `RectTransform` как границы камеры. `SetData` добавляет, `RemoveData` удаляет.

## Настройка сцены

```
Scene
├── Map Camera                    # GameObject с Camera
│   ├── CameraDataStorage         # хранилище модели
│   └── CameraMoveHandler         # плавное следование
│       source → Map Camera (CameraDataStorage)
├── Map
│   ├── Borders                   # RectTransform зоны перемещения
│   │   └── BordersHandler        # cameraName = "Map Camera"
│   └── Objects
│       └── Player
│           └── FocusHandler      # cameraName = "Map Camera"
```

## Граничные случаи

- **Камера зарегистрирована позже хендлера**: `CameraHandler` подписан на `CameraBus.OnRegistration` и автоматически подхватит камеру
- **Нет фокуса**: камера не управляется моделью, `transform.position` читается как есть
- **Границы меньше камеры**: `Rect` с отрицательным размером, `Clamp` схлопнет позицию в центр границ
- **Несколько камер**: каждая независимо регистрируется в `CameraBus` по имени GameObject

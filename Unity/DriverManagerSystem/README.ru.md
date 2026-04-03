# DriverManagerSystem

**Namespace:** `Vortex.Unity.DriverManagerSystem`
**Сборка:** `ru.vortex.drivermanager`

## Назначение

Централизованная конфигурация и валидация соответствия между системами ядра (`AudioController`, `Database`, `SaveController`) и их платформозависимыми драйверами (`AudioDriver`, `DatabaseDriver`, `SaveSystemDriver`).

Возможности:
- Таблица соответствий «Система → Драйвер» в одном ScriptableObject-ассете
- Автообнаружение всех `ISystemController` и совместимых драйверов через рефлексию
- Кодогенерация типобезопасного белого списка (`DriversGenericList.cs`)
- Валидация полноты конфигурации перед сохранением
- Проверка при загрузке проекта: предупреждение при отсутствии или рассинхроне `DriversGenericList.cs`

Вне ответственности:
- Создание и регистрация драйверов (реализуется в конкретных системах)
- Runtime-инициализация систем (выполняется через `Loader`)

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `ISystemController`, `ISystemDriver` — интерфейсы систем и драйверов |
| `Vortex.Unity.CoreAssetsSystem` | `ICoreAsset` — маркер автосоздаваемого ассета |
| `Vortex.Unity.EditorTools` | `[LabelText]` — атрибуты Inspector |
| `Vortex.Unity.Extensions` | `MenuConfigSearchController` — навигация к ассету |
| Odin Inspector | `[Button]`, `[InfoBox]`, `[ValueDropdown]`, `[ListDrawerSettings]` |

---

## Архитектура

```
DriverManagerSystem/
├── Base/
│   ├── DriverConfig.cs                 # ScriptableObject — таблица соответствий
│   └── DriverRecord.cs                 # Строка таблицы: система → драйвер
├── Editor/
│   └── MenuConfigSearchController.cs   # Меню: Vortex → Configs → Drivers Config
└── (generated) DriversGenericList.cs   # Автогенерируемый белый список
```

### DriverConfig

ScriptableObject (`ICoreAsset`), хранит массив `DriverRecord[]`. Располагается в `Assets/Resources/`, создаётся автоматически при первом запуске через `Vortex/Debug/Check Core Assets`.

Редакторные операции:
- **Reload** — сканирует сборки, находит все `ISystemController`, обновляет таблицу (сохраняя существующие привязки)
- **Save Config** — валидирует полноту назначений и генерирует `DriversGenericList.cs`

При загрузке проекта (`InitializeOnLoadMethod`) проверяет наличие и актуальность `DriversGenericList.cs`.

### DriverRecord

Сериализуемая строка таблицы. Хранит `AssemblyQualifiedName` системы и выбранного драйвера. В Inspector отображает:
- Имя системы как label
- Dropdown совместимых драйверов (фильтрация по интерфейсу через `GetDriverType()`)
- Опция `[Switched Off]` для отключения системы

Определение совместимости: рефлексия вызывает статический метод `GetDriverType()` у системы, получает тип интерфейса драйвера, затем ищет все конкретные реализации этого интерфейса в сборках `ru.vortex.*`.

### DriversGenericList.cs (автогенерируемый)

Статический класс в namespace `Vortex.Core.System`. Содержит `Dictionary<string, string> WhiteList` — маппинг `AssemblyQualifiedName` системы на `AssemblyQualifiedName` разрешённого драйвера.

Используется в `SystemController<T, TD>.SetDriver()` для runtime-валидации: подключить можно только тот драйвер, который указан в белом списке.

---

## Контракт

### Вход
- Набор систем, реализующих `ISystemController`
- Набор драйверов, реализующих соответствующие `ISystemDriver`-интерфейсы

### Выход
- `DriversGenericList.cs` — compile-time белый список для runtime-валидации
- `DriverConfig.GetDriverForSystem(systemName)` — runtime-запрос назначенного драйвера

### Гарантии
- Невозможно сохранить конфигурацию с пустыми назначениями
- Невозможно подключить несовместимый драйвер в runtime
- Раннее выявление ошибок: лог при загрузке проекта, если конфигурация рассинхронизирована

---

## Использование

1. Открыть `DriverConfig` через меню **Vortex → Configs → Drivers Config**
2. Нажать **Reload** для обнаружения всех систем
3. Назначить драйвер каждой системе через dropdown
4. Нажать **Save Config** — сгенерируется `DriversGenericList.cs`
5. При старте приложения драйверы подключаются через `SystemController.SetDriver()` с валидацией по белому списку

```csharp
// Создание системы
public class MyService : SystemController<MyService, IMyDriver> { ... }

// Создание драйвера
public class MyServiceDriver : IMyDriver { ... }

// В DriverConfig: Reload → выбрать MyServiceDriver → Save Config

// Runtime (внутри драйвера):
MyService.SetDriver(this); // валидация по DriversGenericList.WhiteList
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Добавлена новая система | Не появится в таблице до нажатия Reload |
| Не все драйверы назначены | Save Config отклоняется с `LogError` |
| `DriversGenericList.cs` удалён | Предупреждение при загрузке проекта |
| `DriversGenericList.cs` не соответствует DriverConfig | Предупреждение при загрузке проекта |
| `SetDriver()` с неразрешённым драйвером | Ошибка валидации в runtime |
| Флаг `onlyInVortexSearch` отключён | Поиск драйверов во всех сборках проекта |
| Система выбрана как `[Switched Off]` | Драйвер не назначается, система не инициализируется |

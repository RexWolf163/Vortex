# MapLevels

**Namespace:** `Vortex.Sdk.MapLevels`
**Assembly:** `ru.vortex.sdk.maplevels`

## Активация

Пакет активируется через `SdkSettings` (меню **Tools → Vortex → Configs → SDK Settings**), тоггл **`mapLevelsSdk`**. Тоггл управляет define-символом `USING_VORTEX_MAP_LEVELS`, который указан в `defineConstraints` asmdef'а — при выключении пакет не компилируется и его типы недоступны.

Канон описания активации SDK-пакетов: `Vortex/Sdk/SdkSettingsSystem/README.ru.md`.

Файл-расширение тогглера: `DefineSettings/SdkSettings.MapLevels.cs` — partial-кусок к `Vortex.Sdk.SdkSettingsSystem.SdkSettings`.

## Назначение

Управление графовой картой уровней с ленивой инстанциацией префабов и стримингом соседей. Активный уровень переключается через `Enter(guid)`, прямые соседи (hop=1) подгружаются в фоне, дальние (на дистанции `>= unloadDistance`) выгружаются.

Возможности:
- Каталог уровней строится из `Database.GetRecords<MapLevelModel>()` при `Init`
- Ленивая синхронная инстанциация префаба активного уровня
- Фоновый стриминг соседей через `UniTask.Yield` (распределение по кадрам)
- BFS по графу `NeighborGuids` с ограничением глубины удержания
- Подменяемая реализация контроллера через `MapLevelsSettings.controllerTypeName` (рефлексия)
- Сохранение текущего уровня в `MapLevelsGameData` (часть `GameModel` через `IGameData`)
- Регистрация контейнера-родителя сцены через `MapsView` / `RegisterMapsParent`
- Автоматическая переброска инстансов между сценовым контейнером и hidden-pool (`MapVoid`)

Вне ответственности:
- Содержимое уровня (NPC, объекты, логика) — задаётся префабом
- Точки спауна между уровнями — описание в `MapGate`, обработка перехода — задача проекта
- Адресная загрузка ассетов — префаб уровня хранится прямой ссылкой в `MapLevelPreset`

## Зависимости

### Core
- `Vortex.Core.AppSystem` — `App`, `AppStates`
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`, `RecordPreset`
- `Vortex.Core.SettingsSystem` — `Settings`, `SettingsModel` (partial-расширение)
- `Vortex.Core.System.Abstractions` — `Singleton<T>`
- `Vortex.Core.Extensions.LogicExtensions` — `IsNullOrWhitespace`, `InitValve`, `[POCO]`, `SerializeProperties`
- `Vortex.Core.Extensions.ReactiveValues` — `EnumData`, `StringData`, `IReactiveData`

### Unity
- `Vortex.Unity.SettingsSystem` — `SettingsPreset`
- `Vortex.Unity.DatabaseSystem` — `RecordPreset<T>`, `[DbRecord]`
- `Vortex.Unity.EditorTools` — `[ValueSelector]`
- `Vortex.Unity.UI.StateSwitcher` — `UIStateSwitcher`, `[StateSwitcher]`
- `Vortex.Unity.Extensions.ReactiveValues` — `Vector2Data`

### Sdk
- `Vortex.Sdk.Core.GameCore` — `GameController`, `GameModel.IGameData`
- `Vortex.Sdk.SdkSettingsSystem` — `[DefineSymbol]`

### Внешние
- UniTask — асинхронный стриминг
- Sirenix Odin Inspector — инспектор-атрибуты `MapGate`

## Архитектура

```
ru.vortex.sdk.maplevels
├── Abstractions/
│   └── IMapObject.cs                  ← базовый контракт объекта карты
├── Bus/
│   └── MapLevelsBus.cs                ← статическая шина: Controller, Config, MapsParent
├── Config/
│   ├── MapLevelsSettings.cs           ← SettingsPreset (unloadDistance, controllerTypeName)
│   └── SettingsModelExt/
│       ├── MapLevelsConfig.cs         ← runtime-конфиг (POCO)
│       └── SettingsModelExtMapLevels.cs ← partial SettingsModel.MapLevels
├── Controllers/
│   ├── MapLevelsController.cs            ← Singleton-каркас, IsInitialized, Model
│   ├── MapLevelsController.Lifecycle.cs  ← Init / Cleanup, BuildCatalog, привязка GameController
│   ├── MapLevelsController.Active.cs     ← Enter, переключение активного уровня
│   └── MapLevelsController.Streaming.cs  ← BFS, EnsureLoaded, Unload, ApplyStreamingPlan
├── DefineSettings/
│   └── SdkSettings.MapLevels.cs       ← partial SdkSettings, тогглер mapLevelsSdk
├── Interfaces/
│   ├── IMapLevelsController.cs        ← публичный контракт контроллера
│   └── IMapLevelView.cs               ← Editor-only поставщик MapGate[]
├── Model/
│   ├── MapContainer.cs                ← per-level: GameObject Instance + EnumData<State>
│   ├── MapContainerState.cs           ← Empty / Loading / Loaded / Unloading
│   ├── MapGate.cs                     ← struct: id, gatePoint, target (mapID||gateID)
│   ├── MapLevelModel.cs               ← Record: Prefab, NeighborGuids
│   ├── MapLevelViewModel.cs           ← POCO под сериализуемое состояние представления
│   ├── MapLevelsGameData.cs           ← GameModel.IGameData: CurrentLevelGuid
│   └── MapLevelsModel.cs              ← runtime-агрегатор: Catalog, Containers, ActiveLevelGuid
├── Presets/
│   └── MapLevelPreset.cs              ← RecordPreset<MapLevelModel>: Prefab, NeighborGuids (Singleton)
└── View/
    ├── MapLevelView.cs                ← MonoBehaviour на префабе уровня, переключает UIStateSwitcher
    └── MapsView.cs                    ← регистратор сценового контейнера-родителя
```

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `MapLevelsBus` | static | Шина: `Controller`, `Config`, `MapsParent`, `OnReady`, `OnRelease` |
| `MapLevelsController` | `Singleton<T>`, `IMapLevelsController`, partial | Реализация по умолчанию: каталог, активный уровень, стриминг |
| `IMapLevelsController` | interface | Контракт контроллера: `Init`, `Enter`, `Cleanup` + события |
| `MapLevelsModel` | `IReactiveData` | Runtime: `Catalog`, `Containers`, `ActiveLevelGuid` |
| `MapLevelsGameData` | `GameModel.IGameData`, `[POCO]` | Сохраняемое состояние: `CurrentLevelGuid` |
| `MapLevelModel` | `Record` | Рабочая копия уровня: `Prefab`, `NeighborGuids` |
| `MapLevelPreset` | `RecordPreset<MapLevelModel>` | Пресет уровня (фиксирован как `Singleton`) |
| `MapContainer` | sealed class | Per-level: `Instance`, `EnumData<MapContainerState>` |
| `MapContainerState` | enum | `Empty`, `Loading`, `Loaded`, `Unloading` |
| `MapGate` | struct | Точка спауна: `id`, `gatePoint`, `target` (`mapID‖gateID`) |
| `MapLevelView` | `MonoBehaviour`, `IMapLevelView` | На префабе уровня — переключает `UIStateSwitcher` (`On`/`Off`) |
| `MapsView` | `MonoBehaviour` | Регистрирует/снимает сценовой `MapsParent` в шине |
| `MapLevelsSettings` | `SettingsPreset` | `unloadDistance` (1–16), `controllerTypeName` |
| `MapLevelsConfig` | POCO | Runtime-конфиг (генерируется из `MapLevelsSettings`) |
| `IMapObject` | `[POCO]` interface | Базовый контракт объекта карты (`Vector2Data Position`) |

## Контракт

### Вход
- `MapLevelsBus.Controller.Enter(guid, gateId = null)` — переключение активного уровня
- `MapLevelsBus.RegisterMapsParent(Transform)` / `UnregisterMapsParent(Transform)` — привязка контейнера сцены
- `MapLevelsSettings` — `unloadDistance`, `controllerTypeName` (резолв через `IMapLevelsController`)

### Выход
- `MapLevelsBus.OnReady` (`InitValve`) — контроллер инициализирован
- `MapLevelsBus.OnRelease` — контроллер очищен
- `MapLevelsBus.OnMapsContainerRegistered` / `OnMapsContainerReleased` — контейнер сцены привязан (`RegisterMapsParent`) / отвязан (`UnregisterMapsParent`)
- `IMapLevelsController.OnActiveLevelChanged(guid, gateId)` — смена активного уровня
- `IMapLevelsController.OnLevelLoaded(guid)` / `OnLevelUnloaded(guid)` — события стриминга
- `MapLevelsModel.ActiveLevelGuid` — `StringData` для подписки

### Гарантии
- `Init` идемпотентен: повторный вызов на инициализированном контроллере — no-op
- `Enter` синхронно гарантирует `Loaded` для целевого уровня перед сменой `ActiveLevelGuid`
- Стриминг соседей — async-fire-and-forget, проверяет `IsInitialized` и совпадение `ActiveLevelGuid`
- `OnActiveLevelChanged` не диспатчится повторно для того же `guid`
- При загрузке (`OnLoadGame`) контроллер сбрасывает все инстансы и заходит в `CurrentLevelGuid`
- `MapLevelPreset.OnValidate` форсит `RecordTypes.Singleton`

### Ограничения
- `MapLevelPreset` фиксирован как `Singleton` — на каждый пресет одна runtime-копия
- Префаб хранится прямой ссылкой (не Addressable) — все префабы попадают в билд
- При отсутствии сценового `MapsParent` инстансы складируются в hidden `MapVoid` (DontDestroyOnLoad)
- Резолв `controllerTypeName` идёт через рефлексию по всем сборкам — тип должен иметь публичный конструктор без параметров

## Поток жизненного цикла

1. `[RuntimeInitializeOnLoadMethod]` `MapLevelsBus.Bootstrap` подписывает создание контроллера на `Settings.OnInit`
2. `Settings.OnInit` → `CreateController` → читает `Settings.Data().MapLevels`, резолвит тип, создаёт через `Activator.CreateInstance`, вызывает `Init`
3. `Init` строит `Catalog` из БД, подписывается на `GameController.OnNewGame` / `OnLoadGame`, выставляет `IsInitialized = true`, вызывает `EnterCurrentFromGameData`
4. `Enter(guid)` → `EnsureLoaded` (синхронная инстанциация) → `ActiveLevelGuid.Set` → `OnActiveLevelChanged` → `ApplyStreamingPlan` (async)
5. `ApplyStreamingPlan` — BFS, инстанциация `hop=1`, выгрузка `>= unloadDistance`
6. `App.OnExit` → `Dispose` (отписка)

## Пример: создание уровня

```csharp
// 1. Создать MapLevelPreset через меню Database/MapLevel Preset
//    Назначить prefab и neighborGuids (через DbRecord-селектор)

// 2. На корне префаба — MapLevelView с UIStateSwitcher (SwitcherState.On/Off)
//    и опциональным массивом MapGate

// 3. На сцене — GameObject с MapsView (контейнер для инстансов уровней)

// 4. Переключение из логики проекта:
MapLevelsBus.OnReady.Subscribe(() =>
{
    MapLevelsBus.Controller.Enter(targetLevelGuid, gateId: "spawn_north");
});
```

## Пример: подмена контроллера

```csharp
// Реализация IMapLevelsController в проекте
public sealed class MyMapLevelsController : Singleton<MyMapLevelsController>, IMapLevelsController { ... }

// В MapLevelsSettings → controllerTypeName выбрать через ValueSelector
// (поставщик типов сканирует домен и фильтрует по IMapLevelsController + ctor())
```

## Граничные случаи

- `controllerTypeName` пуст → используется `MapLevelsController` по умолчанию
- `controllerTypeName` не резолвится → `LogError`, контроллер не создаётся, `IsReady = false`
- `Enter` до `Init` → `LogError`, no-op
- `Enter` с неизвестным `guid` → `LogError`, no-op
- `MapsParent` уже зарегистрирован при повторной попытке → `LogError`, `RegisterMapsParent` возвращает `false`
- `App.GetState() == AppStates.Stopping` при переброске инстансов → инстансы уничтожаются вместо переноса
- `unloadDistance <= 1` → удержание только активного уровня; соседи hop=1 загружаются, но тут же выгружаются на следующем `Enter`

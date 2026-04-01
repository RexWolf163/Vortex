# SteamConnectionSystem

**Namespace:** `Vortex.Steam.SteamConnectionSystem`
**Сборка:** `ru.vortex.steam.connection`
**Платформа:** Unity 2021.3+, Steamworks.NET
**Условная компиляция:** код за `#if USING_STEAM`, сборка без `defineConstraints`

---

## Назначение

Подключение к Steamworks API. Инициализирует Steam-клиент, предоставляет глобальное состояние подключения и данные текущего пользователя (профиль, список друзей) через статическую шину `SteamBus`.

Возможности:

- `SteamBus` — статическая шина: `SteamEnabled`, `IsInitialized`, `IsLoaded`, `User`, события `OnCallServices`/`OnLoaded`
- `SteamManager` — MonoBehaviour-синглтон: `SteamAPI.Init()`, `RunCallbacks()`, `Shutdown()`
- `SteamUserData` / `SteamUserShortData` — модели данных пользователя и друзей
- `SteamConnectionSettings` — ScriptableObject-конфигурация: `steamAppId`, `isEnabled`, `isTestBuild`
- `DefineSymbolManager` — автоматическое управление символом `USING_STEAM` в `PlayerSettings`
- `Settings` — загрузка/создание ассета настроек, синхронизация `steam_appid.txt`

Вне ответственности:

- Достижения — пакет `SteamAchievements`
- Мультиплеер, матчмейкинг, лобби
- Покупки, DLC, инвентарь

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Steamworks.NET` | `SteamAPI`, `SteamUser`, `SteamFriends`, `CSteamID` |
| `Vortex.Unity.EditorTools` | `[OnChanged]`, `[ToggleButton]` для Inspector |

---

## Архитектура

```
SteamBus (static)
  ├── SteamEnabled: const bool          ← #if USING_STEAM: true, иначе false
  ├── IsInitialized: bool               ← SteamAPI.Init() прошёл успешно
  ├── IsLoaded: bool                    ← данные пользователя загружены
  ├── User: SteamUserData               ← профиль + друзья
  ├── OnCallServices: Action            ← сигнал для подписчиков загрузить свои данные
  └── OnLoaded: Action

SteamManager : MonoBehaviour (singleton)
  ├── [RuntimeInitializeOnLoadMethod] Init()
  │   └── new GameObject("SteamManager").AddComponent<SteamManager>()
  ├── Awake()
  │   ├── RestartAppIfNecessary(AppId) → Application.Quit() если не через Steam
  │   ├── SteamAPI.Init() → m_bInitialized
  │   ├── Callback<UserStatsReceived_t>.Create()
  │   ├── SteamBus.IsInitialized = true
  │   └── LoadStatsInBus()
  │       ├── SteamBus.LoadServices() → OnCallServices
  │       ├── SteamBus.User.Init(steamId)
  │       └── SteamBus.IsLoaded = true
  ├── Update() → SteamAPI.RunCallbacks()
  └── OnDestroy() → SteamAPI.Shutdown()

SteamUserData : SteamUserShortData
  ├── SteamID: CSteamID
  ├── Name: string
  ├── Friends: Dictionary<CSteamID, SteamUserShortData>
  ├── OnUpdated: Action
  └── Init(steamId) → загрузка профиля и списка друзей

SteamUserShortData
  ├── SteamID: CSteamID
  ├── Name: string
  └── GetShortData(steamId) → factory

SteamConnectionSettings : ScriptableObject
  ├── steamAppId: uint (default 480)
  ├── isEnabled: bool                   ← [OnChanged] → DefineSymbolManager.Refresh()
  ├── isTestBuild: bool                 ← пропускает RestartAppIfNecessary
  └── OnAppUdChanged() → File.WriteAllText("steam_appid.txt")

DefineSymbolManager (Editor-only)
  ├── [InitializeOnLoadMethod] Refresh()
  └── ApplyDefineSymbol(bool, "USING_STEAM") → PlayerSettings

Settings (internal)
  ├── GetSettings() → Resources.LoadAll / CreateAsset
  └── [InitializeOnLoadMethod] CheckSteamAppId() → синхронизация steam_appid.txt
```

### Порядок инициализации

1. `DefineSymbolManager.Refresh()` — при загрузке Editor устанавливает/удаляет `USING_STEAM`
2. `Settings.CheckSteamAppId()` — синхронизирует `steam_appid.txt` с ассетом настроек
3. `SteamManager.Init()` — `RuntimeInitializeOnLoadMethod`, создаёт GameObject
4. `SteamManager.Awake()` — `SteamAPI.Init()`, загрузка данных пользователя
5. `SteamBus.LoadServices()` — `OnCallServices` для подписчиков (например, `AchievementsController`)

### Условная компиляция

Сборка `ru.vortex.steam.connection` не имеет `defineConstraints` — компилируется всегда. Весь Steamworks-код внутри `#if USING_STEAM`. Без символа `SteamBus.SteamEnabled = false`, `IsInitialized` не может стать `true`. Это позволяет другим сборкам ссылаться на `SteamBus` без зависимости от Steamworks.

### Безопасный режим без Steam

При `isTestBuild = true` пропускается `RestartAppIfNecessary()` — приложение не перезапускается через Steam-клиент. Используется для отладки из редактора.

---

## Контракт

### Вход

- `SteamConnectionSettings` в `Resources/Editor/` — `steamAppId`, `isEnabled`, `isTestBuild`
- Steamworks.NET подключён к проекту
- Steam-клиент запущен на машине

### Выход

- `SteamBus.IsInitialized` — Steam API инициализирован
- `SteamBus.IsLoaded` — данные пользователя загружены
- `SteamBus.User` — профиль, имя, список друзей
- `SteamBus.OnCallServices` — момент для загрузки зависимых сервисов

### API

| Метод / Свойство | Описание |
|------------------|----------|
| `SteamBus.SteamEnabled` | `const bool` — Steam подключён на уровне компиляции |
| `SteamBus.IsInitialized` | `SteamAPI.Init()` прошёл успешно |
| `SteamBus.IsLoaded` | Данные пользователя загружены |
| `SteamBus.User` | `SteamUserData` — текущий пользователь |
| `SteamBus.User.Friends` | `Dictionary<CSteamID, SteamUserShortData>` |
| `SteamBus.User.OnUpdated` | Событие при обновлении статистики (`UserStatsReceived_t`) |
| `SteamBus.OnCallServices` | Событие для инициализации зависимых сервисов |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Требует запущенный Steam-клиент | `SteamAPI.Init()` возвращает `false` без клиента |
| `steam_appid.txt` должен совпадать с настройками | Автоматическая синхронизация при загрузке Editor |
| `DontDestroyOnLoad` на `SteamManager` | Единственный экземпляр на весь жизненный цикл |
| Steamworks-вызовы запрещены в `OnDestroy` | `SteamAPI.Shutdown()` уже мог быть вызван |
| Платформы: Windows, Linux, macOS | `DefineSymbolManager` фильтрует по standalone-платформам |

---

## Использование

### Настройка проекта

1. Подключите Steamworks.NET к проекту
2. Откройте `SteamConnectionSettings` в `Resources/Editor/` (создаётся автоматически)
3. Укажите `steamAppId` вашего приложения
4. Включите `isEnabled` — символ `USING_STEAM` добавится автоматически
5. Для отладки без Steam-клиента включите `isTestBuild`

### Чтение данных пользователя

```csharp
if (SteamBus.IsLoaded)
{
    var name = SteamBus.User.Name;
    var friends = SteamBus.User.Friends;
}
```

### Подписка на загрузку сервисов

```csharp
// Подключение к моменту инициализации Steam
SteamBus.OnCallServices += LoadMyService;

// Подписка на обновление статистики
SteamBus.User.OnUpdated += OnStatsReceived;
```

### Условная компиляция в игровом коде

```csharp
#if USING_STEAM
    SteamBus.User.UnlockAchievement("ACH_001");
#endif

// Или через const:
if (SteamBus.SteamEnabled)
    DoSteamStuff();
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Steam-клиент не запущен | `SteamAPI.Init()` → `false`, `IsInitialized` остаётся `false` |
| `steam_api.dll` не найден | `DllNotFoundException`, `Application.Quit()` |
| `isTestBuild = false`, запуск не через Steam | `RestartAppIfNecessary()` → `true`, перезапуск через Steam-клиент |
| `isTestBuild = true` | `RestartAppIfNecessary()` пропускается |
| Повторная инициализация `SteamManager` | `Debug.LogError`, `Destroy(gameObject)` |
| `USING_STEAM` не определён | `SteamBus.SteamEnabled = false`, `IsInitialized` всегда `false` |
| Дублирующий друг в списке | `TryAdd` → `Debug.LogError("There is broken data")` |
| Domain Reload отключён | `InitOnPlayMode()` сбрасывает статические поля |
| `steamAppId = 0` | Автоматически заменяется на `480` при загрузке Editor |

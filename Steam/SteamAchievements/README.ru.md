# SteamAchievements

**Namespace:** `Vortex.Steam.SteamAchievements`
**Сборка:** `ru.vortex.steam.achievements`
**Платформа:** Unity 2021.3+, Steamworks.NET
**Условная компиляция:** `defineConstraints: ["USING_STEAM"]` — сборка компилируется только при наличии символа

---

## Назначение

Управление Steam-достижениями. Загружает индекс достижений из `SteamUserStats`, предоставляет API для разблокировки и чтения через extension-методы на `SteamUserData`.

Возможности:

- `AchievementsController` — внутренний статический контроллер: загрузка индекса достижений при инициализации Steam
- `AchievementsExtensions` — extension-методы на `SteamUserData`: `UnlockAchievement()`, `GetAchievement()`, `GetAllAchievementsID()`
- `Achievement` — модель данных: ID, Name, Description, IsUnlocked, IsHidden
- `AchievementsManager` — Editor-only MonoBehaviour: отображение и переключение достижений в Inspector
- `AchievementHandler` — Editor-only модель элемента для Inspector с `[ToggleButton]` и `[ClassLabel]`

Вне ответственности:

- Подключение к Steam, инициализация API — пакет `SteamConnectionSystem`
- Статистика Steam (leaderboards, stats) — не реализовано
- Runtime UI достижений — реализуется в проектном слое

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Steamworks.NET` | `SteamUserStats` — чтение/запись достижений |
| `Vortex.Steam.SteamConnectionSystem` | `SteamBus` — состояние подключения, `SteamUserData` — якорь extension-методов |
| `Vortex.Unity.AppSystem` | `TimeController.Accumulate()` — батчинг `StoreStats()` |
| `Vortex.Unity.EditorTools` | `[VortexCollection]`, `[ClassLabel]`, `[InfoBubble]`, `[ToggleButton]`, `[OnChanged]`, `[LabelText]` |

---

## Архитектура

```
AchievementsController (internal static)
  ├── _achievementsIndex: Dictionary<string, Achievement>
  ├── [RuntimeInitializeOnLoadMethod] Run()
  │   ├── SteamBus.IsInitialized → LoadAllAchievements()
  │   └── иначе → SteamBus.OnCallServices += LoadAllAchievements
  └── LoadAllAchievements()
      ├── SteamUserStats.GetNumAchievements()
      └── foreach → Achievement { ID, Name, Description, IsUnlocked, IsHidden }

AchievementsExtensions (static, extension на SteamUserData)
  ├── UnlockAchievement(id)
  │   ├── SteamUserStats.SetAchievement(id)
  │   └── TimeController.Accumulate(() => StoreStats())
  ├── GetAchievement(id) → Achievement
  ├── GetAllAchievementsID() → string[]
  ├── ClearAchievement(id)          ← Editor-only
  └── ResetAllAchievements()        ← Editor-only

Achievement
  ├── ID: string
  ├── Name: string
  ├── Description: string
  ├── IsUnlocked: bool
  └── IsHidden: bool

AchievementsManager : MonoBehaviour (Editor-only singleton)
  ├── index: AchievementHandler[]    ← [VortexCollection]
  ├── [RuntimeInitializeOnLoadMethod] Init() → создаёт тестовый GameObject
  ├── Start() → SteamBus.User.OnUpdated += Refresh
  └── Refresh() → заполняет index из SteamBus

AchievementHandler (Editor-only, Serializable)
  ├── isUnlocked: bool               ← [ToggleButton] + [OnChanged("UpdateAchievements")]
  ├── Id, Name, Description
  ├── Label() → [ClassLabel]
  ├── Info() → [InfoBubble]
  └── UpdateAchievements() → Unlock/Clear
```

### Загрузка индекса

1. `AchievementsController.Run()` вызывается через `RuntimeInitializeOnLoadMethod`
2. Если `SteamBus.IsInitialized` — загрузка сразу, иначе — подписка на `OnCallServices`
3. `LoadAllAchievements()` итерирует `SteamUserStats.GetNumAchievements()`, заполняет `_achievementsIndex`
4. Для каждого достижения читаются: ID, name, desc, unlocked, hidden

### Батчинг StoreStats

`UnlockAchievement()` вызывает `SteamUserStats.SetAchievement()` немедленно, но `StoreStats()` откладывается через `TimeController.Accumulate()`. При нескольких разблокировках за кадр `StoreStats()` вызовется один раз.

### Editor-инструмент

`AchievementsManager` создаётся автоматически (`RuntimeInitializeOnLoadMethod`) только в Editor при `USING_STEAM`. Отображает массив `AchievementHandler` в Inspector с toggle-кнопками для разблокировки/сброса достижений. Подписывается на `SteamBus.User.OnUpdated` для обновления состояния.

---

## Контракт

### Вход

- `SteamBus.IsInitialized = true` — Steam API инициализирован
- Достижения настроены в Steamworks (App Admin → Achievements)
- `SteamUserStats.RequestCurrentStats()` вызван (неявно через `SteamManager`)

### Выход

- `Achievement` модели с актуальным состоянием (`IsUnlocked`)
- Разблокировка через `SteamBus.User.UnlockAchievement(id)`
- Список всех ID через `SteamBus.User.GetAllAchievementsID()`

### API

| Метод | Описание |
|-------|----------|
| `SteamBus.User.UnlockAchievement(id)` | Разблокировать достижение |
| `SteamBus.User.GetAchievement(id)` | Получить `Achievement` по ID или `null` |
| `SteamBus.User.GetAllAchievementsID()` | Все ID достижений проекта |
| `AchievementsExtensions.ClearAchievement(id)` | Сбросить достижение (Editor-only) |
| `AchievementsExtensions.ResetAllAchievements()` | Сбросить все достижения (Editor-only) |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `defineConstraints: ["USING_STEAM"]` | Сборка не компилируется без символа |
| `UnlockAchievement` требует `SteamBus.IsLoaded` | Данные пользователя должны быть загружены |
| `ClearAchievement` / `ResetAllAchievements` — Editor-only | Только для тестирования |
| `AchievementsManager` — Editor-only | Не включается в билд |
| Не использует Database bus | Независимая от Vortex-шины архитектура |

---

## Использование

### Разблокировка достижения

```csharp
#if USING_STEAM
if (SteamBus.IsLoaded)
    SteamBus.User.UnlockAchievement("ACH_WIN_FIRST_BATTLE");
#endif
```

### Проверка состояния

```csharp
#if USING_STEAM
var ach = SteamBus.User.GetAchievement("ACH_WIN_FIRST_BATTLE");
if (ach != null && !ach.IsUnlocked)
    ShowAchievementHint();
#endif
```

### Получение всех достижений

```csharp
#if USING_STEAM
var ids = SteamBus.User.GetAllAchievementsID();
foreach (var id in ids)
{
    var ach = SteamBus.User.GetAchievement(id);
    Debug.Log($"{ach.Name}: {(ach.IsUnlocked ? "Unlocked" : "Locked")}");
}
#endif
```

### Тестирование в Editor

1. Запустите Play Mode с включённым `isEnabled` и `isTestBuild` в `SteamConnectionSettings`
2. Найдите объект `AchievementsManager [TEST]` в Hierarchy
3. В Inspector используйте toggle-кнопки для разблокировки/сброса

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `SteamBus.IsLoaded = false` при вызове `UnlockAchievement` | No-op, ранний return |
| Несуществующий `achievementID` | `GetAchievement()` → `null`, `Debug.LogError` |
| Несколько `UnlockAchievement` за кадр | `SetAchievement()` для каждого, `StoreStats()` один раз (батчинг) |
| Steam не настроен (нет достижений в Steamworks) | `GetNumAchievements()` → 0, пустой индекс |
| `USING_STEAM` не определён | Сборка не компилируется (`defineConstraints`) |
| `AchievementsController.Run()` до `SteamBus.IsInitialized` | Подписка на `OnCallServices`, загрузка отложена |
| Повторный `UnlockAchievement` для уже разблокированного | `SetAchievement()` — idempotent в Steamworks |

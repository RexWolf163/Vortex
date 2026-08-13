# SteamExtensions.Quests

**Namespace:** `Vortex.Steam.SteamExtensions.Quests`
**Сборка:** `ru.vortex.steam.ext.quests`
**Платформа:** Unity 2021.3+, Steamworks.NET, Vortex Quests
**Условная компиляция:** `defineConstraints: ["USING_VORTEX_QUESTS"]` — сборка компилируется только с системой квестов. Обращение к Steam — под `#if USING_STEAM` внутри кода: без символа шаг существует, но выполняется как no-op.

---

## Назначение

Кросс-мост **Vortex Quests ↔ Steam Achievements**: шаги логики квеста (`QuestLogic`), которые при выполнении разблокируют Steam-достижение по его ID.

Пакет намеренно отделён и от ядра квестов, и от пакета Steam — чтобы ни один из них не тянул зависимость на другой. Живёт в `Vortex/Steam/SteamExtensions` (расширения Steam-домена под конкретные системы Vortex).

Возможности:

- `ActivateAchievementsLogic` — `QuestLogic`: разблокирует достижение `achievementId` при прогоне шага. Без `USING_STEAM` — `return false`.
- `IncrementAchievementsLogic` — черновик (файл закомментирован целиком), пока не участвует в сборке. Заготовка под инкрементный прогресс достижения — требует иного Steam API (`IndicateAchievementProgress`), в текущем виде дублирует unlock.

Вне ответственности:

- Инициализация Steam, состояние подключения — пакет `SteamConnectionSystem`
- Загрузка индекса достижений, сам `UnlockAchievement` — пакет `SteamAchievements`
- Оркестрация квестов (старт/прерывание/сейв) — `ru.vortex.sdk.game.quests`

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `ru.vortex.sdk.game.quests` | `QuestLogic` — база шага логики квеста (`USING_VORTEX_QUESTS`) |
| `ru.vortex.steam.achievements` | `SteamUserData.UnlockAchievement()` (`USING_STEAM`) |
| `ru.vortex.steam.connection` | `SteamBus.User` — якорь extension-методов (`USING_STEAM`) |
| `UniTask` | асинхронный `Run` |

> Ссылки на Steam-сборки не требуют символа в самом asmdef: без `USING_STEAM` они не собираются, ссылки игнорируются, а обращения к ним скрыты за `#if USING_STEAM`.

---

## Архитектура

```
ActivateAchievementsLogic : QuestLogic          [Serializable]
  ├── name: string            — метка для редактора (GetEditorLabel)
  ├── achievementId: string   — Steam achievement ID
  ├── Run(CancellationToken) → UniTask<bool>
  │   ├── #if USING_STEAM  → SteamBus.User.UnlockAchievement(achievementId); return true
  │   └── #else            → return false
  └── GetEditorLabel() → "Unlock SteamAchievement: {name}"

IncrementAchievementsLogic — закомментирован (черновик, вне сборки)
```

### Поток

1. Шаг `ActivateAchievementsLogic` стоит в цепочке `QuestLogic[]` квеста и доходит до выполнения.
2. При `USING_STEAM` зовётся `SteamBus.User.UnlockAchievement(achievementId)` — все гарды (`SteamBus.IsLoaded`, наличие ID в индексе, батчинг `StoreStats`) уже внутри extension-метода.
3. Шаг возвращает `true` (fire-and-forget: квест не блокируется на плохом ID). Без `USING_STEAM` — `false`.

---

## Контракт

### Вход

- `USING_VORTEX_QUESTS` определён (иначе пакет не компилируется)
- Квест-ассет содержит `ActivateAchievementsLogic` в `QuestLogic[]` с заданным `achievementId`
- Для реальной разблокировки: `USING_STEAM`, `SteamBus.IsLoaded == true`, достижение настроено в Steamworks

### Выход

- Steam-достижение разблокировано (батчинг `StoreStats` — отложенно)
- Шаг возвращает `true` (Steam) / `false` (без Steam)

### API

| Тип | Описание |
|-----|----------|
| `ActivateAchievementsLogic` | Сериализуемый `QuestLogic`; добавляется в квест через `[SerializeReference] QuestLogic[]` |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `defineConstraints: ["USING_VORTEX_QUESTS"]` | Без системы квестов `QuestLogic` недоступен |
| Без `USING_STEAM` — no-op (`return false`) | Steam-сборки не собраны; вызов скрыт `#if` |
| Не проверяет наличие `achievementId` | Гард (`GetAchievement != null`) — внутри `UnlockAchievement`, шаг всегда `true` |

---

## Использование

1. В квест-ассете добавьте шаг `Activate Achievements Logic` в массив логики.
2. Заполните `name` (метка) и `achievementId` (ID достижения из Steamworks).

```
QuestLogic[]:
  - RunNaniScript ...
  - ActivateAchievementsLogic { name: "First Win", achievementId: "ACH_WIN_FIRST" }
  - ...
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `USING_STEAM` не определён | `Run` → `false`, достижение не тронуто |
| Несуществующий `achievementId` | `UnlockAchievement` логнёт `not found`, шаг всё равно `true` |
| `SteamBus.IsLoaded == false` | `UnlockAchievement` — ранний return (no-op), шаг `true` |
| Повторный вызов для разблокированного | Идемпотентно (Steamworks) |

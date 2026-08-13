# NaniExtensions.SteamAchievements

**Namespace:** `Vortex.NaniExtensions.SteamAchievements`
**Сборка:** `ru.vortex.nani.steam.achievements`
**Платформа:** Unity 2021.3+, Naninovel (`com.elringus.naninovel`), Steamworks.NET
**Условная компиляция:** `defineConstraints: ["USING_NANINOVELL"]` — сборка компилируется только с Naninovel. Обращение к Steam — под `#if USING_STEAM` внутри кода: без символа команда существует, но выполняется как no-op.

---

## Назначение

Кросс-мост **Naninovel ↔ Steam Achievements**: nani-команда разблокировки Steam-достижения по строковому ключу прямо из сценария.

Пакет отделён и от ядра Steam, и от игрового слоя: Naninovel-зависимость не втаскивается в `Vortex/Steam`, а Steam-зависимость — не в игровые скрипты. Живёт в `Vortex/NaniExtensions` (слой мостов Vortex-систем к Naninovel).

Возможности:

- `UnlockAchievementCommand` (`@UnlockSteamAchievement <key>`) — разблокирует Steam-достижение по ID. Без `USING_STEAM` — no-op (варнинг). Пустой ключ — ошибка, шаг пропускается.

Вне ответственности:

- Инициализация Steam, состояние подключения — `SteamConnectionSystem`
- Загрузка индекса достижений, сам `UnlockAchievement` — `SteamAchievements`
- Проигрывание/парсинг сценария — Naninovel

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Elringus.Naninovel.Runtime` | `Command`, `StringParameter`, `UniTask`, атрибуты параметров |
| `ru.vortex.steam.achievements` | `SteamUserData.UnlockAchievement()` (`USING_STEAM`) |
| `ru.vortex.steam.connection` | `SteamBus.User` — якорь extension-методов (`USING_STEAM`) |

> `UniTask` приходит транзитивно через Naninovel — отдельная ссылка не нужна. Ссылки на Steam-сборки не требуют символа в asmdef: без `USING_STEAM` они не собираются, ссылки игнорируются, вызовы скрыты за `#if USING_STEAM`.

---

## Архитектура

```
UnlockAchievementCommand : Command      [CommandAlias("UnlockSteamAchievement"), Preserve, Serializable]
  ├── Key: StringParameter    — безымянный обязательный параметр (Steam achievement ID)
  └── Execute(AsyncToken) → UniTask
      ├── пустой ключ  → Debug.LogError, пропуск
      ├── #if USING_STEAM  → SteamBus.User.UnlockAchievement(key)
      └── #else            → Debug.LogWarning (no-op)
```

### Почему `#if`, а не constraint на `USING_STEAM`

Nani-команда обязана **существовать всегда** (иначе Naninovel падает при парсинге строки `@UnlockSteamAchievement`). Поэтому пакет ограничен только `USING_NANINOVELL` (без Naninovel команд нет и смысла нет), а Steam-зависимость спрятана за `#if USING_STEAM`, давая безопасный no-op в не-Steam сборках. Это осознанное отличие от паттерна `SteamExtensions.Quests`/quest-логик, которые адресуются данными и могут безопасно отсутствовать.

---

## Контракт

### Вход

- `USING_NANINOVELL` определён (иначе пакета нет)
- Строка сценария: `@UnlockSteamAchievement <achievementID>`
- Для реальной разблокировки: `USING_STEAM`, `SteamBus.IsLoaded == true`, достижение настроено в Steamworks

### Выход

- Steam-достижение разблокировано; команда неблокирующая (мгновенный `UniTask.CompletedTask`)

### API

| Команда | Описание |
|---------|----------|
| `@UnlockSteamAchievement <key>` | Разблокировать достижение с ID `<key>` (безымянный обязательный параметр) |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `defineConstraints: ["USING_NANINOVELL"]` | Без Naninovel нет `Command` |
| Без `USING_STEAM` — no-op (варнинг) | Steam-сборки не собраны; вызов скрыт `#if` |
| Не проверяет наличие `key` в индексе | Гарды (`IsLoaded`, `not found`) — внутри `UnlockAchievement` |

---

## Использование

```
; в nani-сценарии
@UnlockSteamAchievement ACH_WIN_FIRST_BATTLE
```

`ACH_WIN_FIRST_BATTLE` — Steam achievement **ID** из Steamworks (App Admin → Achievements).

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Пустой ключ | `Debug.LogError`, команда пропускается |
| `USING_STEAM` не определён | `Debug.LogWarning`, no-op |
| Несуществующий ключ | `UnlockAchievement` логнёт `Achievement not found` |
| `SteamBus.IsLoaded == false` | `UnlockAchievement` — ранний return (no-op) |
| Повторный вызов для разблокированного | Идемпотентно (Steamworks) |

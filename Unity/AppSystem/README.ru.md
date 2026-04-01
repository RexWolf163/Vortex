# AppSystem (Unity)

Unity-адаптация жизненного цикла приложения.

## Назначение

Связь Unity lifecycle с шиной `App`: обработка фокуса/фона, завершение процесса, debug-настройки.

- Трансляция `OnApplicationFocus` / `OnApplicationPause` в состояния `App`
- Завершение приложения при `Stopping` (`Application.Quit` / `EditorApplication.isPlaying`)
- Вызов `App.Exit()` при разрушении `AppStateHandler`
- Debug-toggle для логирования переходов состояний

Вне ответственности: логика переходов состояний, события, модель данных — это Core (Layer 1).

## Зависимости

- `Vortex.Core.AppSystem` — шина `App`, `AppStates`
- `Vortex.Unity.EditorTools.Attributes` — `[ToggleButton]` (для `DebugSettings`)
- `UnityEngine` — `MonoBehaviour`, `Application.Quit()`

## Архитектура

```
AppStateHandler (MonoBehaviour)
├── Awake()              — подписка на App.OnStateChanged
├── OnApplicationFocus() — _pauseState → SetPauseState()
├── OnApplicationPause() — _pauseState → SetPauseState()
├── OnStateChanged()     — обновление _oldState, обработка Stopping
├── OnDestroy()          — App.Exit()
└── Start() [Editor]     — задержка _started (1с)

DebugSettingsExtApp (partial DebugSettings)
└── AppStateDebugMode    — DebugMode && appStates
```

---

## AppStateHandler

MonoBehaviour, связывающий Unity lifecycle с шиной `App`.

### Контракт

**Вход:**
- Unity lifecycle events: `OnApplicationFocus`, `OnApplicationPause`, `OnDestroy`
- Событие `App.OnStateChanged`

**Выход:**
- Вызовы `App.SetState()` при изменении фокуса/фона
- `App.Exit()` при разрушении компонента
- `Application.Quit()` при состоянии `Stopping`

### Логика

| Событие | Действие |
|---------|----------|
| `OnApplicationFocus(false)` | `App.SetState(Unfocused)` |
| `OnApplicationPause(true)` | `App.SetState(Unfocused)` |
| Возврат фокуса | `App.SetState(_oldState)` — восстановление предыдущего состояния |
| `OnStateChanged(Stopping)` | `Application.Quit()` (в редакторе: `EditorApplication.isPlaying = false`) |
| `OnStateChanged(Unfocused)` | Игнорируется — `_oldState` не обновляется |
| `OnStateChanged(другое)` | `_oldState = newState` |
| `OnDestroy` | `App.Exit()` |

### Editor-защита

Флаг `_started` выставляется через 1 секунду после `Start` (coroutine). `OnDestroy` до этого момента игнорируется — защита от ложного срабатывания при запуске с активной сценой, когда Unity может пересоздавать объекты.

---

## DebugSettingsExtApp

Partial-расширение `DebugSettings`. Добавляет toggle `appStates` с атрибутом `[ToggleButton]`.

Свойство `AppStateDebugMode` — `true` только если включены и глобальный `DebugMode`, и локальный `appStates`. Используется в `App.SetState()` для условного логирования переходов.

---

## Использование

### Настройка сцены

Разместить `AppStateHandler` на persistent GameObject (не уничтожается при смене сцен). Компонент автоматически подписывается на `App.OnStateChanged` в `Awake`.

### Debug

В ассете `DebugSettings` включить `DebugMode` (глобальный) и `appStates` (локальный). После этого все переходы состояний будут логироваться в `App.SetState()`.

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Множественные `Unfocused` подряд | `_oldState` не обновляется — восстановится последнее рабочее состояние |
| `OnDestroy` в первую секунду (Editor) | Игнорируется — `_started` ещё `false` |
| `Stopping` в Editor | `EditorApplication.isPlaying = false` вместо `Application.Quit()` |
| `OnDestroy` при обычном выходе | `App.Exit()` → `Stopping` → `OnExit` |

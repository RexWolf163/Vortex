# DebugSystem (Unity)

ScriptableObject-ассет настроек отладки с toggle-кнопками для каждой системы.

## Назначение

`DebugSettings` — `SettingsPreset` (ScriptableObject), хранящий глобальный `DebugMode` и локальные toggle для каждой системы. Partial-расширения из других пакетов добавляют свои toggle автоматически.

- Глобальный переключатель `DebugMode` (gate для всех локальных)
- Локальные toggle: `appStates`, `inputLogs`, `uiLogs`, `asyncTweenerLogs`
- Каждый локальный toggle — partial-расширение `DebugSettings` из соответствующей системы
- Итоговое свойство: `XxxDebugMode => DebugMode && xxxToggle`
- Меню `Vortex/Configs/Debug Settings` для быстрого доступа к ассету

Вне ответственности: логика логирования, Core-свойства `SettingsModel` — это Core (Layer 1).

## Зависимости

- `Vortex.Unity.SettingsSystem.Presets` — `SettingsPreset` (базовый класс)
- `Vortex.Unity.EditorTools.Attributes` — `[ToggleButton]`, `[Position]`
- `Sirenix.OdinInspector` (опционально) — `[PropertyOrder]`

## Архитектура

```
DebugSettings (partial, SettingsPreset)
├── DebugSettings.cs                          — debugMode (глобальный gate)
├── AppSystem/Debug/                          → appStates → AppStateDebugMode
├── InputBusSystem/Debug/Presets/             → inputLogs → InputDebugMode
├── UIProviderSystem/Debug/Presets/           → uiLogs → UiDebugMode
└── UI/TweenerSystem/Debug/Presets/           → asyncTweenerLogs → AsyncTweenerDebugMode

MenuController (Editor)
└── Vortex/Configs/Debug Settings             — навигация к ассету
```

### Паттерн расширения

Каждая система добавляет partial `DebugSettings` со своим toggle:

```csharp
public partial class DebugSettings
{
    [SerializeField] [ToggleButton(isSingleButton: true)] private bool myToggle;
    public bool MyDebugMode => DebugMode && myToggle;
}
```

Свойство `MyDebugMode` будет `true` только при включённых `DebugMode` И `myToggle`.

## Контракт

### Вход
- Ассет `DebugSettings` в проекте (создаётся как `SettingsPreset`)
- Значения toggle — через Inspector

### Выход
- `DebugMode` — глобальный bool
- Локальные свойства: `AppStateDebugMode`, `InputDebugMode`, `UiDebugMode`, `AsyncTweenerDebugMode`
- Все свойства доступны через `Settings.Data()` (копируются в `SettingsModel` при загрузке)

### Гарантии
- `[Position(-100)]` — `DebugMode` отрисовывается первым в Inspector
- Все локальные toggle зависят от `DebugMode` — выключение глобального отключает все

### Ограничения
- Partial-расширения разбросаны по разным пакетам — полный список toggle виден только в Inspector ассета
- Добавление нового toggle требует assembly reference на `ru.vortex.unity.debug`

## Использование

### Настройка

1. Создать ассет `DebugSettings` (или использовать существующий в `StartSettings`)
2. Включить `DebugMode` (глобальный gate)
3. Включить нужные локальные toggle

### Доступ к ассету

Меню: `Vortex → Configs → Debug Settings`

### Проверка в коде

```csharp
// Через SettingsModel (заполняется из DebugSettings)
if (Settings.Data().AppStateDebugMode)
    Log.Print(LogLevel.Common, "state changed", "App");
```

### Добавление toggle для новой системы

1. В папке системы создать `Debug/Presets/DebugSettingsExtMySystem.cs`
2. Добавить partial `DebugSettings` с toggle и свойством
3. В Core системы — partial `SettingsModel` с соответствующим свойством

## Известные расширения

| Система | Toggle | Свойство | Файл |
|---------|--------|----------|------|
| DebugSystem | `debugMode` | `DebugMode` | `DebugSystem/DebugSettings.cs` |
| AppSystem | `appStates` | `AppStateDebugMode` | `AppSystem/Debug/DebugSettingsExtApp.cs` |
| InputBusSystem | `inputLogs` | `InputDebugMode` | `InputBusSystem/Debug/Presets/DebugSettingsExtInput.cs` |
| UIProviderSystem | `uiLogs` | `UiDebugMode` | `UIProviderSystem/Debug/Presets/DebugSettingsExtUiProvider.cs` |
| TweenerSystem | `asyncTweenerLogs` | `AsyncTweenerDebugMode` | `UI/TweenerSystem/Debug/Presets/DebugSettingsExtAsyncTweener.cs` |

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `DebugMode = false` | Все локальные `XxxDebugMode` возвращают `false` |
| Ассет не создан | `Settings.Data()` не содержит debug-свойств — зависит от `SettingsSystem` |
| Новый пакет без toggle | Debug-логи этого пакета неуправляемы — нужно добавить partial |

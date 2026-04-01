# DebugSystem (Core)

Глобальный флаг debug-режима в модели настроек.

## Назначение

Partial-расширение `SettingsModel`, добавляющее свойство `DebugMode`. Используется другими системами как глобальный gate для условного логирования.

- Глобальный `DebugMode` — единый переключатель отладки
- Конвенция: каждая система добавляет свой partial с локальным флагом (`AppStateDebugMode`, `UiDebugMode` и т.д.)
- Локальные флаги зависят от глобального: `SystemDebugMode => DebugMode && localToggle`

Вне ответственности: хранение и загрузка значений флагов (это `SettingsSystem`), UI для настройки, реализация логирования.

## Зависимости

- `Vortex.Core.SettingsSystem` — `SettingsModel` (расширяемый класс)

## Архитектура

```
SettingsModel (partial)
├── DebugSystem/         → DebugMode           (глобальный gate)
├── AppSystem/Debug/     → AppStateDebugMode   (= DebugMode && appStates)
└── UIProviderSystem/    → UiDebugMode         (= DebugMode && uiLogs)
```

### Конвенция расширений

Каждая система, нуждающаяся в условном логировании, добавляет partial-расширение `SettingsModel` в своей папке `Debug/`:

```csharp
// Core: свойство с private set (заполняется через SettingsSystem)
public partial class SettingsModel
{
    public bool MySystemDebugMode { get; private set; }
}
```

На уровне Unity соответствующий partial `DebugSettings` добавляет toggle и вычисляет итоговое значение:

```csharp
// Unity: toggle + gate
public partial class DebugSettings
{
    [SerializeField] [ToggleButton(isSingleButton: true)] private bool mySystemLogs;
    public bool MySystemDebugMode => DebugMode && mySystemLogs;
}
```

## Контракт

### Вход
- Значение заполняется через `SettingsSystem` при загрузке настроек

### Выход
- `Settings.Data().DebugMode` — `bool`, глобальный флаг

### Гарантии
- Свойство доступно сразу после инициализации `Settings`
- `private set` — изменение только через `SettingsSystem`

### Ограничения
- `Settings.Data()` может быть `null` до инициализации — проверка на вызывающей стороне

## Использование

```csharp
if (Settings.Data().DebugMode)
    Log.Print(LogLevel.Common, "debug info", this);

// Или через локальный флаг системы:
if (Settings.Data().AppStateDebugMode)
    Log.Print(new LogData(LogLevel.Common, $"AppState: {state}", "App"));
```

## Известные расширения (Core)

| Система | Свойство | Файл |
|---------|----------|------|
| DebugSystem | `DebugMode` | `Core/DebugSystem/SettingsSystemExt/SettingsModelExtDebug.cs` |
| AppSystem | `AppStateDebugMode` | `Core/AppSystem/Debug/SettingsModelExtDebug.cs` |
| UIProviderSystem | `UiDebugMode` | `Core/UIProviderSystem/Debug/Model/SettingsModelExtDebug.cs` |

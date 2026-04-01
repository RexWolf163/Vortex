# SettingsSystem (Core)

**Namespace:** `Vortex.Core.SettingsSystem.Bus`, `Vortex.Core.SettingsSystem.Model`
**Сборка:** `ru.vortex.settings`
**Платформа:** .NET Standard 2.1+

---

## Назначение

Шина доступа к общим настройкам проекта. Предоставляет единую точку чтения `SettingsModel` — агрегированной модели, собираемой из нескольких источников через драйвер. Модель расширяется через `partial class` — каждая система добавляет свои свойства.

Возможности:

- Статический доступ к настройкам: `Settings.Data()`
- `SettingsModel` — расширяемая partial-модель
- Драйверная архитектура (`IDriver`) для платформозависимой загрузки

Вне ответственности:

- Хранение настроек (ScriptableObject-пресеты) — Unity-слой
- Автосоздание ассетов, Editor-меню — Unity-слой
- UI-отображение настроек

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.System` | `SystemController<T, TD>`, `SystemModel` |

---

## Архитектура

```
Settings : SystemController<Settings, IDriver>
  └── Data() → SettingsModel                 ← Driver.GetData()

IDriver : ISystemDriver
  └── GetData() → SettingsModel

SettingsModel : SystemModel  (partial)
  └── расширяется partial-классами в других системах
```

### Partial-расширение SettingsModel

`SettingsModel` — пустой partial-класс в Core. Каждая система, нуждающаяся в конфигурационных параметрах, добавляет свои свойства:

| Расширение | Свойства | Система |
|------------|----------|---------|
| `SettingsModelExtUnity` | `StartScene` | Unity SettingsSystem |
| `SettingsModelExtDebug` | `DebugMode` | Core DebugSystem |
| `SettingsModelExtDatabase` | Параметры Database | Unity DatabaseSystem |
| `SettingsModelExtAsyncTweener` | Параметры TweenerSystem | Unity TweenerSystem |
| `SettingsModelExtInput` | Параметры InputBus | Unity InputBusSystem |
| Другие | По мере необходимости | Прикладные системы |

Свойства задаются как `{ get; private set; }` — установка через `CopyFrom()` (reflection-based копирование из `SettingsPreset`).

### Механизм заполнения

Драйвер загружает набор `SettingsPreset` → для каждого вызывает `Model.CopyFrom(preset)` → `ObjectExtCopy` через Reflection копирует значения read-only свойств из пресета в одноимённые свойства модели.

---

## Контракт

### Вход

- Драйвер (`IDriver`) подключается через `Settings.SetDriver()`
- Драйвер заполняет `SettingsModel` из платформозависимых источников

### Выход

- `Settings.Data()` — возвращает `SettingsModel` с агрегированными настройками
- `null` если драйвер не подключён

### API

| Метод | Описание |
|-------|----------|
| `Settings.Data()` | Агрегированная модель настроек |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `Data()` → `null` без драйвера | `SystemController` возвращает `null` для `Driver` |
| Свойства `SettingsModel` — `private set` | Заполняются только через `CopyFrom` (Reflection) |
| Имя свойства в модели = имя свойства в пресете | `ObjectExtCopy` сопоставляет по имени |
| Порядок загрузки пресетов не гарантирован | При конфликте побеждает последний загруженный |

---

## Использование

### Добавление настроек для новой системы

1. Создать partial-расширение `SettingsModel`:

```csharp
namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        public float MusicVolume { get; private set; }
        public float SfxVolume { get; private set; }
    }
}
```

2. Создать `SettingsPreset`-наследник (Unity-слой):

```csharp
public class AudioSettings : SettingsPreset
{
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;
}
```

3. Ассет создастся автоматически в `Resources/Settings/` при загрузке Editor.

### Чтение настроек

```csharp
var startScene = Settings.Data().StartScene;
var debugMode = Settings.Data().DebugMode;
```

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Драйвер не подключён | `Settings.Data()` → `null` |
| `CopyFrom` не нашёл совпадающее свойство | Свойство остаётся `default` |
| Несколько пресетов с одинаковым свойством | Последний загруженный перезапишет значение |
| Свойство в пресете без пары в модели | Игнорируется |
| `CopyFrom` завершился ошибкой | `LogError`, возврат `false`, загрузка прерывается |

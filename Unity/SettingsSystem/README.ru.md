# SettingsSystem (Unity)

**Namespace:** `Vortex.Unity.SettingsSystem`, `Vortex.Unity.SettingsSystem.Presets`, `Vortex.Unity.SettingsSystem.Editor`
**Сборка:** `ru.vortex.unity.settings`
**Платформа:** Unity 2021.3+

---

## Назначение

Unity-слой системы настроек. Предоставляет драйвер загрузки настроек из Resources, абстрактный ScriptableObject-пресет для хранения конфигурации, автосоздание ассетов настроек при загрузке Editor и встроенный пресет стартовой сцены.

Возможности:

- `SettingsDriver` — загрузка пресетов из `Resources/Settings/`, заполнение `SettingsModel` через `CopyFrom`
- `SettingsPreset` — абстрактный `ScriptableObject` (базовый класс для пресетов настроек)
- `StartSettings` — встроенный пресет: стартовая сцена для Editor
- Автосоздание ассетов: при загрузке Editor для каждого `SettingsPreset`-наследника без ассета создаётся файл
- `StartSceneHandler` — загрузка стартовой сцены при Play Mode в Editor
- Меню `Vortex > Configs > Application Start Config` — навигация к ассету `StartSettings`

Вне ответственности:

- Шина `Settings`, модель `SettingsModel`, интерфейс `IDriver` — Core
- Логика `CopyFrom` (Reflection) — `ObjectExtCopy` в Extensions

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.SettingsSystem` | `Settings`, `SettingsModel`, `IDriver` |
| `Vortex.Core.System` | `Singleton<T>` |
| `Vortex.Core.Extensions` | `ObjectExtCopy.CopyFrom()` |
| `Vortex.Unity.FileSystem` | `File.CreateFolders()` |
| `Vortex.Unity.Extensions` | `SoData` (базовый класс), `MenuConfigSearchController` |
| Odin Inspector | `[InfoBox]`, `[ValueDropdown]` (в `StartSettings`) |

---

## Архитектура

```
SettingsDriver : Singleton<SettingsDriver>, IDriver  (partial)
  ├── Model → SettingsModel                  ← lazy, создаётся при первом обращении
  ├── Init() → LoadData()
  ├── GetData() → Model
  ├── LoadData()
  │    ├── CheckPath() → создание Resources/Settings/
  │    ├── Resources.LoadAll<SettingsPreset>("Settings")
  │    └── foreach preset → Model.CopyFrom(preset)
  ├── [RuntimeInitializeOnLoadMethod] Run()  ← Settings.SetDriver(Instance)
  ├── [InitializeOnLoadMethod] Run()         ← Editor: то же
  └── [InitializeOnLoadMethod] EditorRegister()  ← автосоздание ассетов

SettingsPreset : SoData  (abstract ScriptableObject)
  └── Свойства { get; } → копируются в SettingsModel через CopyFrom

StartSettings : SettingsPreset
  ├── startScene: string                     ← [ValueDropdown] из Build Settings
  └── StartScene → string

StartSceneHandler  (Editor, static)
  └── [RuntimeInitializeOnLoadMethod] Run()
       └── SceneManager.LoadScene(Settings.Data().StartScene)

MenuController  (Editor, static)
  └── [MenuItem("Vortex/Configs/Application Start Config")]
       └── навигация к ассету StartSettings
```

### Загрузка настроек

1. `SettingsDriver.Run()` вызывается через `[RuntimeInitializeOnLoadMethod]` и `[InitializeOnLoadMethod]`
2. `Settings.SetDriver(Instance)` — регистрация драйвера в Core-шине
3. При первом обращении к `Model` — создание `SettingsModel`, вызов `LoadData()`
4. `LoadData()` загружает все `SettingsPreset` из `Resources/Settings/`
5. Для каждого пресета — `Model.CopyFrom(preset)`: Reflection копирует read-only свойства по имени

### Автосоздание ассетов (Editor)

`EditorRegister()` при `[InitializeOnLoadMethod]`:

1. Создание папки `Resources/Settings/` если не существует
2. Поиск всех наследников `SettingsPreset` через Reflection по всем сборкам
3. Для каждого типа без существующего ассета — `ScriptableObject.CreateInstance` + `AssetDatabase.CreateAsset`
4. Лог: `"Create new settings preset {TypeName}"`

### StartSceneHandler (Editor)

При Play Mode в Editor загружает сцену из `Settings.Data().StartScene`. Позволяет запускать проект с любой сцены, не меняя Build Settings. Работает только в Editor (`#if UNITY_EDITOR`).

---

## Контракт

### Вход

- Наследники `SettingsPreset` создаются как ScriptableObject в `Resources/Settings/`
- Ассеты создаются автоматически при загрузке Editor
- Значения настраиваются в Inspector

### Выход

- `Settings.Data()` — `SettingsModel` с агрегированными данными из всех пресетов
- `SettingsDriver.OnInit` — событие после загрузки всех пресетов

### API

| Компонент | Назначение |
|-----------|-----------|
| `SettingsPreset` | Абстрактный базовый класс для пресетов настроек |
| `StartSettings` | Встроенный пресет стартовой сцены |
| `SettingsDriver.OnInit` | Событие завершения загрузки настроек |

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Загрузка из `Resources/Settings/` | `Resources.LoadAll<SettingsPreset>("Settings")` |
| Автосоздание только в Editor | `[InitializeOnLoadMethod]` + `AssetDatabase` |
| `StartSceneHandler` только в Editor | `#if UNITY_EDITOR` |
| Один ассет на тип `SettingsPreset` | `EditorRegister` проверяет `resources.Contains(type)` |
| Порядок загрузки не определён | `Resources.LoadAll` не гарантирует порядок |

---

## Использование

### Создание нового пресета настроек

1. Создать partial-расширение `SettingsModel` (Core):

```csharp
namespace Vortex.Core.SettingsSystem.Model
{
    public partial class SettingsModel
    {
        public int MaxPlayers { get; private set; }
    }
}
```

2. Создать наследник `SettingsPreset` (Unity):

```csharp
public class GameplaySettings : SettingsPreset
{
    [SerializeField] private int maxPlayers = 4;
    public int MaxPlayers => maxPlayers;
}
```

3. Перезагрузить Editor — ассет `GameplaySettings.asset` создастся автоматически в `Resources/Settings/`
4. Настроить значения в Inspector

### Навигация к настройкам

`Menu: Vortex > Configs > Application Start Config` — открывает ассет `StartSettings` в Inspector.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Нет ассета для типа `SettingsPreset` | Автосоздание при загрузке Editor |
| Папка `Resources/Settings/` не существует | Автосоздание через `File.CreateFolders` |
| Драйвер уже установлен (повторный `SetDriver`) | Предупреждение в лог, `Dispose()` нового экземпляра |
| `CopyFrom` не нашёл свойство | Свойство модели остаётся `default` |
| `StartScene` пустой | `SceneManager.LoadScene("")` — ошибка Unity |
| Ассет удалён вручную | Пересоздастся при следующей загрузке Editor |
| Несколько пресетов с одинаковым свойством | Последний из `LoadAll` перезапишет значение |

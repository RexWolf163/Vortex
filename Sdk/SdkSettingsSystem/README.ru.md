# SdkSettingsSystem

**Namespace:** `Vortex.Sdk.SdkSettingsSystem`
**Assembly:** `sdk.settings.system`

## Назначение

Единая точка управления `Scripting Define Symbols` для подключаемых SDK-пакетов Vortex. Собирает bool-тогглы из всех Sdk-пакетов в один ScriptableObject-ассет и синхронизирует их с PlayerSettings проекта.

Возможности:
- Один инспектор-ассет с тогглами включения/выключения каждого SDK-пакета
- Авто-синхронизация дефайнов при загрузке домена и смене целевой платформы
- Двунаправленный режим: «применить состояние тогглов в PlayerSettings» и «вычитать текущие defines обратно в тогглы»
- Расширение через `partial`+`asmref` — новый пакет добавляет один файл, без правок в центре

Вне ответственности:
- Сами `defineConstraints` отдельных пакетов (живут в их asmdef)
- Условная компиляция (это работа компилятора по результату sync)
- Очистка устаревших defines при удалении пакета

## Зависимости

- `Vortex.Unity.CoreAssetsSystem` — `ICoreAsset`
- `Vortex.Unity.EditorTools` — атрибут `[ToggleButton]`
- Sirenix Odin Inspector — `[Button]`, `[HorizontalGroup]`
- `UnityEditor` (Editor-only) — `PlayerSettings`, `EditorUserBuildSettings`, `AssetDatabase`

## Архитектура

```
sdk.settings.system  (asmdef)
├── SdkSettings.cs            ← базовый partial, reflection-движок
├── Attribute/
│   └── DefineSymbolAttribute.cs
└── Editor/
    └── MenuController.cs     ← пункт меню Vortex/Configs/SDK Settings

<package>/DefineSettings/
├── SdkSettings.<Package>.cs  ← partial-кусок с bool-полем
└── sdk.settings.system.ext.asmref → sdk.settings.system
```

`asmref` гарантирует, что partial-куски физически собираются в одну сборку с базовым `SdkSettings`. Без этого partial не работает (требование C#).

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `SdkSettings` | `partial ScriptableObject`, `ICoreAsset` | Главный ассет, partial-кусками собирает поля из всех Sdk-пакетов |
| `DefineSymbolAttribute` | `Attribute` | Метка bool-поля: с каким `define` в PlayerSettings оно связано |
| `MenuController` | static, Editor-only | Пункт меню `Vortex/Configs/SDK Settings` для перехода к ассету |

## Как добавить новый Sdk-пакет

1. В корне нового пакета создать папку `DefineSettings/`.
2. Положить туда два файла:

   **`SdkSettings.<PackageName>.cs`:**
   ```csharp
   using UnityEngine;
   using Vortex.Sdk.SdkSettingsSystem.Attribute;
   using Vortex.Unity.EditorTools.Attributes;

   namespace Vortex.Sdk.SdkSettingsSystem
   {
       public partial class SdkSettings
       {
           [SerializeField, ToggleButton(isSingleButton: true)]
           [DefineSymbol("USING_VORTEX_MYPACKAGE")]
           private bool myPackageSdk = true;
       }
   }
   ```

   **`sdk.settings.system.ext.asmref`:**
   ```json
   { "reference": "GUID:56735331757685c47ba846af366ea373" }
   ```
   (GUID соответствует `sdk.settings.system.asmdef`.)

3. В asmdef нового пакета указать `defineConstraints: ["USING_VORTEX_MYPACKAGE"]` — пакет начнёт компилироваться только при включённом тоггле.

После пересборки проекта в инспекторе `SdkSettings` появится новый тогглер.

## Контракт

### Вход
- bool-поля в `partial SdkSettings`, помеченные `[DefineSymbol("KEY")]`

### Выход
- `PlayerSettings.SetScriptingDefineSymbols` — синхронизированный набор defines для текущей `BuildTargetGroup`

### Гарантии
- Каждое включённое поле → соответствующий define присутствует в PlayerSettings
- Каждое выключенное поле → соответствующий define отсутствует
- Запись в PlayerSettings выполняется только если итоговый набор отличается от текущего (идемпотентность)
- На дубликат `[DefineSymbol("X")]` — `LogError` и пропуск второго

### Ограничения
- Один ассет `SdkSettings` на проект (при множественных — `LogError` + abort)
- Все partial-куски обязаны лежать в asmref'ах на `sdk.settings.system`
- Editor-only: в рантайм-сборку движок не попадает

## Жизненный цикл

| Триггер | Что происходит |
|---------|---------------|
| Первый запуск после создания ассета (через `CoreAssetsController`) | `OnPlatformChanged` → ассет с `_initialized = false` → `ReloadStates` (тогглы выставляются по уже существующим defines), затем `_initialized = true` |
| `[InitializeOnLoadMethod]` (загрузка домена) на «прогретом» ассете | `OnPlatformChanged` → `RefreshDefines` |
| `EditorUserBuildSettings.activeBuildTargetChanged` | То же — `RefreshDefines` |
| Кнопка `ApplyChanges` в инспекторе | `RefreshDefines` — bool-поля → PlayerSettings |
| Кнопка `ReloadStates` в инспекторе | Чтение PlayerSettings → bool-поля + `SetDirty` + `SaveAssets` |

### Зачем `_initialized`-флаг

При свеже-созданном ассете все bool-поля имеют дефолтные значения (обычно `true`). Без флага первый же `RefreshDefines` затёр бы существующий набор defines в PlayerSettings — например, если в проекте до подключения Vortex уже были собственные defines с пересекающимися именами или если пакет создан в проекте с уже проставленными defines из git. Поэтому первый прогон — `ReloadStates` (читаем то, что уже есть в PlayerSettings, в тогглы), и только потом начинается обычная синхронизация.

Флаг сериализуется в ассете (скрыт в инспекторе через `[HideInInspector]`).

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Ассет `SdkSettings` отсутствует в проекте | Тихий no-op (`OnPlatformChanged` ничего не делает) |
| Найдено более одного ассета | `LogError` и abort |
| Два partial-куска объявляют один и тот же `[DefineSymbol("X")]` | `LogError` про дубликат, в карту попадает первый |
| Поле помечено `[DefineSymbol]`, но не `bool` | Игнорируется |
| `BuildTargetGroup.Unknown` | Тихий выход |
| Ошибка `SaveAssets` в `ReloadStates` | Заглушено (`try/catch{}`) — известное упрощение |
| Удалён весь пакет (исчез partial-кусок) | Поле пропадает из инспектора, define в PlayerSettings остаётся (zombie-define) |

## Меню

`Vortex → Configs → SDK Settings` — поиск ассета `SdkSettings` в проекте и переход к нему через `MenuConfigSearchController`.

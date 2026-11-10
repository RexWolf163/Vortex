# SdkSettingsSystem

**Namespace:** `Vortex.Sdk.SdkSettingsSystem`
**Assembly:** `sdk.settings.system`

## Назначение

Единая точка управления `Scripting Define Symbols` для подключаемых SDK-пакетов Vortex. Собирает bool-тогглы из всех Sdk-пакетов в один ScriptableObject-ассет и синхронизирует их с PlayerSettings проекта.

Возможности:
- Один инспектор-ассет с тогглами включения/выключения каждого SDK-пакета
- Авто-синхронизация дефайнов при загрузке домена, смене целевой платформы и импорте самого ассета (подмена файла AssetSwap, `git checkout/pull`)
- Немедленная синхронизация из кода для скриптов сборки и CI — `SdkSettings.SyncDefinesNow()` (см. [Синхронизация из скрипта сборки](#синхронизация-из-скрипта-сборки))
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
    ├── MenuController.cs     ← пункт меню Tools/Vortex/Configs/SDK Settings
    └── SdkSettingsImportWatcher.cs ← AssetPostprocessor: ассет импортирован → синхронизация

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
| `MenuController` | static, Editor-only | Пункт меню `Tools/Vortex/Configs/SDK Settings` для перехода к ассету |
| `SdkSettingsImportWatcher` | `AssetPostprocessor`, Editor-only | Импорт действующего ассета → отложенная синхронизация |

Публичное Editor-API: `SdkSettings.SyncDefinesNow()` — немедленная синхронизация для скриптов сборки.

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
- `SyncDefinesNow()` → `bool` — выполнена ли синхронизация (совпадение без записи — тоже `true`)

### Гарантии
- Каждое включённое поле → соответствующий define присутствует в PlayerSettings
- Каждое выключенное поле → соответствующий define отсутствует
- Запись в PlayerSettings выполняется только если итоговый набор отличается от текущего (идемпотентность)
- На дубликат `[DefineSymbol("X")]` — `LogError` и пропуск второго

### Ограничения
- Один ассет `SdkSettings` на проект вне `Editor`-папок (при множественных — `LogError` со списком путей + abort). Ассеты в `Editor`-папках не учитываются: это заготовки (напр. варианты AssetSwap), в сборку не попадают и дефайнами не управляют
- Все partial-куски обязаны лежать в asmref'ах на `sdk.settings.system`
- Editor-only: в рантайм-сборку движок не попадает

## Жизненный цикл

| Триггер | Что происходит |
|---------|---------------|
| Первый запуск после создания ассета (через `CoreAssetsController`) | `OnPlatformChanged` → ассет с `_initialized = false` → `ReloadStates` (тогглы выставляются по уже существующим defines), затем `_initialized = true` |
| `[InitializeOnLoadMethod]` (загрузка домена) на «прогретом» ассете | `OnPlatformChanged` → `RefreshDefines` |
| `EditorUserBuildSettings.activeBuildTargetChanged` | То же — `RefreshDefines` |
| Импорт ассета `SdkSettings` вне `Editor`-папок (`SdkSettingsImportWatcher`) | `delayCall` → `OnPlatformChanged` → `RefreshDefines`. Покрывает смену ассета без перезагрузки домена: применение варианта AssetSwap, `git checkout/pull`, внешняя правка. Перекомпиляция скриптов не срабатывает (импортируются только `.cs`); повтор безвреден — запись в PlayerSettings только при изменении набора дефайнов |
| Кнопка `ApplyChanges` в инспекторе | `RefreshDefines` — bool-поля → PlayerSettings |
| Вызов `SdkSettings.SyncDefinesNow()` (скрипт сборки, CI) | Сразу, без `delayCall`: `RefreshDefines` действующего ассета. Отказ (с ошибкой в лог) — во время сборки/импорта/компиляции, без ассета или при неинициализированном ассете |
| Кнопка `ReloadStates` в инспекторе | Чтение PlayerSettings → bool-поля + `SetDirty` + `SaveAssets` |

### Зачем `_initialized`-флаг

При свеже-созданном ассете все bool-поля имеют дефолтные значения (обычно `true`). Без флага первый же `RefreshDefines` затёр бы существующий набор defines в PlayerSettings — например, если в проекте до подключения Vortex уже были собственные defines с пересекающимися именами или если пакет создан в проекте с уже проставленными defines из git. Поэтому первый прогон — `ReloadStates` (читаем то, что уже есть в PlayerSettings, в тогглы), и только потом начинается обычная синхронизация.

Флаг сериализуется в ассете (скрыт в инспекторе через `[HideInInspector]`).

### Синхронизация из скрипта сборки

Автоматические точки синхронизации отложены (`EditorApplication.delayCall`): они ждут конца импорта/компиляции. Внутри одного `-executeMethod` отложенный вызов не успевает до `BuildPipeline.BuildPlayer`, а с `-quit` может не выполниться вовсе — сборка уйдёт со старыми дефайнами. Поэтому скрипт сборки, который меняет ассет `SdkSettings` (напр. применяет вариант AssetSwap) или просто хочет гарантии, синхронизирует явно:

```csharp
public static void BuildNutaku()
{
    if (!AssetSwapController.ApplyByComment("nutaku"))   // подменили SdkSettings.asset (и не только)
        EditorApplication.Exit(1);

    if (!SdkSettings.SyncDefinesNow())                     // дефайны → PlayerSettings прямо сейчас
        EditorApplication.Exit(1);

    BuildPipeline.BuildPlayer(...);
}
```

`SyncDefinesNow()`:

| Результат | Когда |
|---|---|
| `true` | Дефайны записаны или уже совпадали с галочками |
| `false` + ошибка в лог | Идёт сборка (`BuildPipeline.isBuildingPlayer`), импорт или компиляция |
| | Действующего ассета нет или их несколько (вне `Editor`-папок) |
| | Ассет не инициализирован (`_initialized = false`): первый прогон прочитал бы текущие дефайны в ассет, а не наоборот — для сборки это неверное направление |
| | Неизвестная платформа или ошибка записи PlayerSettings |

Особенности:
- **Платформа.** Пишется набор для `EditorUserBuildSettings.selectedBuildTargetGroup`. В batchmode запускайте Unity с `-buildTarget`, совпадающим с целью сборки, иначе дефайны уйдут другой платформе.
- **Перекомпиляция не нужна.** Плеерная сборка берёт дефайны из PlayerSettings в момент сборки. Редакторские сборки в этом процессе остаются скомпилированными со старыми дефайнами — это касается только редакторского кода под условием (build-хуки и т.п.).
- **Повтор безвреден.** Позже сработает и отложенная синхронизация от импорта ассета — она увидит совпадение и ничего не запишет.

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Ассет `SdkSettings` отсутствует в проекте | Тихий no-op (`OnPlatformChanged` ничего не делает) |
| Найдено более одного ассета вне `Editor`-папок | `LogError` со списком путей и abort |
| Копии ассета в `Editor`-папках (варианты AssetSwap) | Поиском и наблюдателем импорта игнорируются. Кнопки инспектора у них работают: `ApplyChanges` на копии применит **её** тогглы к дефайнам проекта, `ReloadStates` перезапишет копию текущими дефайнами — копии правьте только тогглами |
| Два partial-куска объявляют один и тот же `[DefineSymbol("X")]` | `LogError` про дубликат, в карту попадает первый |
| Поле помечено `[DefineSymbol]`, но не `bool` | Игнорируется |
| `BuildTargetGroup.Unknown` | Тихий выход |
| Ошибка `SaveAssets` в `ReloadStates` | Заглушено (`try/catch{}`) — известное упрощение |
| Удалён весь пакет (исчез partial-кусок) | Поле пропадает из инспектора, define в PlayerSettings остаётся (zombie-define) |

## Меню

`Tools → Vortex → Configs → SDK Settings` — поиск ассета `SdkSettings` в проекте и переход к нему через `MenuConfigSearchController`.

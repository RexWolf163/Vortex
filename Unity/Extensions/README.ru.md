# Extensions (Unity)

**Namespace:** `Vortex.Unity.Extensions.Abstractions`, `Vortex.Core.Extensions.LogicExtensions` (partial), `Vortex.Unity.Extensions.Editor`
**Сборка:** `ru.vortex.unity.extensions`

## Назначение

Unity-расширения ядра: абстракции для MonoBehaviour-синглтонов, конвертация текстур, маркировка платформенных типов для DeepCopy, редакторные утилиты (поиск ассетов, управление define-символами, кодогенерация из шаблонов, Odin-dropdown).

- `MonoBehaviourSingleton<T>` — синглтон на базе `MonoBehaviour`
- `SoData` / `SoDataController` — абстракция ScriptableObject с рефлексией свойств
- `TextureExtBase64` — конвертация `Texture2D` ↔ Base64 с GZip-сжатием
- `SimpleTypeMarkerExtUnity` — partial-расширение маркера платформенных типов
- `DefineSymbolManager` — автоматическое управление define-символами по наличию пакетов
- VTP Template System — генерация и развёртывание `.vtp` шаблонов кода

Вне ответственности: бизнес-логика, UI-компоненты, системные драйверы.

## Зависимости

- `Vortex.Core.Extensions` — `SimpleTypeMarker` (partial), `IsNullOrWhitespace`
- `Vortex.Unity.AppSystem` — `TimeController` (отложенные вызовы в `MonoBehaviourSingleton`)
- `Sirenix.OdinInspector` — `[Button]`, `[InfoBox]`, `[TabGroup]`, `ValueDropdownList` (Editor)

---

## Abstractions

### MonoBehaviourSingleton\<T\>

```
MonoBehaviourSingleton<T> : MonoBehaviour
  where T : MonoBehaviourSingleton<T>

  ├── Instance: T (static, protected)     ← lazy через FindAnyObjectByType
  ├── Awake()                             ← SetInstance (Editor: через TimeController.Call(0))
  └── OnDestroy()                         ← _instance = null, TimeController.RemoveCall
```

Синглтон на базе `MonoBehaviour`. При `Awake` регистрирует себя как единственный экземпляр. При повторном создании — `LogError`. При обращении к `Instance` до `Awake` — fallback на `FindAnyObjectByType<T>()`.

В редакторе `SetInstance` выполняется через `TimeController.Call(0)` — отложенный вызов на следующий кадр. Это защита от рассинхрона при горячем рестарте Play Mode.

### SoData

Абстрактный `ScriptableObject`. В редакторе предоставляет кнопку `TestFields` — выводит в консоль список read-only публичных свойств, пригодных для автоматического копирования в `SystemModel`.

### SoDataController

Статический класс-расширение для `SoData`.

| Метод | Описание |
|-------|----------|
| `GetPropertiesList()` | Возвращает `PropertyInfo[]` — публичные свойства без сеттера |
| `PrintFields()` | Editor-only: лог списка свойств в консоль |

---

## CoreExt

### TextureExtBase64

Конвертация `Texture2D` ↔ Base64-строка с опциональным GZip-сжатием.

**Namespace:** `Vortex.Core.Extensions.LogicExtensions` (partial-расширение Core-пакета).

#### API

| Метод | Описание |
|-------|----------|
| `texture.TextureToBase64(encodingRules, compress)` | Кодирование текстуры в Base64-строку |
| `texture.Base64ToTexture(base64)` | Восстановление текстуры из Base64-строки |

**Параметры `TextureToBase64`:**
- `encodingRules` — формат: `PNG`, `JPEGLow` (25%), `JPEGMedium` (50%), `JPEGHigh` (75%), `JPEGMax` (100%). Default: `PNG`
- `compress` — GZip-сжатие `byte[]` перед конвертацией в Base64. Default: `false`

**`Base64ToTexture`** автоматически определяет GZip по magic bytes (`0x1F 0x8B`) — явный параметр декомпрессии не нужен.

#### Использование

```csharp
// Без сжатия
var base64 = texture.TextureToBase64(TextureEncodingRules.JPEGHigh);
targetTexture.Base64ToTexture(base64);

// Со сжатием
var base64 = texture.TextureToBase64(TextureEncodingRules.JPEGHigh, compress: true);
targetTexture.Base64ToTexture(base64); // автодетект GZip
```

#### Граничные случаи

- `texture == null` → `ArgumentNullException`
- `base64` пустой или `null` → лог ошибки, return `false`
- `LoadImage` не распознал формат → лог ошибки, return `false`
- PNG уже сжат — GZip поверх даёт минимальный выигрыш; для JPEG выигрыш заметнее
- Ошибки перехватываются `try/catch` с `Debug.LogError`, без выброса исключений наружу

### TextureEncodingRules

```csharp
enum TextureEncodingRules { PNG, JPEGLow, JPEGMedium, JPEGHigh, JPEGMax }
```

### SimpleTypeMarkerExtUnity

Partial-расширение `SimpleTypeMarker` из Core. Добавляет `UnityEngine.Object` как платформенный примитив — все наследники (`GameObject`, `Sprite`, `Material`, ...) не клонируются в `DeepCopy`, а передаются по ссылке.

---

## Editor

### AssetFinder

Утилита поиска ассетов через `AssetDatabase`.

| Метод | Описание |
|-------|----------|
| `FindAssets<T>(params string[] searchInFolders)` | Все ассеты типа `T` в проекте (или в указанных папках) |
| `FindAsset<T>(params string[] searchInFolders)` | Первый ассет типа `T` или `null` |

### MenuConfigSearchController

Утилита навигации к ScriptableObject-ассету: `Selection.activeObject` + `PingObject`. Используется в пунктах меню `Vortex/Configs/*`.

### DefineSymbolManager

Автоматическое управление define-символами компиляции по наличию пакетов в проекте.

#### Архитектура

```
DefineSymbolManager (static, Editor-only)
  ├── [InitializeOnLoadMethod] Run()        ← подписка на Events.registeringPackages
  ├── [DidReloadScripts] OnScriptsReloaded  ← пересканирование
  └── UpdateDefineSymbols()                 ← Client.List → INeedPackage → PlayerSettings

INeedPackage (interface)
  ├── GetPackageName()   → "com.unity.addressables"
  └── GetDefineString()  → "ENABLE_ADDRESSABLES"

AddressablesPreBuildProcessor (IPreprocessBuildWithReport)
  └── OnPreprocessBuild() → UpdateDefineSymbols() + ожидание завершения (до 2 сек)
```

Сканирует все реализации `INeedPackage` через рефлексию. Для каждой проверяет наличие пакета в `Client.List()`. Если пакет есть — добавляет define-символ; если нет — удаляет. Применяется к текущей и Standalone платформам.

#### INeedPackage

Интерфейс для объявления зависимости от пакета:

```csharp
public class MyPackageDependency : INeedPackage
{
    public string GetPackageName() => "com.unity.addressables";
    public string GetDefineString() => "ENABLE_ADDRESSABLES";
}
```

### RichTextHelpBox

`EditorGUI.HelpBox` с поддержкой Rich Text (`<b>`, `<color>`, `<i>`).

| Метод | Описание |
|-------|----------|
| `Create(Rect, string, MessageType)` | HelpBox в указанном Rect |
| `Create(string, MessageType, int height)` | HelpBox через `EditorGUILayout` |

### OdinDropdownTool

Обёртка над `OdinSelector<T>` для рисования dropdown-полей в кастомных Inspector'ах. Требует Odin Inspector.

| Метод | Описание |
|-------|----------|
| `DropdownSelector<T>(value, items)` | Dropdown из `IEnumerable<T>` |
| `DropdownSelector<T>(label, value, dropItems, out rect)` | Dropdown из `ValueDropdownList<T>` с label и выходным Rect |

### DropDawnHandler

Утилиты для формирования `ValueDropdownList` из рефлексии.

| Метод | Описание |
|-------|----------|
| `GetTypesNameList<T>()` | Все типы, реализующие `T` (интерфейс или класс), как `ValueDropdownList<string>` по `AssemblyQualifiedName` |
| `GetScenes()` | Сцены из Build Settings как `ValueDropdownList<string>` |

---

## Editor/Templates — VTP Template System

Система кодогенерации из текстовых шаблонов `.vtp`.

### Формат .vtp

Текстовый файл UTF-8. Файлы разделяются маркерами:

```
//---{путь/ИмяФайла.cs}---
содержимое файла...

//---{другой/Файл.cs}---
содержимое...
```

Плейсхолдеры: `{!Key!}` — заменяются значениями из словаря подстановок.

### VtpTemplateGenerator

| Метод | Описание |
|-------|----------|
| `Generate(templatePath, outputFolder, substitutions)` | Развёртывание `.vtp` шаблона в файлы с подстановками |
| `CreateTemplateFromFolder(sourceFolder, outputPath, replacements)` | Создание `.vtp` шаблона из папки с `.cs` файлами |

### VtpTemplateCreatorWindow

Editor-окно для создания `.vtp` шаблона из папки. Меню: **Assets → Vortex → Template Generator**.

- Выбор исходной папки
- Настройка обратных подстановок (значение → плейсхолдер)
- Генерация `.vtp` + `TemplateMenu.cs` (скрипт контекстного меню)

### VtpGeneratorWindow

Editor-окно для развёртывания шаблона. Ввод имени класса → подстановка `{!ClassName!}` → генерация файлов.

### Контекстные меню шаблонов

Каждый шаблон получает автогенерированный `*TemplateMenu.cs` с пунктом меню **Assets → Create → Vortex Templates → {Имя}**.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Два `MonoBehaviourSingleton<T>` на сцене | Второй логирует `LogError`, перезаписывает `_instance` |
| `Instance` до `Awake` | Fallback на `FindAnyObjectByType` |
| `TextureToBase64` с `compress: true` для PNG | Работает, но выигрыш минимален (PNG уже сжат) |
| `Base64ToTexture` с несжатыми данными | GZip-автодетект не срабатывает, данные передаются в `LoadImage` как есть |
| `DefineSymbolManager` — пакет удалён | Define-символ автоматически удаляется при следующем сканировании |
| `DefineSymbolManager` — нет реализаций `INeedPackage` | Ничего не происходит |
| VTP шаблон без маркеров `//---{...}---` | Предупреждение в консоль, файлы не создаются |
| VTP плейсхолдер `{!Key!}` отсутствует в словаре | Предупреждение в консоль, плейсхолдер остаётся как есть |

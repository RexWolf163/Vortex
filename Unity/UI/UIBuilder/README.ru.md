# UIBuilder

**Namespace:** `Vortex.Unity.UI.UIBuilder`
**Сборка:** `ru.vortex.unity.ui.uibuilder` (только Editor)

## Назначение

Editor-инструмент вёрстки: создаёт под выбранным объектом готовый UI-слой из префаба-примитива за одно действие — контейнер `UIComponent`, экземпляр примитива внутри, собранные части и нужные `Set*Component` со связями.

Возможности:
- Каталог примитивов по папке (с подпапками), выбор из выпадающего списка с группировкой по подпапкам
- Контекстное меню Hierarchy: `Vortex Primitives/Create Text`, `Vortex Primitives/Create Button`
- Параметры окна зависят от состава выбранного примитива: секция показывается, только если в нём есть нужная часть
- Настройки модулей на странице `Project Settings → Vortex/UIBuilder`, файл вне `Assets` — в сборку не попадает
- Одна запись Undo на всё создание; ошибка сборки откатывает созданное
- Расширение новыми видами элементов и секциями без правки ядра

Вне ответственности:
- Сами примитивы и их структура (проектные префабы)
- Runtime-поведение `UIComponent` и `Set*Component` (`UIComponents`, `Components`)

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `ru.vortex.unity.ui.misc` | `UIComponent` и его части (`UIComponentText/Graphic/Button`) |
| `ui.vortex.unity.components` | `SetTextComponent`, `SetSpriteComponent`, `SetActionComponent` |
| `ru.vortex.unity.localization` | `[LocalizationKey]` — селектор ключа в секции локали |
| `ru.vortex.unity.editortools` | `SearchablePopup` — выпадающий список примитивов |
| Odin Inspector | Отрисовка настроек и секций (`PropertyTree`, `[FolderPath]`, `[ValueDropdown]`) |

---

## Архитектура

```
UIBuilder/
├── UIBuilderController.cs          # Ядро: поиск модулей, пункт меню → окно, сборка слоя
├── UIBuilderSettings.cs            # ScriptableSingleton в ProjectSettings/, синхронизация с модулями
├── UIBuilderSettingsProvider.cs    # Страница Project Settings → Vortex/UIBuilder
├── UIBuilderCreateWindow.cs        # Окно параметров создания
├── UIBuilderCatalog.cs             # Каталог префабов папки (с подпапками)
├── Shortcuts/                      # Горячие клавиши вёрстки (общий код — EditorTools/HierarchyTools)
│   ├── BackgroundLayerShortcut.cs  # Alt+I — слой Background с Image
│   ├── TweenerHubShortcut.cs       # Alt+T / Ctrl+Alt+T — TweenerHub на объект / слоем
│   └── UIStateSwitcherShortcut.cs  # Alt+S / Ctrl+Alt+S — UIStateSwitcher на объект / слоем
├── Base/
│   ├── UIBuilderModule.cs          # Модуль вида элемента (+ generic UIBuilderModule<TSettings>)
│   ├── UIBuilderModuleSettings.cs  # Общие настройки модуля
│   └── UIBuilderSection.cs         # Секция параметров создания
├── Modules/
│   ├── TextModule.cs               # Text + TextModuleSettings
│   └── ButtonModule.cs             # Button + ButtonModuleSettings
└── Sections/
    ├── LocaleSection.cs            # UIComponentText → SetTextComponent
    ├── IconSection.cs              # UIComponentGraphic → SetSpriteComponent
    └── ActionSection.cs            # UIComponentButton → SetActionComponent
```

### Модуль (`UIBuilderModule`)

Вид элемента: свои настройки (`SettingsType`), название (`Title`), набор секций (`CreateSections()`) и шаг сборки (`Apply`). Состояния нет, нужен конструктор без параметров. Ядро находит неабстрактных наследников через `TypeCache`. Пункт меню модуль объявляет сам — статическим `[MenuItem]`, который вызывает `UIBuilderController.Open<TModule>`.

| Модуль | Меню | Секции | Имя слоя по умолчанию |
|--------|------|--------|-----------------------|
| `TextModule` | `Vortex Primitives/Create Text` | Локаль | `Text` |
| `ButtonModule` | `Vortex Primitives/Create Button` | Локаль, Иконка, Действие | `Button` |

### Настройки (`UIBuilderSettings`, `UIBuilderModuleSettings`)

`UIBuilderSettings` — `ScriptableSingleton`, файл `ProjectSettings/VortexUIBuilderSettings.asset`: вне `Assets`, в сборку не попадает, хранится в VCS. Содержит по одному `UIBuilderModuleSettings` на каждый модуль (`[SerializeReference]`).

Общие поля настроек модуля:

| Поле | Тип | Описание |
|------|-----|----------|
| `folder` | `string` (`[FolderPath]`) | Папка примитивов, сканируется с подпапками. Пусто — модуль не настроен |
| `defaultPrefab` | `GameObject` (`[ValueDropdown]`) | Примитив, выбранный при открытии окна. Выбирается из каталога папки |
| `defaultLayerName` | `string` | Имя создаваемого слоя. Значение по умолчанию задаёт наследник через конструктор |
| `defaultSize` | `Vector2` | Размер создаваемого слоя (`sizeDelta`), по умолчанию `240 × 80` |

Кроме настроек модулей, на странице есть блок **Background Layer (Alt+I)**: `backgroundColor` — цвет `Image` слоя, создаваемого `Add BackgroundLayer`, по умолчанию чёрный.

**Синхронизация** (`Sync`) — при открытии страницы настроек и при обращении к настройкам отсутствующего модуля:
- добавляет настройки для модулей, у которых их нет;
- удаляет записи, чей класс не найден, настройки модулей, которых больше нет, и дубликаты (остаётся первый);
- каждое удаление пишется в лог, изменения сохраняются на диск.

### Секция (`UIBuilderSection`)

Общий кусок параметров создания, привязанный к типу части `UIComponent` (`PartType`). Поля секции (`[SerializeField]`) рисует Odin. Экземпляры создаются заново при каждом выборе примитива и не сохраняются.

| Секция | Часть | Поля | Добавляет |
|--------|-------|------|-----------|
| `LocaleSection` | `UIComponentText` | `localeKey` (`[LocalizationKey]`), `useLocalization = false` | `SetTextComponent` с ключом, если ключ задан |
| `IconSection` | `UIComponentGraphic` | `icon` | `SetSpriteComponent` со спрайтом, если спрайт задан |
| `ActionSection` | `UIComponentButton` | `addAction = true` | `SetActionComponent`, если галочка стоит |

`AddLinked<T>` добавляет компонент и явно проставляет `uiComponent` и `position = -1` (все части типа); собственные поля дописываются через `SerializedObject`.

### Сборка слоя

```
<активный выделенный объект>
└── <имя слоя>          RectTransform (sizeDelta = defaultSize), UIComponent, Set*Component
    └── <экземпляр примитива>   связь с префабом, без изменений
```

1. Группа Undo.
2. Слой создаётся **неактивным** — `SetTextComponent` (`[ExecuteInEditMode]`) не должен включиться без ссылки на `UIComponent`.
3. Экземпляр примитива через `PrefabUtility.InstantiatePrefab`.
4. Сбор частей — приватный editor-метод `UIComponent.Init` через рефлексию (пакет `UIComponents` не меняется).
5. `module.Apply`: секции, у которых после сбора есть части нужного типа.
6. Слой активируется, группа Undo сворачивается, созданный слой выделяется.

---

## Использование

1. `Project Settings → Vortex/UIBuilder` — указать папку примитивов для каждого модуля, при желании префаб, имя и размер по умолчанию.
2. ПКМ по объекту в Hierarchy → `Vortex Primitives/Create Text` или `Create Button`.
3. В окне выбрать примитив, имя слоя, заполнить секции → «Создать».

## Горячая клавиша Add BackgroundLayer

`Alt+I` (`Tools/Vortex/UI/Add BackgroundLayer`) — в каждый выделенный объект сцены или открытого префаба добавляется дочерний слой `Background` с `Image`:
- цвет — `backgroundColor` из настроек (по умолчанию чёрный); Maskable выключен, Raycast Target включён;
- слой первым в иерархии (рисуется под остальными детьми), `RectTransform` растянут по родителю;
- повторное нажатие — ещё один слой; новые слои выделяются; отмена — одним шагом Undo;
- ассеты в Project window не затрагиваются. У родителя без `RectTransform` растягивать не по чему.

Здесь же живут горячие клавиши `TweenerHub` (`Alt+T` / `Ctrl+Alt+T`) и `UIStateSwitcher` (`Alt+S` / `Ctrl+Alt+S`) — описаны в README `TweenerSystem` и `StateSwitcher`. Выбор целей, группировка Undo и создание слоёв у всех команд — общий код `EditorTools/HierarchyTools/HierarchyLayers.cs`.

## Расширение

Новый вид элемента — три класса в любой editor-сборке, ссылающейся на `ru.vortex.unity.ui.uibuilder`:

```csharp
[Serializable]
public sealed class ImageModuleSettings : UIBuilderModuleSettings
{
    public ImageModuleSettings() : base("Image") { }
}

public sealed class ImageModule : UIBuilderModule<ImageModuleSettings>
{
    private const string MenuPath = "GameObject/Vortex Primitives/Create Image";

    public override string Title => "Image";

    public override IEnumerable<UIBuilderSection> CreateSections()
    {
        yield return new IconSection();
    }

    [MenuItem(MenuPath, true)]
    private static bool OpenValidate() => UIBuilderController.CanOpen();

    [MenuItem(MenuPath, false, 3)]
    private static void Open(MenuCommand command) => UIBuilderController.Open<ImageModule>(command);
}
```

Своя секция — наследник `UIBuilderSection` с `PartType`, `Title` и `Apply`; модуль перечисляет её в `CreateSections()`. Нестандартная сборка — переопределить `UIBuilderModule.Apply`.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `folder` пуст | Пункт меню открывает страницу `Vortex/UIBuilder`, предупреждение в лог |
| `folder` не существует | Окно: «Папка не найдена», создание недоступно; `defaultPrefab` — пустой список |
| В папке нет префабов | Окно: «В папке нет префабов», создание недоступно |
| `defaultPrefab` пуст или вне каталога | Примитив не выбран, «Создать» недоступно до выбора |
| Нет активного выделенного объекта | Пункт меню недоступен |
| Выделено несколько объектов | Создание только под активным, повторные вызовы контекстного меню отсекаются |
| Родитель без `RectTransform` | Слой создаётся, предупреждение в лог |
| Секция показана, но части ушли во вложенный `UIComponent` примитива | Секция пропускается, предупреждение в лог |
| `UIComponent.Init` не найден (переименован) | `MissingMethodException` в лог, созданное откатывается |
| У `Set*Component` не найдено поле | `InvalidOperationException` с именем поля, созданное откатывается |
| Перезагрузка домена при открытом окне | Окно закрывается |
| Модуль удалён из кода | Его настройки удаляются при следующей синхронизации, запись в лог |
| Поле, добавленное в настройки после сохранения файла | Может загрузиться значением по умолчанию типа (например, `defaultSize = 0 × 0`) — выставить вручную |

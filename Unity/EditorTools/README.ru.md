# EditorTools

Пакет атрибутов и Odin-drawer'ов для кастомизации Unity Inspector в проектах Vortex. Полностью построен поверх Sirenix Odin Inspector — нативный пайплайн `PropertyDrawer`/MultiDrawer не используется.

## Назначение

- Декларативная настройка инспектора через атрибуты на сериализованных полях.
- Расширение возможностей Odin доменными конструкциями Vortex (фильтры по типам, авто-линковка, popup-селекторы значений и т. п.).
- Общая инфраструктура (палитра темы, popup с поиском, info/error-блоки), переиспользуемая другими editor-пакетами Vortex.

## Архитектура

### Сборки

| Assembly | Содержимое | Constraints |
|----------|-----------|-------------|
| `ru.vortex.unity.editortools` | Атрибуты (runtime) + editor-утилиты (`Elements/`, `EditorSettings/`, `InspectorHandler`) | — |
| `ru.vortex.unity.editortools.sirenix` | Odin-drawer'ы (`SirenixOdinDrawers/`) | `defineConstraints: ["ODIN_INSPECTOR"]` |

### Структура папок

```
EditorTools/
├── Attributes/                  # Runtime-атрибуты
├── SirenixOdinDrawers/          # Odin-drawer'ы (Editor-only)
├── DataModelSystem/             # [DataModel] — развёртка runtime-объектов
├── Elements/                    # DrawingUtility, SearchablePopup
├── EditorSettings/              # ToolsSettings, ThemeColors, DefaultColors
└── InspectorHandler.cs          # Утилиты SerializedProperty (IsPropertyNullable, GetPropertyValue)
```

### Условная компиляция

- Атрибуты — без guard'ов, доступны в runtime (наследуются от `PropertyAttribute`/`Attribute`).
- Drawer'ы (`SirenixOdinDrawers/`) — `#if UNITY_EDITOR` + define-constraint `ODIN_INSPECTOR` на уровне asmdef.
- Editor-утилиты (`Elements/`, `InspectorHandler`, `EditorSettings/`) — `#if UNITY_EDITOR`.
- `PropertyFoldoutGroupAttribute` — наследуется от Odin `FoldoutGroupAttribute` под `#if ODIN_INSPECTOR`, иначе fallback на `Attribute`.

## Атрибуты

### `[AutoLink]`

Авто-привязка `UnityEngine.Object`-поля при null. Drawer берёт `Property.SerializationRoot` (обычно MonoBehaviour) и вызывает `GetComponent(fieldType)` на его GameObject. Учитывает `[ClassFilter]`, если он установлен на том же поле.

```csharp
[SerializeField, AutoLink] private Animator animator;
[SerializeField, AutoLink, ClassFilter(typeof(IInteractable))]
private MonoBehaviour interactable;
```

### `[ClassFilter(params Type[] requiredTypes)]`

Валидация значения `UnityEngine.Object`-поля по списку типов (классы или интерфейсы). Если назначенный объект не наследуется ни от одного `RequiredTypes` — поле очищается с предупреждением в консоль.

```csharp
[SerializeField, ClassFilter(typeof(IDamageable), typeof(IHealable))]
private MonoBehaviour target;
```

### `[ClassLabel(string groupName = "$ToString")]`

Odin-процессор: оборачивает все члены класса/структуры в `FoldoutGroup` с заголовком `groupName`. Поддерживает `$Method` для динамического значения.

```csharp
[Serializable, ClassLabel("$GetTitle")]
public class WeaponSlot
{
    public string id;
    public int damage;
    private string GetTitle() => $"{id} ({damage} dmg)";
}
```

### `[ToggleButton(string labelsMethod = null, string colorsMethod = null, bool isSingleButton = false)]`

Заменяет поле горизонтальной полоской кнопок-переключателей. Поддерживаемые типы: `bool`, `int`, `byte`, `enum`.

- `labelsMethod` — метод, возвращающий `Dictionary<int, string>` (ключ = значение поля, value = текст кнопки).
- `colorsMethod` — метод, возвращающий `Dictionary<int, Color>`.
- `isSingleButton` — рендерит одну кнопку, по клику циклически меняет значение.

Дефолты:
- `bool` без `labelsMethod` — кнопки `Off`/`On` с цветами `SwitcherOffBg`/`SwitcherOnBg`.
- `enum` без `labelsMethod` — кнопки по именам enum-значений.
- `int`/`byte` без `labelsMethod` — ошибка в инспекторе.

```csharp
[SerializeField, ToggleButton] private bool isActive;
[SerializeField, ToggleButton(nameof(GetLabels))] private int mode;

private Dictionary<int, string> GetLabels() => new()
{
    { 0, "Idle" }, { 1, "Run" }, { 2, "Attack" },
};
```

### `[ValueSelector(string methodName, Placeholder = "...")]`

Заменяет поле на `SearchablePopup` с вариантами из метода. Поддерживаемые типы возврата:

- `string[]` / `List<string>` / `IEnumerable<string>` — ключ = значение, пишется в string-поле.
- `Dictionary<string, TValue>` — ключи отображаются в popup, при выборе `TValue` пишется в поле.

Метод может быть instance/static, public/private, без параметров.

```csharp
[SerializeField, ValueSelector("GetTags")] private string tag;
private string[] GetTags() => new[] { "Player", "Enemy", "NPC" };

[SerializeField, ValueSelector("GetTypes", Placeholder = "— Pick Type —")]
private string typeName;
private Dictionary<string, string> GetTypes() => /* FullName → AssemblyQualifiedName */ ;
```

### `[DateTimeDraw]` / `[TimeDraw]`

Отображение `long` (Unix-timestamp / тики) как редактируемой даты или времени.

| Атрибут | Формат | Применение |
|---------|--------|-----------|
| `[DateTimeDraw]` | `dd.MM.yyyy HH:mm:ss` | Полная дата + время |
| `[TimeDraw]` | `hh:mm:ss` | Только время |

### `[TimerDraw]` / `[DateTimerDraw]`

Read-only варианты для отображения значений-таймеров.

| Атрибут | Применение |
|---------|-----------|
| `[TimerDraw]` | Read-only таймер (длительность) |
| `[DateTimerDraw]` | Read-only дата (например, момент срабатывания) |

### `[PropertyFoldoutGroup(string groupName, ...)]`

Расширение Odin `FoldoutGroupAttribute`: foldout, в заголовке которого рендерится одно из полей группы. Имя выбираемого поля — `PropertyName` (по умолчанию совпадает с `groupName`), кастомный заголовок — `Title`.

```csharp
[PropertyFoldoutGroup("settings"), SerializeField] private string settings;
[PropertyFoldoutGroup("settings"), SerializeField] private float volume;
[PropertyFoldoutGroup("settings"), SerializeField] private bool mute;
```

### `[DataModel]` + `[DataModelMethod]`

Развёртка runtime-объекта в инспекторе: публичные свойства (включая унаследованные) отображаются в foldout-группе, свойства с сеттером доступны для редактирования через рефлексию. Методы помеченные `[DataModelMethod]` рендерятся как кнопки.

```csharp
[SerializeField, DataModel] private PlayerStateModel state;

public class PlayerStateModel
{
    public int Hp { get; set; }
    public string Name { get; private set; }

    [DataModelMethod("Reset")]
    public void Reset() => Hp = 100;
}
```

## Odin-drawer'ы

| Drawer | Атрибут | Базовый класс |
|--------|---------|---------------|
| `AutoLinkAttributeDrawer` | `[AutoLink]` | `OdinAttributeDrawer<AutoLinkAttribute>` |
| `ClassFilterAttributeDrawer` | `[ClassFilter]` | `OdinAttributeDrawer<ClassFilterAttribute>` |
| `ToggleButtonAttributeDrawer` | `[ToggleButton]` | `OdinAttributeDrawer<ToggleButtonAttribute>` |
| `ValueSelectorAttributeDrawer` | `[ValueSelector]` | `OdinAttributeDrawer<ValueSelectorAttribute>` |
| `DateTimeAttributeDrawer` | `[DateTimeDraw]` | `OdinAttributeDrawer<DateTimeDrawAttribute, long>` |
| `TimeAttributeDrawer` | `[TimeDraw]` | `OdinAttributeDrawer<TimeDrawAttribute, long>` |
| `TimerAttributeDrawer` | `[TimerDraw]` | `OdinAttributeDrawer<TimerDrawAttribute, long>` |
| `DateTimerAttributeDrawer` | `[DateTimerDraw]` | `OdinAttributeDrawer<DateTimerDrawAttribute, long>` |
| `PropertyFoldoutGroupAttributeDrawer` | `[PropertyFoldoutGroup]` | `OdinGroupDrawer<PropertyFoldoutGroupAttribute>` |
| `ClassLabelAttributeProcessor` | `[ClassLabel]` | `OdinAttributeProcessor` |
| `DataModelDrawer` | `[DataModel]` | `OdinAttributeDrawer<DataModelAttribute>` |

### Соглашения и ключевые приёмы

- `OdinAttributeDrawer<TAttr>` без `TValue` — для атрибутов, применимых к полям любого типа. Доступ к значению: `Property.ValueEntry.WeakSmartValue`.
- `OdinAttributeDrawer<TAttr, TValue>` — когда тип значения известен (`long`, `string`, `UIStateSwitcher` и т. п.). Доступ через `ValueEntry.SmartValue`.
- `ValueResolver<T>` / `ValueResolver.GetForString` — резолв литералов, методов, свойств, полей.
- `Property.Info.GetMemberInfo() as FieldInfo` — извлечение `FieldInfo` поля.
- `Property.Info.TypeOfValue` — декларированный тип поля.
- `Property.SerializationRoot.ValueEntry.WeakSmartValue` — корневой объект (MonoBehaviour/SO).
- `Property.Tree.UnitySerializedObject?.FindProperty(Property.UnityPropertyPath)` — мост к Unity `SerializedProperty`, нужен для drawer'ов, которые модифицируют значение через `serializedObject.ApplyModifiedProperties`.
- `CallNextDrawer(label)` — продолжение цепочки drawer'ов.
- Сообщения: `SirenixEditorGUI.ErrorMessageBox` / `InfoMessageBox`.

## Утилиты

### `Elements/DrawingUtility`

Примитивы GUI поверх `EditorGUI`:
- `DrawSelector(Rect, SerializedProperty, keys, values, currentIndex, placeholder)` — popup-селектор через `SearchablePopupWindow`
- `MakeInfoBox(Rect, text, hasError, icon)` / `CalcInfoBoxHeight(text, width)` — info/error блоки с rich-text
- `DrawBoxBorder(Rect, color, c2, raise, ...)` — рамка из 1px-линий

### `Elements/SearchablePopup` / `SearchablePopupWindow`

Попап со встроенным поиском и группировкой по разделителю `/`. Используется `ValueSelectorAttributeDrawer` и `DrawSelector`.

API имеет две перегрузки `Draw`, рассчитанные на разные паттерны хранения состояния:

| Перегрузка | Сигнатура | Когда использовать |
|---|---|---|
| **Stateful** | `Draw(rect, controlId, selectedIndex, items, placeholder?)` | Caller хранит «текущий индекс» в своём поле, не во внешнем источнике. Попап ведёт `SelectionCache[controlId]`, между фреймами помнит активный выбор. |
| **Stateless** | `Draw(rect, items, selectedIndex, placeholder, onPicked)` | Текущий индекс выводится из внешнего источника истины (Odin `ValueEntry`, `SerializedProperty`, доменная модель) на каждом OnGUI. Кеш `SelectionCache` не задействован — выбор приходит **только** через `onPicked(index)`, caller сам пишет результат во внешнее состояние. |

Stateless-перегрузка нужна там, где попап используется как «представление выбора», а не как «носитель выбора». Без неё caller с авторитативным внешним состоянием попадал в гонку: попап-окно асинхронно писало в кеш, а на следующем OnGUI caller передавал старое значение из своего источника — кеш затирался, выбор терялся. См. `DbRecordAttributeDrawer` как пример.

### `InspectorHandler`

Утилиты `SerializedProperty`: `IsPropertyNullable(property)` (true для String/ObjectReference), `GetPropertyValue(property)` (boxed-значение примитивов).

### `EditorSettings/ToolsSettings`

`ScriptableObject` с парой `ThemeColors` (light / pro). `ThemeColors` хранит словарь `Dictionary<DefaultColors, Color>` — централизованная палитра для drawer'ов. Доступ:

```csharp
ToolsSettings.GetBgColor(DefaultColors.SwitcherOnBg);
ToolsSettings.GetLineColor(DefaultColors.TextColor);
```

## Зависимости

- Odin Inspector (Sirenix) — обязателен для всех drawer'ов в `SirenixOdinDrawers/`.
- Unity 2021.3+ (тестировалось на Unity 2022.3 LTS).

## Граничные случаи

- `[ClassFilter]` на поле, тип которого не наследуется от `UnityEngine.Object`, — drawer выводит ErrorMessageBox и пропускает значение без изменений.
- `[AutoLink]` без `SerializationRoot`-MonoBehaviour (например, на ScriptableObject) — линковка не выполняется, drawer тихо пропускает.
- `[ToggleButton]` на `int`/`byte` без `labelsMethod` — ErrorMessageBox.
- `[ValueSelector]` с возвратом `null`/пустой коллекции — ErrorMessageBox под полем, само поле остаётся редактируемым стандартным drawer'ом.
- `[DateTimeDraw]` и подобные на полях не-`long` — Odin не активирует drawer (TValue не совпадает).

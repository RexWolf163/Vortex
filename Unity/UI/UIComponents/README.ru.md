# UIComponents

**Namespace:** `Vortex.Unity.UI.UIComponents`
**Сборка:** `ru.vortex.unity.ui.misc`

## Назначение

Модульная система UI-компонентов. `UIComponent` (MonoBehaviour) управляет массивами типизированных `UIComponentPart`, обеспечивая единый API для работы с текстами, кнопками, графикой и состояниями.

Возможности:
- Массовое и точечное обновление UI через `PutData()` / `SetText()` / `SetSprite()` / `SetAction()` / `SetSwitcher()`
- Программное нажатие и отпускание кнопки части через `CallPress()` / `CallRelease()`
- Поддержка Text, TextMeshPro, TextMeshProUGUI, Button, AdvancedButton, SpriteRenderer, Image, UIStateSwitcher
- Позиционная адресация part'ов для multi-part компонентов
- `[UIComponentLink]` атрибут для type-safe выбора позиции в Inspector
- Опциональная локализация текстов (включена по умолчанию) с переустановкой при смене локали

Вне ответственности:
- Логика отображения данных (уровень 3/4)
- Управление жизненным циклом интерфейсов (`UIProviderSystem`)

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Unity.UI.StateSwitcher` | `UIStateSwitcher` — интеграция через `UIComponentSwitcher` |
| `Vortex.Unity.EditorTools` | `[AutoLink]` — авто-привязка компонентов |
| `Vortex.Core.LocalizationSystem` | `StringExt.Translate()` — локализация текстов |
| Odin Inspector | `[ShowInInspector]`, `[Button]`, `[TitleGroup]` |
| TextMeshPro | TMP-компоненты |

---

## Архитектура

```
UIComponents/
├── UIComponent.cs                  # Центральный оркестратор (partial)
├── UIComponentExtEditor.cs         # Editor: Init(), Test(), GetLinks(), блок «Выравнивание текста»
├── UIComponentData.cs              # Struct данных для PutData()
├── Parts/
│   ├── UIComponentPart.cs          # Абстрактная база part'а
│   ├── UIComponentText.cs          # Text, TMP, TMPUGUI
│   ├── UIComponentButton.cs        # Button, AdvancedButton
│   ├── UIComponentGraphic.cs       # SpriteRenderer, Image (+ Texture2D→Sprite)
│   └── UIComponentSwitcher.cs      # UIStateSwitcher
├── Attributes/
│   └── UIComponentLinkAttribute.cs # Type-safe позиция part'а
└── Editor/
    └── UIComponentLinkAttributeDrawer.cs  # Slider + валидация
```

### UIComponent

Partial MonoBehaviour. Хранит четыре массива part'ов:

| Массив | Тип part'а | Назначение |
|--------|-----------|-----------|
| `uiComponentTexts[]` | `UIComponentText` | Текстовые элементы |
| `uiComponentButtons[]` | `UIComponentButton` | Кнопки |
| `uiComponentGraphics[]` | `UIComponentGraphic` | Графика |
| `uiComponentSwitchers[]` | `UIComponentSwitcher` | Состояния |

### Init() — автообнаружение part'ов

Кнопка `Init` в Inspector запускает рекурсивный `GetComponentsInChildren` по всем четырём типам part'ов. При наличии вложенных `UIComponent` в иерархии — сначала рекурсивно вызывает `Init()` на каждом дочернем `UIComponent`, затем **исключает** все part'ы, уже принадлежащие дочерним контейнерам. Каждый part принадлежит ровно одному `UIComponent`.

После сбора part'ов `Init()` заполняет `_testData` текущими значениями (тексты, спрайты, состояния свитчеров) для отладки через кнопку `Test`.

### Выравнивание текста

Блок инспектора «Выравнивание текста» (тип `TextAlignmentOptions` из TMP) задаёт выравнивание всех связанных текстов разом: чтение — по первому связанному тексту, запись — всем (с Undo и записью prefab-оверрайдов). Значение не сериализуется в `UIComponent` — всегда берётся из самих текстов. Блок скрыт, если связанных текстов нет. Legacy `Text` получает ближайший из 9 якорей (justified/flush → влево, baseline → вниз, capline → вверх, geometry → центр). Только редактор: системные методы `UIComponentText` (`GetAlignment`/`SetAlignment`) — `internal` под `#if UNITY_EDITOR`.

### Локализация

`useLocalization` включён по умолчанию: тексты проходят через `StringExt.Translate()`. Компонент запоминает последние значения, переданные в `SetText`, по позициям частей — в частях лежит уже перевод, а ключ известен только ему. На `OnEnable` (и только при включённом `useLocalization`) он подписывается на `Localization.OnLocalizationChanged` и по событию переустанавливает тексты из кэша, на `OnDisable` — отписывается.

Тексты, заданные только в префабе, при смене локали не обновляются: компонент их не задавал и ключа не знает. Для такого случая есть `SetTextComponent` с полем `[LocalizationKey]`.

### UIComponentPart (abstract)

Базовый класс для всех part'ов. В Editor автоматически заполняет RectTransform до размера контейнера (если не отмечен `onlyNativeSize`).

### Реализации

**UIComponentText** — поддерживает Text (legacy), TextMeshPro, TextMeshProUGUI. Автообнаружение компонентов в Editor (`[OnInspectorInit]`).

**UIComponentButton** — поддерживает Button и AdvancedButton. Отслеживает `_currentAction`, снимает старый listener перед установкой нового. Очистка в `OnDestroy`.

Программное управление: `CallPress()` — у `AdvancedButton` его `Press()` (визуал и логика нажатия по режиму фиксации клика), у `Button` — PointerDown (визуальное «нажата»; своего API нажатия у `Button` нет). `CallRelease()` — у `AdvancedButton` его `Release()` (клик по правилам режима; без предшествующего нажатия — ничего), у `Button` — PointerUp и `onClick`, если кнопка активна и интерактивна.

**UIComponentGraphic** — поддерживает SpriteRenderer и Image. Принимает как `Sprite`, так и `Texture2D` (автоконвертация в Sprite). Валидация типа в Editor: допускаются только `SpriteRenderer` и `Image`.

**UIComponentSwitcher** — мост к `UIStateSwitcher`. Принимает `int` или `Enum` для переключения состояния.

---

## API

```csharp
// Массовое применение данных
component.PutData(new UIComponentData {
    texts = new[] { "Title", "Subtitle" },
    sprites = new[] { icon },
    actions = new[] { OnClick }
});

// Точечное обращение
component.SetText("Title");
component.SetText("Subtitle", 1);       // по позиции
component.SetSprite(icon);
component.SetSprite(texture2D);          // автоконвертация
component.SetAction(OnClick);
component.SetAction(OnClick, 2);         // по позиции
component.CallPress();                   // программное нажатие кнопки части 0
component.CallRelease();                 // программное отпускание (клик по правилам кнопки)
component.SetSwitcher(SwitcherState.On);
```

### UIComponentData (struct)

| Поле | Тип | Описание |
|------|-----|----------|
| `texts` | `string[]` | Тексты для каждого `UIComponentText` |
| `actions` | `UnityAction[]` | Callback'и для каждого `UIComponentButton` |
| `sprites` | `Sprite[]` | Спрайты для каждого `UIComponentGraphic` |
| `enumValues` | `int[]` | Состояния для каждого `UIComponentSwitcher` |

### UIComponentLinkAttribute

Type-safe выбор позиции part'а в Inspector. Отображает slider 0..N с именем целевого part'а:

```csharp
[SerializeField, UIComponentLink(typeof(UIComponentText), "uiComponent")]
private int position = -1;  // -1 = по умолчанию, 0..N = конкретный part
```

Значение -1 по умолчанию подписывается «Настройка будет применена ко всем компонентам». Если у поля -1 значит другое, третий параметр задаёт свою подсказку: `UIComponentLink(typeof(UIComponentGraphic), "component", "Спрайт не выводится")`.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `SetText("x", pos)` без `UIComponentText` или вне диапазона | `Debug.LogError`, возврат |
| `SetText("x")` при `uiComponentTexts == null` или пустом массиве | `Debug.LogError`, возврат |
| `SetAction(null)` | Снятие текущего listener |
| `CallPress` / `CallRelease` без кнопки на позиции | `Debug.LogWarning`, возврат |
| `CallRelease` у `AdvancedButton` без предшествующего `CallPress` | Ничего не происходит |
| `PutData()` с массивом короче кол-ва part'ов | Тексты/кнопки/графика: отсутствующие элементы обнуляются (пустая строка / null / null). Свитчеры: обработка прерывается (`break`) |
| `SetSprite(Texture2D)` | Создаётся `Sprite` через `Sprite.Create()` |
| `position` вне диапазона | `Debug.LogError` + возврат (позиционные методы), `IndexOutOfRangeException` (прямой доступ к массиву) |
| `useLocalization = true` (по умолчанию) | Тексты проходят через `StringExt.Translate()`; компонент подписан на смену локали |
| Смена локали, текст из кода не задавали | Ничего не происходит: кэш пуст, текст из префаба не трогаем |
| Смена локали, состав частей изменился после последнего `SetText` | Предупреждение в лог, тексты не переустанавливаются |
| Связанные тексты с разным выравниванием | Блок «Выравнивание текста» показывает выравнивание первого; изменение выставляет выбранное всем |
| Смена локали, в позицию писали `null` | Позиция очищается: `Translate(null)` даёт пустую строку |

### UIComponentLinkAttribute

| Ситуация | Поведение |
|----------|-----------|
| `position = -1` | Drawer показывает warning "ко всем компонентам" |
| `position` вне диапазона | Drawer показывает error |
| `position` в диапазоне | Drawer показывает disabled ObjectField с целевым GameObject |

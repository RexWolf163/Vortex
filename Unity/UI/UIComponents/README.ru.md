# UIComponents

**Namespace:** `Vortex.Unity.UI.UIComponents`
**Сборка:** `ru.vortex.unity.ui.misc`

## Назначение

Модульная система UI-компонентов. `UIComponent` (MonoBehaviour) управляет массивами типизированных `UIComponentPart`, обеспечивая единый API для работы с текстами, кнопками, графикой и состояниями.

Возможности:
- Массовое и точечное обновление UI через `PutData()` / `SetText()` / `SetSprite()` / `SetAction()` / `SetSwitcher()`
- Поддержка Text, TextMeshPro, TextMeshProUGUI, Button, AdvancedButton, SpriteRenderer, Image, UIStateSwitcher
- Позиционная адресация part'ов для multi-part компонентов
- `[UIComponentLink]` атрибут для type-safe выбора позиции в Inspector
- Опциональная локализация текстов (включена по умолчанию)

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
├── UIComponentExtEditor.cs         # Editor: Init(), Test(), GetLinks()
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

### UIComponentPart (abstract)

Базовый класс для всех part'ов. В Editor автоматически заполняет RectTransform до размера контейнера (если не отмечен `onlyNativeSize`).

### Реализации

**UIComponentText** — поддерживает Text (legacy), TextMeshPro, TextMeshProUGUI. Автообнаружение компонентов в Editor (`[OnInspectorInit]`).

**UIComponentButton** — поддерживает Button и AdvancedButton. Отслеживает `_currentAction`, снимает старый listener перед установкой нового. Очистка в `OnDestroy`.

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

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `SetText("x", pos)` без `UIComponentText` или вне диапазона | `Debug.LogError`, возврат |
| `SetText("x")` при `uiComponentTexts == null` | `NullReferenceException` |
| `SetAction(null)` | Снятие текущего listener |
| `PutData()` с массивом короче кол-ва part'ов | Тексты/кнопки/графика: отсутствующие элементы обнуляются (пустая строка / null / null). Свитчеры: обработка прерывается (`break`) |
| `SetSprite(Texture2D)` | Создаётся `Sprite` через `Sprite.Create()` |
| `position` вне диапазона | `Debug.LogError` + возврат (позиционные методы), `IndexOutOfRangeException` (прямой доступ к массиву) |
| `useLocalization = true` (по умолчанию) | Тексты проходят через `StringExt.Translate()` |

### UIComponentLinkAttribute

| Ситуация | Поведение |
|----------|-----------|
| `position = -1` | Drawer показывает warning "ко всем компонентам" |
| `position` вне диапазона | Drawer показывает error |
| `position` в диапазоне | Drawer показывает disabled ObjectField с целевым GameObject |

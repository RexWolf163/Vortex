# StateSwitcher

**Namespace:** `Vortex.Unity.UI.StateSwitcher`
**Сборка:** `ru.vortex.unity.ui.misc`

## Назначение

Машина состояний для визуальных и поведенческих переключений. `UIStateSwitcher` управляет массивом именованных состояний, каждое из которых содержит набор `StateItem`.

Возможности:
- Переключение по индексу, имени или enum
- 7 готовых StateItem: GameObjects, Animator (bool/int), Colors (с анимацией), Sprites, Events, TweenerHub
- `StateSwitcherAttribute` для привязки к enum с визуализацией в Inspector
- Событие `OnStateSwitch` при смене состояния
- Защита от реентрантности
- `UISlaveStateSwitcher` — ведомый свитчер, повторяющий состояние мастера
- Горячие клавиши `Alt+S` / `Ctrl+Alt+S` — добавить `UIStateSwitcher` на объект или отдельным слоем

Вне ответственности:
- Анимации переходов между состояниями (используйте `TweenerHubSwitch`, `ColorsSwitch` или `StateView<TEnum>` из TweenerSystem, когда состояния — это анимации)
- Логика выбора состояния (уровень 3/4)

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Unity.UI.TweenerSystem` | `TweenerHub` — интеграция через `TweenerHubSwitch` |
| `Vortex.Unity.UI.TweenerSystem.UniTaskTweener` | `AsyncTween` — анимация цвета в `ColorsSwitch` |
| `Vortex.Unity.EditorTools` | `DrawingUtility`, `ToolsSettings` — рендеринг `StateSwitcherDrawer` |
| `Vortex.Unity.AppSystem.System.TimeSystem` | `TimeController` — покадровая задержка в `GameObjectsSwitch` |
| `Vortex.Core.ExtensibleEnumSystem` | `ExtensibleEnum` — перегрузка `Set(ExtensibleEnum)` по `Order` |
| Odin Inspector | `[ValueDropdown]`, `[ShowInInspector]`, `[Button]` |

---

## Архитектура

```
StateSwitcher/
├── UIStateSwitcher.cs          # Машина состояний (MonoBehaviour)
├── UISlaveStateSwitcher.cs     # Ведомый свитчер: повторяет состояние мастера
├── StateItem.cs                # Абстрактная база поведения
├── Items/
│   ├── GameObjectsSwitch.cs    # SetActive на массив GameObject
│   ├── AnimatorBoolSwitch.cs   # SetBool на Animator
│   ├── AnimatorStateSwitch.cs  # SetInteger на Animator
│   ├── ColorsSwitch.cs         # Цвет (мгновенный или анимированный)
│   ├── SpritesSwitch.cs        # Спрайт на SpriteRenderer/Image
│   ├── EventFire.cs            # UnityEvent при активации
│   └── TweenerHubSwitch.cs     # Forward/Back на TweenerHub
└── Handlers/
    └── OnEnableStateRunner.cs  # Переключение при OnEnable
```

### UIStateSwitcher

MonoBehaviour с массивом `StateData`. Каждый `StateData` — именованное состояние с массивом `StateItem[]`.

При переключении:
1. Вызывает `DefaultState()` на всех item'ах предыдущего состояния (**узкое место!**)
2. Вызывает `Set()` на всех item'ах нового состояния
3. Запускает событие `OnStateSwitch`

---

## API

```csharp
switcher.Set(0);                        // по индексу
switcher.Set("Active");                 // по имени
switcher.Set(MyEnum.Active);            // по enum
switcher.Set((byte)2);                  // по byte
switcher.Set(myExtensibleEnum);         // по ExtensibleEnum (через Order, null → no-op)
switcher.ResetStates();                 // сброс всех item'ов + начальное состояние

int current = switcher.State;           // текущее состояние (-1 = нет)
StateData[] all = switcher.States;      // массив всех состояний
switcher.OnStateSwitch += OnSwitch;     // событие (StateData или null)
int index = switcher.GetState("name");  // индекс по имени (-1 = не найден)
```

| Поле Inspector | Тип | Описание |
|----------------|-----|----------|
| `states` | `StateData[]` | Массив состояний |
| `stateOnEnable` | `int` | Начальное состояние (dropdown) |
| `duplicateOnCreate` | `bool` | Editor-only: при добавлении нового состояния клонировать item'ы из последнего |

---

## UISlaveStateSwitcher

Наследник `UIStateSwitcher`, который повторяет состояние мастер-свитчера. У ведомого свои состояния и свои `StateItem`, настраиваются как у обычного свитчера; общий у них с мастером только номер состояния.

| Поле Inspector | Тип | Описание |
|----------------|-----|----------|
| `master` | `UIStateSwitcher` | Мастер-свитчер, обязателен |

Поведение:
- `OnEnable` — подписка на `master.OnStateSwitch` и сразу `Set(master.State)`: ведомый принимает текущее состояние мастера.
- Смена состояния мастера — ведомый выставляет тот же индекс (`-1` у мастера → `-1` у ведомого).
- `OnDisable` — отписка; пока ведомый выключен, он мастера не повторяет, при включении догоняет.

Состояния сопоставляются **по индексу**: их число и имена должны совпадать с мастером.

**Кнопка «Синхронизировать с мастером»** (редактор):
- число и имена состояний — как у мастера, по индексу;
- недостающие состояния добавляются пустыми (без `StateItem`);
- у уже настроенных меняются только имена, элементы не трогаются;
- лишние не удаляются — `LogError`, удалить вручную.

`OnValidate` пишет ошибку, если состояния расходятся с мастером по числу или именам, и если мастером назначен сам свитчер.

---

## StateItem

Абстрактный класс поведения. При активации состояния — `Set()`, при деактивации — `DefaultState()`.

| Реализация | Что делает Set() | Что делает DefaultState() |
|-----------|-----------------|--------------------------|
| `GameObjectsSwitch` | `SetActive(true)` на массив (опционально с задержкой на кадр через `onDelayed`) | `SetActive(false)` (опционально с задержкой через `offDelayed`) |
| `AnimatorBoolSwitch` | `SetBool(name, true)` | `SetBool(name, false)` |
| `AnimatorStateSwitch` | `SetInteger(name, value)` | `SetInteger(name, default)` |
| `ColorsSwitch` | Установка цвета (мгновенно или через `AsyncTween`) | Возврат к `Color.white` |
| `SpritesSwitch` | Установка спрайта | Установка `null` |
| `EventFire` | `UnityEvent.Invoke()` | Ничего |
| `TweenerHubSwitch` | `tweener.Forward()` | `tweener.Back()` |

### ColorsSwitch

Поддерживает SpriteRenderer, Graphic (Image, Text), Outline. Режимы:
- Мгновенная смена цвета
- Анимированная через `AsyncTween` с настраиваемой длительностью и кривой
- Опциональный стартовый цвет: `_useOwnStartColor = true` — берёт текущий цвет с объекта, `false` — использует поле `_startColor`

Inspector-кнопка `Get from...` — захватывает текущий цвет с первого объекта.

---

## StateSwitcherAttribute

Атрибут для привязки `UIStateSwitcher` к enum. Используется вместе с `StateSwitcherDrawer`.

```csharp
[SerializeField, StateSwitcher(typeof(MyStatesEnum))]
private UIStateSwitcher switcher;
```

В Inspector отображает таблицу состояний:
- Индекс, описание (из `[Tooltip]` / `[LabelText]`), имя из switcher
- Подсветка активного состояния
- Клик для переключения
- Кнопка **Sync** — синхронизирует имена состояний с описаниями enum

---

## Горячие клавиши

Для выделенных объектов сцены или открытого префаба (`UI/UIBuilder/Shortcuts/UIStateSwitcherShortcut.cs`):

| Сочетание | Меню | Действие |
|-----------|------|----------|
| `Alt+S` | `Tools/Vortex/UI/Add UIStateSwitcher` | Добавить `UIStateSwitcher` на сам объект; где он уже есть — пропуск |
| `Ctrl+Alt+S` | `Tools/Vortex/UI/Add UIStateSwitcher Layer` | Дочерний слой `[UIStateSwitcher]` со свитчером: первым в иерархии, в нулевой точке, на обычном `Transform` (`RectTransform` снимается); новые слои выделяются |

Ассеты в Project window не затрагиваются. Отмена — одним шагом Undo. Пункты меню живут в пакете `UIBuilder` вместе с остальными горячими клавишами вёрстки; добавление компонента и создание слоя — общий код `EditorTools/HierarchyTools/HierarchyLayers.cs`, тот же, что у горячих клавиш `TweenerHub` (`Alt+T` / `Ctrl+Alt+T`).

---

## OnEnableStateRunner

MonoBehaviour — переключает `UIStateSwitcher` в заданное состояние при `OnEnable`.

| Поле | Тип | Описание |
|------|-----|----------|
| `_stateSwitcher` | `UIStateSwitcher` | Целевой switcher |
| `_stateToOpen` | `int` | Индекс состояния |

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `Set()` с индексом вне диапазона | `LogError`, состояние не меняется |
| `Set()` с тем же индексом | Пропуск (состояние не перезаписывается) |
| `Set()` во время переключения | Защита от реентрантности (`_isSwitching` guard) |
| `Set()` до `Awake` | Состояние сохраняется в `_startState`, применяется в `Awake` |
| `GetState("несуществующее")` | Возвращает -1 |
| `Set("несуществующее")` | `LogError`, **затем откат к `stateOnEnable`** — switcher не остаётся в неопределённом состоянии |
| `StateItem.Set()` бросает исключение | Перехват `try/catch` вокруг цикла `foreach`: `LogError` + `LogException`, остальные `StateItem` в этом состоянии **не отрабатываются** (быстрый откат, чтобы не маскировать настоящую проблему) |
| `ColorsSwitch` с `_smoothChange` и нулевой `_duration` | Мгновенная смена (AsyncTween с duration ≤ 0) |
| Дублирующийся `DropDownGroupName/DropDownItemName` у StateItem-классов | `LogError`, дубликат пропущен в dropdown |
| `UISlaveStateSwitcher`: у мастера больше состояний, чем у ведомого | Индекс вне диапазона — `LogError` ведомого, его состояние не меняется |
| `UISlaveStateSwitcher` включился до `Awake` мастера | Ведомый встаёт в `-1`; мастер в `Awake` выставляет стартовое состояние — ведомый догоняет через событие |
| `UISlaveStateSwitcher` в `Awake` | Сначала своё `stateOnEnable`, в `OnEnable` того же кадра — состояние мастера: два переключения подряд, `StateItem` с побочным эффектом (`EventFire`) сработают дважды |
| `master` не задан | `NullReferenceException` в `OnEnable` — ошибка настройки |

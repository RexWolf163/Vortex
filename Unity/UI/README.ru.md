# UI

UI-слой фреймворка. Всё, что касается визуала: компоненты интерфейса, анимации, машина состояний, пул объектов, кнопки и утилиты.

Раздел не управляет жизненным циклом интерфейсов — этим занимается `UIProviderSystem`. Здесь только строительные блоки: из чего собирается интерфейс и как он двигается, мигает и переключается.

## Что внутри

**UIComponents** — модульная система, где один `UIComponent` управляет массивами типизированных part'ов: текст, кнопка, графика, switcher. Единый API (`PutData`, `SetText`, `SetSprite`, `SetAction`) вместо ручной работы с каждым компонентом. Поддерживает Text, TMP, Image, SpriteRenderer, Button, AdvancedButton.

**TweenerSystem** — анимации на UniTask. Два режима. Сценарный — `TweenerHub` на сцене с массивом `TweenLogic` (цвет, прозрачность, масштаб, fill, pivot), Forward/Back/Pulse. Standalone — `AsyncTween` fluent API для одноразовых анимаций из кода, с шорткатами для Move, Scale, Fade, Color. 16 типов easing. `StateView<TEnum>` — поле-переключатель состояний на твинерах: хаб на каждое значение enum, в инспекторе таблица состояний и автосоздание хабов.

**StateSwitcher** — машина состояний. `UIStateSwitcher` переключает именованные состояния, каждое из которых содержит набор `StateItem`: включить/выключить GameObject'ы, сменить цвет (с анимацией или без), поменять спрайт, дёрнуть Animator, запустить TweenerHub, выстрелить UnityEvent.

**PoolSystem** — пул с data-ключами. `Pool` создаёт, переиспользует и деактивирует `PoolItem`. Элементы не уничтожаются — отключаются и возвращаются в очередь.

**Misc** — утилиты. `AdvancedButton` с четырьмя режимами клика и защитой от scroll-drag через `IPointerClickHandler`. `CounterViewBase`/`CounterViewAdvanced` для счётчиков с порогами и пульсацией. `SliderView` с плавным движением. `DataStorage` как универсальный контейнер. `DropDown` — выпадающий список на Pool с сортировкой, дедупликацией и scroll-позиционированием. `AutoRectSetter`, `EnableDelayForChild`, `ScrollRectResetHandler`.

**CursorSystem** — кастомный системный курсор. Дефолтный спрайт, отдельные на LMB/RMB, массив hover-вариантов по UI-зонам. Унифицированный API через `MouseHoverListener` на UGUI-объектах + публичный `CursorController.OnHover/OnUnHover` для не-UGUI источников. Защита от alt-tab из коробки (через Input System soft-reset).

**RollbackSystem** — откат несохранённых изменений экрана. `RollbackHandler` объединяет источники отката (наследники `RollbackSource`), показывает признак изменений через `UIStateSwitcher` и ведёт два пути — «Сохранить» и «Откатить»; выключение экрана — откат. Работает реактивно, без покадровых проверок. Готовый источник — выпадающие списки и слайдеры с маркером `RollbackControl` на контроле.

**UIBuilder** — editor-инструмент вёрстки. ПКМ в Hierarchy → `Vortex Primitives/Create Text` / `Create Button`: под объектом создаётся слой с `UIComponent`, экземпляром выбранного примитива и нужными `Set*Component`. Каталог примитивов — папка из `Project Settings → Vortex/UIBuilder`; параметры окна зависят от частей примитива. Новые виды элементов и секции добавляются без правки ядра.

## Горячие клавиши

Команды для выделенных объектов сцены или открытого префаба. Ассеты в Project window не затрагиваются, отмена — одним шагом Undo, новые слои выделяются.

| Сочетание | Меню | Действие |
|-----------|------|----------|
| `Alt+T` / `Ctrl+Alt+T` | `Tools/Vortex/UI/Add TweenerHub` / `… Layer` | `TweenerHub` на объект или отдельным слоем — см. `TweenerSystem/` |
| `Alt+S` / `Ctrl+Alt+S` | `Tools/Vortex/UI/Add UIStateSwitcher` / `… Layer` | `UIStateSwitcher` на объект или отдельным слоем — см. `StateSwitcher/` |
| `Alt+I` | `Tools/Vortex/UI/Add BackgroundLayer` | Дочерний слой `Background` с `Image` (Maskable выключен, Raycast Target включён): первым в иерархии, `RectTransform` растянут по родителю. Повторное нажатие — ещё один слой |

Код — `Shortcuts/` (`ComponentShortcuts.cs` — общее создание слоёв компонентов, `BackgroundLayerShortcut.cs`).

## Зависимости

UniTask, TextMeshPro, Odin Inspector. Из фреймворка — `TimeController`, `ActionExt`, `IDataStorage`, `EditorTools`, `SettingsSystem` (для `CursorSystem`).

## Документация подсистем

Каждая подсистема документирована отдельно:

- `UIComponents/` — модульные UI-компоненты
- `TweenerSystem/` — анимации
- `StateSwitcher/` — машина состояний
- `PoolSystem/` — пул объектов
- `Misc/` — утилитарные компоненты
- `CursorSystem/` — кастомный курсор
- `RollbackSystem/` — откат изменений экрана
- `UIBuilder/` — создание UI-слоёв из примитивов (Editor)

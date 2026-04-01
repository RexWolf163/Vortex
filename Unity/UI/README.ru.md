# UI

UI-слой фреймворка. Всё, что касается визуала: компоненты интерфейса, анимации, машина состояний, пул объектов, кнопки и утилиты.

Раздел не управляет жизненным циклом интерфейсов — этим занимается `UIProviderSystem`. Здесь только строительные блоки: из чего собирается интерфейс и как он двигается, мигает и переключается.

## Что внутри

**UIComponents** — модульная система, где один `UIComponent` управляет массивами типизированных part'ов: текст, кнопка, графика, switcher. Единый API (`PutData`, `SetText`, `SetSprite`, `SetAction`) вместо ручной работы с каждым компонентом. Поддерживает Text, TMP, Image, SpriteRenderer, Button, AdvancedButton.

**TweenerSystem** — анимации на UniTask. Два режима. Сценарный — `TweenerHub` на сцене с массивом `TweenLogic` (цвет, прозрачность, масштаб, fill, pivot), Forward/Back/Pulse. Standalone — `AsyncTween` fluent API для одноразовых анимаций из кода, с шорткатами для Move, Scale, Fade, Color. 16 типов easing.

**StateSwitcher** — машина состояний. `UIStateSwitcher` переключает именованные состояния, каждое из которых содержит набор `StateItem`: включить/выключить GameObject'ы, сменить цвет (с анимацией или без), поменять спрайт, дёрнуть Animator, запустить TweenerHub, выстрелить UnityEvent.

**PoolSystem** — пул с data-ключами. `Pool` создаёт, переиспользует и деактивирует `PoolItem`. Элементы не уничтожаются — отключаются и возвращаются в очередь.

**Misc** — утилиты. `AdvancedButton` с четырьмя режимами клика и визуальными состояниями. `CounterView` для анимированных счётчиков. `SliderView` с плавным движением. `DataStorage` как универсальный контейнер. `DropDown` — выпадающий список на Pool с сортировкой, дедупликацией и scroll-позиционированием. `AutoRectSetter`, `EnableDelayForChild`, `ScrollRectResetHandler`.

## Зависимости

UniTask, TextMeshPro, Odin Inspector. Из фреймворка — `TimeController`, `ActionExt`, `IDataStorage`, `EditorTools`.

## Документация подсистем

Каждая подсистема документирована отдельно:

- `UIComponents/` — модульные UI-компоненты
- `TweenerSystem/` — анимации
- `StateSwitcher/` — машина состояний
- `PoolSystem/` — пул объектов
- `Misc/` — утилитарные компоненты

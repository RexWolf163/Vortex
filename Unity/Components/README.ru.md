# Components

**Namespace:** `Vortex.Unity.Components`
**Сборка:** `ui.vortex.unity.components`

## Назначение

Набор готовых MonoBehaviour-компонентов для типовых задач: привязка данных к UI, управление сценами, декларативные lifecycle-callback'и, персистентные контейнеры.

Возможности:
- Декларативная привязка текста, спрайтов и действий к `UIComponent` через Inspector
- Переключение языка с визуальной индикацией активного
- Асинхронная загрузка и выгрузка сцен с выбором из dropdown
- Lifecycle-события (`Awake`, `OnDestroy`, `OnEnable`, `OnDisable`) через `UnityEvent` без кода
- Персистентный контейнер с защитой от дублирования при смене сцен

Вне ответственности:
- Логика загрузки приложения (см. `LoaderSystem/`)
- Бизнес-логика и контроллеры

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.AppSystem` | `App.GetState()`, `App.OnStart`, `AppStates` |
| `Vortex.Core.LocalizationSystem` | `Localization`, `Translate()`, `SetCurrentLanguage()` |
| `Vortex.Unity.UI.UIComponents` | `UIComponent`, `UIComponentText`, `UIComponentButton`, `UIComponentGraphic` |
| `Vortex.Unity.LocalizationSystem` | `[LocalizationKey]`, `[Language]` — атрибуты выбора ключей |
| `Vortex.Unity.EditorTools` | `[AutoLink]`, `[UIComponentLink]` |
| Odin Inspector | `[Button]`, `[TitleGroup]`, `[ValueDropdown]` |

---

## Архитектура

```
Components/
├── LoaderSystem/              # Запуск и визуализация загрузки (отдельный README)
├── Misc/
│   ├── LocalizationSystem/    # Привязка данных к UIComponent
│   │   ├── SetTextComponent       # Текст (с локализацией)
│   │   ├── SetSpriteComponent     # Спрайт
│   │   ├── SetActionComponent     # Действие (UnityEvent → кнопка)
│   │   └── SetLocaleHandler       # Переключатель языка
│   ├── MBHandlers/
│   │   └── MonoBehaviourEventsHandler  # Декларативные lifecycle-события
│   └── NotDestroyableSystemContainer   # DontDestroyOnLoad с защитой от дублей
└── SceneControllers/          # Загрузка/выгрузка сцен
    ├── SceneHandler               # Абстрактная база
    ├── LoadSceneHandler           # Single/Additive загрузка
    └── UnloadSceneHandler         # Выгрузка
```

---

## Misc/LocalizationSystem — привязка данных к UIComponent

Четыре компонента для декларативной настройки `UIComponent` через Inspector. Все поддерживают `position` — индекс целевого part'а внутри `UIComponent` (при `-1` используется part по умолчанию).

### SetTextComponent

Устанавливает фиксированный или локализованный текст. `[ExecuteInEditMode]` — текст виден в редакторе без запуска.

| Поле | Тип | Описание |
|------|-----|----------|
| `key` | `string` | Ключ локализации (`[LocalizationKey]` — dropdown) |
| `useLocalization` | `bool` | `true` — `key.Translate()`, `false` — raw-строка |
| `position` | `int` | Индекс `UIComponentText` (-1 = по умолчанию) |

Подписывается на `Localization.OnLocalizationChanged`, `Localization.OnInit`, `App.OnStart`. Обновляется автоматически при смене языка.

### SetSpriteComponent

Устанавливает фиксированный спрайт. Одноразовое присвоение при `OnEnable`.

| Поле | Тип | Описание |
|------|-----|----------|
| `sprite` | `Sprite` | Целевой спрайт |
| `position` | `int` | Индекс `UIComponentGraphic` (-1 = по умолчанию) |

### SetActionComponent

Привязывает `UnityEvent` к кнопке `UIComponent`. При `OnDisable` снимает действие (устанавливает `null`).

| Поле | Тип | Описание |
|------|-----|----------|
| `events` | `UnityEvent` | Callback'и на нажатие |
| `position` | `int` | Индекс `UIComponentButton` (-1 = по умолчанию) |

### SetLocaleHandler

Кнопка переключения языка. При нажатии вызывает `Localization.SetCurrentLanguage()`. Отображает локализованное название языка и опциональный switcher-индикатор активного языка.

| Поле | Тип | Описание |
|------|-----|----------|
| `language` | `string` | Код языка (`[Language]` — dropdown) |
| `useSwitch` | `bool` | Показывать `SwitcherState.On/Off` для активного языка |

---

## MonoBehaviourEventsHandler

Декларативная привязка lifecycle-событий MonoBehaviour через `UnityEvent` в Inspector.

| Поле | Вызывается в |
|------|-------------|
| `onAwake` | `Awake()` |
| `onDestroy` | `OnDestroy()` |
| `onEnable` | `OnEnable()` |
| `onDisable` | `OnDisable()` |

Позволяет дизайнерам настраивать реакции на lifecycle без написания скриптов.

---

## NotDestroyableSystemContainer

Персистентный контейнер (`DontDestroyOnLoad`) с ключевой защитой от дублирования.

| Поле | Тип | Описание |
|------|-----|----------|
| `key` | `string` | Уникальный идентификатор контейнера |

При `Awake` ищет все экземпляры `NotDestroyableSystemContainer` на сцене. Если найден другой с тем же `key` — уничтожает себя. Иначе — вызывает `DontDestroyOnLoad`. Защищает от дублей при запуске из произвольной сцены в редакторе.

---

## SceneControllers

Компоненты для управления сценами через Inspector.

### SceneHandler (абстрактный)

Базовый класс. Поле `sceneName` с dropdown-выбором из Build Settings (`@DropDawnHandler.GetScenes()`). Абстрактный метод `Run()` с `[Button]` для тестирования в Inspector.

### LoadSceneHandler

Асинхронная загрузка сцены. Поле `additiveMode` переключает между `LoadSceneMode.Single` и `LoadSceneMode.Additive`.

### UnloadSceneHandler

Асинхронная выгрузка сцены по имени.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `SetTextComponent` / `SetActionComponent` без `UIComponent` | `LogError`, компонент не работает |
| `SetTextComponent` до инициализации `Localization` | Пустая строка; обновится по `Localization.OnInit` |
| `SetLocaleHandler` — текущий язык совпадает с `language` | Switcher = `On`, повторный вызов `SetCurrentLanguage` безвреден |
| `NotDestroyableSystemContainer` с пустым `key` | `LogError`, но `DontDestroyOnLoad` всё равно вызывается |
| Два `NotDestroyableSystemContainer` с одним `key` | Второй уничтожает себя в `Awake` |
| `position = -1` в Set-компонентах | Используется part по умолчанию (первый в массиве) |

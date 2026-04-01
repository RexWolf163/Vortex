# VideoSystem (Unity)

Unity-реализация драйвера видеосистемы.

## Назначение

Платформенная адаптация `VideoController`: управление разрешением и режимом экрана через `UnityEngine.Screen`, сохранение настроек в `PlayerPrefs`, UI-компоненты для выбора разрешения и режима.

- Сбор доступных разрешений устройства с дедупликацией
- Фильтрация режимов экрана по платформе
- Применение разрешения и режима через `Screen.SetResolution`
- Сохранение/загрузка настроек через `PlayerPrefs`
- Компоненты для инспектора: выпадающие списки разрешений и режимов

Вне ответственности: качество графики, VSync, частота кадров, настройки рендеринга.

## Зависимости

- `Vortex.Core.VideoSystem` — шина `VideoController`, `IVideoDriver`, `ScreenMode`
- `Vortex.Core.System` — `Singleton<T>`
- `Vortex.Core.Extensions` — `ActionExt` (`AddNew`)
- `Vortex.Core.LocalizationSystem` — `StringExt.Translate()` (локализация названий режимов)
- `Vortex.Unity.UI.Misc` — `DropDownComponent`

---

## VideoDriver

Реализация `IVideoDriver`. Partial-класс из двух файлов.

### Архитектура

```
VideoDriver (Singleton<VideoDriver>, IVideoDriver)
├── VideoDriver.cs              — Init/Destroy, сбор разрешений, режимы, Set/Get
└── VideoDriverExtLoading.cs    — [RuntimeInitializeOnLoadMethod] авторегистрация, Save/Load
```

### Контракт

**Вход:**
- Автоматическая регистрация через `[RuntimeInitializeOnLoadMethod]`
- Ссылки на реестры контроллера через `SetLinks()` (вызывается в `OnDriverConnect`)

**Выход:**
- Заполненные реестры `AvailableResolutions` и `AvailableScreenModes` в `VideoController`
- Событие `OnInit` после инициализации
- Настройки в `PlayerPrefs` (ключ `VideoSettings`)

**Формат сохранения:**

```
{resolution};{screenModeByte}
```

Пример: `1920x1080;1`

`resolution` — строка вида `{width}x{height}`. `screenModeByte` — числовое значение `ScreenMode`.

**Гарантии:**
- Разрешения дедуплицируются по `width × height` (частота кадров игнорируется)
- Режимы фильтруются по платформе через условную компиляцию
- Загрузка с `try/catch` — при некорректных данных настройки сбрасываются к текущим параметрам экрана
- Настройки сохраняются при каждом изменении разрешения или режима

**Ограничения:**
- Если `VideoController.SetDriver` вернул `false` — экземпляр уничтожается (`Dispose()`)

---

### Платформенная фильтрация режимов

| ScreenMode | Windows | macOS | Linux | Прочие |
|------------|---------|-------|-------|--------|
| `FullScreenWindow` | + | + | + | + |
| `Windowed` | + | + | + | - |
| `MaximizedWindow` | + | + | - | - |
| `ExclusiveFullScreen` | + | - | - | - |

---

## Handlers

UI-компоненты для настроек видео. Работают с `DropDownComponent`.

### ScreenModeHandler

Выпадающий список режимов экрана с фильтрацией и локализацией.

```
ScreenModeHandler (MonoBehaviour)
├── dropDownComponent: DropDownComponent   — UI выпадающего списка
├── localeTagPrefix: string                — префикс ключа локализации
└── whiteList: ScreenMode[]               — допустимые режимы
```

**Поведение:**
- `Awake` — формирует whitelist из массива `ScreenMode`
- `OnEnable` — запрашивает доступные режимы из `VideoController`, фильтрует по whitelist, локализует названия (`"{prefix}{mode}".Translate()`), передаёт в `DropDownComponent` с текущим индексом
- При выборе — вызывает `VideoController.SetScreenMode()`

### ScreenResolutionHandler

Выпадающий список разрешений экрана.

```
ScreenResolutionHandler (MonoBehaviour)
├── dropDownComponent: DropDownComponent   — UI выпадающего списка
└── _list: string[]                        — копия списка разрешений
```

**Поведение:**
- `OnEnable` — копирует список разрешений из `VideoController`, передаёт в `DropDownComponent` с текущим индексом
- При выборе — вызывает `VideoController.SetResolution()`

---

## Использование

### Минимальная настройка

Драйвер регистрируется автоматически через `[RuntimeInitializeOnLoadMethod]`. Дополнительных действий не требуется.

### UI настроек видео

1. Создать `DropDownComponent` на сцене
2. Добавить `ScreenResolutionHandler`, указать ссылку на `DropDownComponent`
3. Для режимов — добавить `ScreenModeHandler`, указать `DropDownComponent`, заполнить `whiteList` нужными режимами
4. При необходимости — задать `localeTagPrefix` для локализации названий режимов

### Локализация режимов

Ключи локализации формируются как `{localeTagPrefix}{ScreenMode}`:

```
# Пример при localeTagPrefix = "settings.video."
settings.video.FullScreenWindow    → Полноэкранное окно
settings.video.Windowed            → Оконный
settings.video.ExclusiveFullScreen → Полный экран
settings.video.MaximizedWindow     → Развёрнутое окно
```

---

## Граничные случаи

| Сценарий | Поведение |
|----------|----------|
| Первый запуск (нет сохранения) | Используются текущие параметры экрана, сохраняются |
| Сохранённое разрешение недоступно | Настройки загружаются, но `SetResolution` бросит `KeyNotFoundException` при попытке применить |
| Повреждённые данные в `PlayerPrefs` | `try/catch` → сброс к текущим параметрам экрана |
| Пустой `whiteList` в `ScreenModeHandler` | Пустой выпадающий список |
| `OnEnable` до `VideoController.OnInit` | Пустые реестры — выпадающий список пуст |

# LocalizationSystem (Unity)

**Namespace:** `Vortex.Unity.LocalizationSystem`, `Vortex.Unity.Components.Misc.LocalizationSystem`
**Сборки:** `ru.vortex.unity.localization`, `ui.vortex.unity.components`

---

## Назначение

Unity-реализация драйвера локализации и компоненты для привязки переводов к UI.

Возможности:

- Загрузка переводов из Google Sheets (TSV-формат)
- Хранение данных в ScriptableObject (`LocalizationPreset`)
- Сохранение выбранного языка через `PlayerPrefs`
- Асинхронная загрузка индекса через `IProcess` / `Loader`
- Inspector-атрибуты: выбор ключа локализации, выбор языка
- Компоненты: привязка текста, спрайта, действия, переключение языка

Вне ответственности:

- Логика поиска перевода (Core `Localization`)
- Extension-методы `Translate()` / `TryTranslate()` (Core `StringExt`)

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.LocalizationSystem` | `Localization`, `IDriver`, `StringExt` |
| `Vortex.Core.LoaderSystem` | `Loader.Register()`, `Loader.RunAlone()` |
| `Vortex.Core.AppSystem` | `App.OnStart`, `AppStates` |
| `Vortex.Unity.UI` | `UIComponent`, `UIStateSwitcher` |
| `Vortex.Unity.EditorTools` | `MultiDrawer`, `AutoLink`, `DrawingUtility` |
| `Cysharp.Threading.Tasks` | `UniTask` |

---

## Архитектура

### Драйвер

```
LocalizationDriver (Singleton<T>, IDriver, IProcess)
  ├── partial: основной    → Init, SetLanguage, GetLanguages, GetDefaultLanguage
  ├── partial: Loading     → Register, RunAsync, WaitingFor (IProcess)
  └── partial: Editor      → EditorRegister, LoadLocalizationData, RefreshIndex
```

- `Singleton<LocalizationDriver>` — один экземпляр на приложение
- Реализует `IDriver` (контракт с Core) и `IProcess` (загрузка через Loader)
- `WaitingFor() → null` — без зависимостей, загружается одной из первых

### Данные

```
LocalizationPreset (ScriptableObject)
  ├── localeDoc: string           ← ID Google Sheets документа
  ├── sheets: string[]            ← GID листов
  ├── langs: string[]             ← зафиксированные языки (HideInInspector)
  └── localeData: LocalePreset[]  ← массив переводов (HideInInspector)

LocalePreset (struct)
  ├── Key: string
  └── Texts: LanguageData[]

LanguageData (struct)
  ├── Language: string
  └── Text: string
```

### Формат Google Sheets

```
KEY         | English      | Russian      | ...
MENU_START  | Start        | Начать       |
MENU_EXIT   | Exit         | Выход        |
```

- Первая строка — заголовок: столбец 0 = ключ, столбцы 1..N = языки
- Ключи приводятся к `UPPER` при загрузке
- Формат экспорта: TSV (`export?format=tsv&gid=...`)
- Поддержка нескольких листов (sheets)

### Загрузка (runtime)

1. `RuntimeInitializeOnLoadMethod` → `Register()` → загрузка `LocalizationPreset` из Resources
2. `Loader.Register(Instance)` → ставится в очередь загрузки
3. `RunAsync()` → итерация по `localeData`, заполнение индекса для `GetCurrentLanguage()`
4. `UniTask.Yield()` каждые 20 записей — разгрузка основного потока
5. `TimeController.Call(CallOnInit)` → уведомление о готовности

### Смена языка (runtime)

1. `Localization.SetCurrentLanguage(lang)` → `Driver.SetLanguage(lang)`
2. Драйвер сохраняет в `PlayerPrefs("AppLanguage")`
3. `Loader.RunAlone(this)` → перезагрузка индекса
4. `OnLocalizationChanged` → UI обновляется

---

## Контракт

### Пресеты

| Поле | Описание |
|------|----------|
| `localeDoc` | ID Google Sheets документа |
| `sheets` | GID листов для загрузки |
| `langs` | Автозаполнение — языки из заголовков TSV |
| `localeData` | Автозаполнение — массив `LocalePreset` |

### Атрибуты

| Атрибут | Тип поля | Описание |
|---------|----------|----------|
| `[LocalizationKey]` | `string` | Dropdown выбора ключа из индекса + превью перевода. Odin-drawer |
| `[Language]` | `string` | Selector языка из списка `GetLanguages()`. MultiDrawer |

### Гарантии

- Языки валидируются через `SystemLanguage` enum
- `GetDefaultLanguage()`: `PlayerPrefs` → `Application.systemLanguage` → первый из списка
- Индекс перезагружается полностью при смене языка

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| `LocalizationPreset` в `Resources/Localization/` | Загрузка через `Resources.LoadAll` |
| `SetLanguage` — `async void` | fire-and-forget при смене языка |
| Языки привязаны к `SystemLanguage` | Валидация через `Enum.TryParse` |
| Ключи — `ToUpper()` | Регистронезависимый поиск |

---

## Компоненты

### SetTextComponent

Привязка локализованного текста к `UIComponent`.

| Поле | Описание |
|------|----------|
| `key` | Ключ локализации (`[LocalizationKey]` — dropdown) |
| `useLocalization` | `true` — `key.Translate()`, `false` — ключ как есть |
| `uiComponent` | Целевой `UIComponent` (`[AutoLink]`) |
| `position` | Индекс `UIComponentText` (-1 = первый) |

Подписывается на `OnLocalizationChanged`, `App.OnStart`, `Localization.OnInit`. Работает в `ExecuteInEditMode`.

### SetSpriteComponent

Привязка спрайта к `UIComponent`.

| Поле | Описание |
|------|----------|
| `sprite` | `Sprite` для установки |
| `uiComponent` | Целевой `UIComponent` |
| `position` | Индекс `UIComponentGraphic` (-1 = первый) |

### SetActionComponent

Привязка `UnityEvent` к кнопке `UIComponent`.

| Поле | Описание |
|------|----------|
| `events` | `UnityEvent` — вызывается по нажатию |
| `uiComponent` | Целевой `UIComponent` (`[AutoLink]`) |
| `position` | Индекс `UIComponentButton` (-1 = первый) |

При `OnDisable` очищает действие (`SetAction(null)`).

### SetLocaleHandler

Кнопка переключения языка.

| Поле | Описание |
|------|----------|
| `uiComponent` | `UIComponent` — кнопка + текст |
| `language` | Целевой язык (`[Language]` — selector) |
| `useSwitch` | Показывать `SwitcherState.On/Off` для текущего языка |

Вызов `Run()` → `Localization.SetCurrentLanguage(language)`. Текст кнопки — перевод имени языка.

---

## Редакторные инструменты

### Меню

| Пункт | Описание |
|-------|----------|
| `Vortex/Localization/Load data` | Загрузить данные из Google Sheets в `LocalizationPreset` |
| `Vortex/Localization/Update index` | Перестроить индекс из текущего пресета |
| `Vortex/Localization/Set Default Locale` | Сбросить язык на системный/сохранённый |
| `Vortex/Localization/Set Next Locale` | Переключить на следующий язык циклически |

### LocalizationPreset Inspector

- Кнопка `Load Data` — загрузка из Google Sheets
- Dropdown `Debug` — просмотр перевода по ключу
- InfoBox — список зафиксированных языков
- Кнопка `Check System Language` — лог текущего `Application.systemLanguage`

### Автосоздание ассета

`EditorRegister` (InitializeOnLoadMethod) автоматически создаёт `Resources/Localization/LocalizationData.asset` если файл отсутствует.

---

## Использование

### Настройка

1. Создать Google Sheets с переводами (формат: KEY | English | Russian | ...)
2. Вставить ID документа и GID листов в `LocalizationPreset`
3. Нажать `Load Data` в Inspector
4. Убедиться, что `LocalizationPreset` в `Resources/Localization/`

### Привязка текста к UI

1. Добавить `UIComponent` на GameObject
2. Добавить `SetTextComponent` на тот же GameObject
3. Выбрать ключ через dropdown `[LocalizationKey]`
4. Включить `useLocalization` для перевода

### Кнопка смены языка

1. Добавить `UIComponent` (с кнопкой и текстом)
2. Добавить `SetLocaleHandler`
3. Выбрать язык через `[Language]` selector
4. Включить `useSwitch` для визуального выделения текущего языка

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Google Sheets недоступен при `Load Data` | `LogError`, лист пропускается |
| Язык из TSV не найден в `SystemLanguage` | `LogError`, язык пропускается |
| `PlayerPrefs` содержит язык, отсутствующий в списке | Fallback на первый язык из списка |
| `LocalizationPreset` не найден в Resources | `LogError`, драйвер не регистрируется |
| Дублирование ключа в разных листах | `AddNew` логирует предупреждение |
| `SetTextComponent` без `UIComponent` | `LogError` в `OnEnable` |
| `SetLanguage` во время загрузки | `Loader.RunAlone` запускает перезагрузку индекса |

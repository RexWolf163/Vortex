# LocalizationSystem (NaniExtensions)

Драйвер локализации для проектов на Naninovel.

## Назначение

Замена стандартного `LocalizationDriver` (Unity) драйвером, интегрированным с Naninovel. Расширяет систему локализации каналами языков и связывает Vortex-локализацию с Naninovel `ILocalizationManager`.

- Загрузка переводов из файловой структуры Naninovel
- Три канала локализации: Default (общий), Dialogue, Voice — с независимым выбором языка
- Синхронизация языка с Naninovel (`ILocalizationManager`, `VoiceLoader`)
- Сохранение языковых предпочтений по каналам через `PlayerPrefs`
- UI-компонент выбора языка с фильтрацией по каналу

Вне ответственности: формат файлов локализации Naninovel (см. документацию Naninovel), логика поиска перевода (Core `Localization`).

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Core.LocalizationSystem` | `Localization`, `IDriver`, `StringExt` |
| `Vortex.Core.LoaderSystem` | `Loader.Register()`, `Loader.RunAlone()` |
| `Vortex.Core.AppSystem` | `App.OnStart` |
| `Vortex.Unity.UI.Misc` | `DropDownComponent` |
| `Naninovel` | `ILocalizationManager`, `LocalizationConfiguration`, `LocalizableResourceLoader` |
| `Vortex.NaniExtensions.Core` | `NaniWrapper` |
| `Cysharp.Threading.Tasks` | `UniTask` |

---

## Архитектура

### Драйвер

```
LocalizationDriver (Singleton, IDriver, IChanneledDriver, IProcess)
├── LocalizationDriver.cs              — Init, SetLanguage, GetLanguages, каналы
├── LocalizationDriverExtLoading.cs    — RuntimeInitialize, RunAsync (IProcess)
└── Editor/LocalizationDriverExtEditor.cs — EditorRegister, меню, RefreshIndex
```

Реализует `IDriver` (контракт с Core) и `IChanneledDriver` (per-channel языки). Подменяет стандартный Unity-драйвер через `Localization.SetDriver()`.

События драйвера:

| Событие | Описание |
|---------|----------|
| `OnLocalizationChanged` | Вызывается после `SetLanguage` или любого `SetChannelLanguage` |
| `OnLocalizationInChannelChanged(byte channel)` | Вызывается после `SetChannelLanguage`, передаёт номер канала |
| `OnInit` | Вызывается после завершения асинхронной загрузки данных (`RunAsync`) |

### Каналы

```
LocaleChannels
├── Default = 0   — язык интерфейса/общий (Vortex-индекс, делегирует в Driver.SetLanguage)
├── Dialogue = 1  — язык диалогов (Naninovel ILocalizationManager)
└── Voice = 2     — язык озвучки (Naninovel VoiceLoader)
```

Только канал Default перезагружает Vortex-индекс при смене языка. Dialogue и Voice сохраняют предпочтение в `PlayerPrefs` и передают настройку Naninovel.

Сохранение: `PlayerPrefs("AppLanguage")` — общий, `PlayerPrefs("AppLanguage{channel}")` — per-channel.

### Расширение Core

`LocalizationExtVoices.cs` — partial на `Localization` (живёт в `Sdk/AudioLocalizationSystem/LocalizationExt/`, через `ru.vortex.localization.ext.asmref` компонуется в `ru.vortex.localization`):

| Метод | Описание |
|-------|----------|
| `GetCurrentChannelLanguage(LocaleChannels channel)` | Текущий язык канала. Для `Default` делегирует в `GetCurrentLanguage()` базовой партиальной — Default-канал является синонимом базового `_currentLanguage`, отдельного состояния не имеет. Для остальных — lazy: in-memory cache → `IChanneledDriver.GetChannelLanguage` → `Driver.GetDefaultLanguage` |
| `SetCurrentChannelLanguage(string language, LocaleChannels channel)` | Для `Default` делегирует в `SetCurrentLanguage(language)` — обновляет базовое `_currentLanguage` и дёргает `Driver.SetLanguage`. Для остальных — пишет в per-channel слот и вызывает `IChanneledDriver.SetChannelLanguage` (с `.Forget`-логгером) |

Хранение per-channel — массив `string[]`, индексируемый `(byte)channel`. Слот `Default` (индекс 0) не используется — Default делегируется в базовое состояние. Доступ к канальным операциям через `ChDriver` — кастит `IDriver` к `IChanneledDriver`. Если драйвер не поддерживает каналы, getter возвращает `Driver.GetDefaultLanguage()`, setter сохраняет только локальное значение.

### NaniVortexLocaleConnector

Статический мост. При `App.OnStart` подписывается на `OnLocalizationChanged` и выполняет начальную синхронизацию:

- `SetNaniDialogueLocale()` → `l10n.SelectLocale(dialogueLanguage)` + вызов `SetNaniVoiceLocale()`
- `SetNaniVoiceLocale()` → `voiceLoader.OverrideLocale = voiceLanguage`
- Оба метода вызывают `StateManager.SaveGlobal()`
- При `AppStates.Stopping` — автоотписка, методы прерываются

Смена языка диалогов всегда обновляет и озвучку через внутренний вызов `SetNaniVoiceLocale()`.

---

## Данные

### LocalizationPreset

`ScriptableObject` в `Resources/Localization/`.

| Поле | Описание |
|------|----------|
| `path` | Путь к папкам локализации Naninovel (относительно `Assets/`) |
| `files` | Базовые TextAsset с ключами (формат `key:value`) |
| `languages` | TextAsset со списком языков (формат `key:Name`) |
| `defaultLanguage` | Язык по умолчанию (`[Language]` selector) |
| `langs` | Автозаполнение — полные имена языков |
| `langsKeys` | Автозаполнение — ключи языков |
| `localeData` | Автозаполнение — массив `LocalePreset` |

### LocalePreset

| Поле | Тип | Описание |
|------|-----|----------|
| `Key` | `string` | Ключ перевода |
| `Texts` | `IReadOnlyList<LanguageData>` | Переводы по языкам |

`SetLangData(LanguageData)` — internal, обновляет существующий перевод или добавляет новый.

### Структура файлов

```
Assets/
├── Resources/Localization/
│   └── LocalizationDataNaninovell.asset   ← LocalizationPreset
└── {path}/                                ← папки локализации Naninovel
    ├── en/
    │   ├── file1.txt                      ← переводы (key:value)
    │   └── file2.txt
    ├── ru/
    │   ├── file1.txt
    │   └── file2.txt
    └── ...
```

Имена файлов в языковых папках должны совпадать с именами базовых `files[]`.

### Логика парсинга (Editor)

Двухпроходный парсинг для каждого языкового файла:
1. **Первый проход** — по базовому файлу (`files[]`): заполняет ключи переводом из языковой папки
2. **Второй проход** — по файлу из языковой папки: перезаписывает переводы, валидирует ключи

Рекурсивный обход папок: если директория не является языковым ключом (не найдена в `languages`), парсер спускается глубже.

---

## Компоненты

### NaniLocaleHandler

UI-компонент выбора языка по каналу. Работает через `DropDownComponent`.

| Поле | Описание |
|------|----------|
| `whiteList` | Фильтр доступных языков (`[ValueDropdown]` из `LocalizationConfiguration`) |
| `dropdown` | `DropDownComponent` для выбора |
| `mode` | `LocaleChannels` — Default, Dialogue или Voice |

Источник языков: `ILocalizationManager.AvailableLocales`, отфильтрованные через `whiteList`.

Labels в dropdown: `lang.ToUpper().Translate()` — ключ языка переводится через Vortex-локализацию.

При выборе вызывается `Localization.SetCurrentChannelLanguage(selectedLocale, mode)` — для канала `Default` это эквивалентно `SetCurrentLanguage`, для `Dialogue`/`Voice` сохраняет per-channel предпочтение и проталкивает в Nani.

Подписывается на `OnLocalizationChanged` (`OnEnable`/`OnDisable`) для синхронизации dropdown.

---

## Редакторские инструменты

| Пункт меню | Описание |
|------------|----------|
| `Vortex/Localization/(Nani) Load data` | Парсинг файлов Naninovel → заполнение `LocalizationPreset` |

Inspector `LocalizationPreset`:
- Drag-and-drop `folder` для быстрого заполнения `path`
- Dropdown `Debug` — просмотр перевода по ключу
- Кнопка `Check System Language`

Пункт меню доступен только после успешной инициализации драйвера (валидация `_isSet`).

Автосоздание ассета при `InitializeOnLoadMethod` если `LocalizationPreset` отсутствует в Resources.

---

## Использование

### Настройка

1. Убедиться, что структура папок локализации Naninovel настроена
2. Открыть `LocalizationPreset` в `Resources/Localization/`
3. Указать `path` (или перетащить папку), назначить `files` и `languages`
4. Выбрать `defaultLanguage`
5. `Vortex/Localization/(Nani) Load data`

### UI настроек языка

1. Добавить `NaniLocaleHandler` на GameObject
2. Назначить `DropDownComponent`
3. Выбрать `mode` (Default / Dialogue / Voice)
4. Настроить `whiteList` — доступные языки для этого канала

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `SetLanguage` с тем же языком (совпадение с PlayerPrefs) | Ранний выход, индекс не перезагружается |
| `SetChannelLanguage` для Dialogue/Voice | Только сохранение в PlayerPrefs, без перезагрузки индекса |
| `SetChannelLanguage` для Default (channel 0) | Перезагрузка индекса + `OnLocalizationChanged` |
| Язык канала не установлен | Lazy-инициализация: channel PlayerPrefs → default PlayerPrefs → system → fallback |
| `LocalizationPreset` не найден | `LogError`, драйвер не регистрируется |
| Файл языковой папки отсутствует в `files[]` | Молча пропускается |
| Ключ перевода отсутствует в языковой папке | `LogError` с указанием ключа и языка |
| Ключ не найден в индексе при парсинге | `LogError` "Wrong key" |
| Пустой перевод | `LogError`, ключ пропускается |
| Язык не найден в списке, перевод отсутствует | Fallback на первый доступный перевод (`Texts[0]`) |
| Повторный `Load Data` во время загрузки | Блокируется флагом `_run` |
| `App.OnStart` при отсутствии Naninovel Engine | Exception в `Engine.GetServiceOrErr` |
| `AppStates.Stopping` | Коннектор отписывается, синхронизация прекращается |

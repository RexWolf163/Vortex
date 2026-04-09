# AudioLocalizationSystem

**Namespace:** `Vortex.Sdk.AudioLocalizationSystem`
**Assembly:** `ru.vortex.sdk.localization.audio`

## Назначение

Связывает текстовые ключи локализации с озвученными аудиоклипами. Для каждого ключа хранится карта «язык → звуковой ассет», при воспроизведении автоматически выбирается клип текущего языка.

Возможности:
- Маппинг ключей локализации на звуковые ассеты по языкам
- Кеширование `SoundClipFixed` — пересоздаётся только при смене языка
- Воспроизведение через выделенный аудиоканал с автоматической остановкой предыдущего клипа
- Батчинг индексации при загрузке (`IProcess`, `UniTask.Yield`)

Вне ответственности:
- Управление аудиоканалами (используется `AudioController`)
- Определение текущего языка (используется `Localization`)
- Хранение и загрузка звуковых ассетов (используется `Database` + `Sound`)

## Зависимости

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`
- `Vortex.Core.System.ProcessInfo` — `IProcess`, `ProcessData`
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`
- `Vortex.Core.AudioSystem` — `AudioController`
- `Vortex.Core.LocalizationSystem` — `Localization.GetCurrentVoiceLanguage()`
- `Vortex.Core.LoaderSystem` — `Loader.Register`
- `Vortex.Core.Extensions` — `ActionExt`, `ReactiveValues`

### Unity
- `Vortex.Unity.AudioSystem` — `Sound`, `SoundClipFixed`
- `Vortex.Unity.DatabaseSystem` — `RecordPreset<T>`, `DbRecordAttribute`
- `Vortex.Unity.LocalizationSystem` — `LanguageAttribute`
- `Vortex.Unity.EditorTools` — `ClassFilter`, `AutoLink`, `ClassLabel`
- `Cysharp.Threading.Tasks` — `UniTask`
- `Sirenix.Utilities` — строковые расширения (Odin Inspector)

## Архитектура

```
AudioLocalizationController (Singleton, IProcess)
├── Index: Dictionary<string, AudioLocaleData>   ← TextGuid → данные
├── PlayForText(string)                          ← воспроизведение по ключу
└── RunAsync()                                   ← индексация при загрузке

AudioLocaleData (Record)
├── TextGuid: string                             ← ключ локализации
├── Voices: Dictionary<string, string>           ← язык → GUID звука
├── GetLocale() → Sound                          ← сырой пресет для текущего языка
└── GetSoundClip() → SoundClipFixed              ← кешированный клип для воспроизведения

AudioLocaleDataPreset (RecordPreset<AudioLocaleData>)
├── textGuid [LocalizationKey]                   ← привязка к строке локализации
└── voices: List<LangGroup>                      ← пары язык + звуковой ассет

AudioLocaleHandler (MonoBehaviour)
└── IDataStorage → StringData → PlayForText()    ← автовоспроизведение при изменении текста
```

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `AudioLocalizationController` | `Singleton<T>`, `IProcess` | Индексация и воспроизведение озвучки по ключу |
| `AudioLocaleData` | `Record` | Данные маппинга: ключ → языки → звуки, кеш `SoundClipFixed` |
| `AudioLocaleDataPreset` | `RecordPreset<AudioLocaleData>` | ScriptableObject-пресет для настройки в редакторе |
| `AudioLocaleHandler` | `MonoBehaviour` | Автовоспроизведение озвучки при изменении `StringData` из `IDataStorage` |

## Контракт

### Вход
- `Database` — записи `AudioLocaleData` загружены до вызова `RunAsync`
- `Localization.GetCurrentVoiceLanguage()` — текущий язык озвучки
- `IDataStorage` + `StringData` — источник текстового ключа для `AudioLocaleHandler`

### Выход
- `AudioLocalizationController.PlayForText(string)` — воспроизвести озвучку для текстового ключа
- `AudioLocaleData.GetLocale()` — получить `Sound` для текущего языка
- `AudioLocaleData.GetSoundClip()` — получить кешированный `SoundClipFixed`

### Гарантии
- `PlayForText` останавливает предыдущий клип на канале перед воспроизведением нового
- `SoundClipFixed` пересоздаётся только при смене языка — повторные вызовы возвращают кеш
- `RunAsync` батчит индексацию по 50 записей за кадр
- `WaitingFor() → Database` — загрузка начнётся только после готовности БД

### Ограничения
- Один голосовой клип одновременно (канал очищается целиком)
- Odin Inspector обязателен (`Sirenix.Utilities`)
- `AudioLocaleData` не сохраняется (`GetDataForSave() → null`)

## Использование

### Создание пресета

1. `Create → Database → AudioLocaleData`
2. Указать `textGuid` — ключ строки локализации
3. Заполнить `voices` — пары «язык → звуковой ассет (`Sound`)»

### Воспроизведение из кода

```csharp
// По текстовому ключу
AudioLocalizationController.PlayForText("quest_intro_text_guid");
```

### Воспроизведение через Handler

1. Добавить `AudioLocaleHandler` на GameObject
2. Указать `source` — компонент с `IDataStorage`, предоставляющий `StringData`
3. При изменении `StringData.Value` озвучка воспроизведётся автоматически

### Доступ к данным напрямую

```csharp
var data = Database.GetRecord<AudioLocaleData>(guid);

// Сырой Sound пресет
Sound sound = data.GetLocale();

// Готовый к воспроизведению клип (кешируется)
SoundClipFixed clip = data.GetSoundClip();
```

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `PlayForText` с незарегистрированным ключом | Игнорируется (early return) |
| Нет озвучки для текущего языка | `GetLocale()` → `null`, `GetSoundClip()` → `null`, звук не воспроизводится |
| Смена языка между вызовами | Кеш `SoundClipFixed` пересоздаётся при следующем обращении |
| Повторный вызов без смены языка | Возвращается кешированный `SoundClipFixed` |
| `GetLocale()` без смены языка | Возвращает свежий `Sound` из БД, кеш не затрагивается |
| Дублирование `language` в пресете | `ToDictionary` кинет `ArgumentException` при загрузке |
| `StringData.Value` == null/empty | `AudioLocaleHandler` игнорирует (проверка `IsNullOrWhitespace`) |
| Отмена загрузки (`CancellationToken`) | Индексация прерывается, `Index` может быть неполным |

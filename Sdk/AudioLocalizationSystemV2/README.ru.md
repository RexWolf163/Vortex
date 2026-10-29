# AudioLocalizationSystemV2

**Namespace:** `Vortex.Sdk.AudioLocalizationSystemV2`
**Assembly:** `ru.vortex.sdk.localization.audio.v2`

Преемник `AudioLocalizationSystem` (V1, помечен `[Obsolete]`). Отличие — клипы реплик выгружаемые: это собственные ассеты записи за `AssetHandle<AudioClip>`, а не записи `Sound` в БД, и держатся в памяти только на время проигрывания.

## Назначение

Связывает текстовые ключи локализации с озвученными аудиоклипами. Для каждого ключа хранится карта «язык → звуковой ассет», при воспроизведении автоматически выбирается клип текущего языка.

Возможности:
- Маппинг ключей локализации на звуковые ассеты по языкам
- Клип грузится на время проигрывания и отпускается по его завершению (`AudioSampleWrapper.OnFinished`)
- Выбор хранения клипа в пресете: прямая ссылка (`DirectAssetHandle`) или ленивая подгрузка через Addressables (`AddressableAssetHandle`, с группой `AssetCache`)
- Воспроизведение через выделенный аудиоканал с автоматической остановкой предыдущего клипа
- Батчинг индексации при загрузке (`IProcess`, `UniTask.Yield`)

Вне ответственности:
- Управление аудиоканалами (используется `AudioController`)
- Определение текущего языка (используется `Localization`)
- Механика загрузки/кэширования ассетов (используется `AssetHandle<T>` и его реализации; для Addressables — `AssetCache`)
## Выбор версии (V1 / V2)

Версия выбирается тогглом `audioLocalizationV2` в `SdkSettings` (partial — `DefineSettings/SdkSettings.AudioLocalization.cs`). Тоггл управляет define-символом `USING_VORTEX_AUDIO_LOCALIZATION_V2`:

| Что | Выключен (V1) | Включён (V2) |
|---|---|---|
| Сборка `ru.vortex.sdk.localization.audio` (V1) | компилируется | вырезана (`defineConstraints: !USING_VORTEX_AUDIO_LOCALIZATION_V2`) |
| Сборка `ru.vortex.sdk.localization.audio.v2` | вырезана (`defineConstraints: USING_VORTEX_AUDIO_LOCALIZATION_V2`) | компилируется |
| `LocalizationExt` (канальные расширения `Localization`) | копия из V1 | копия из V2 |

`LocalizationExt` — `LocaleChannels`, `IChanneledDriver`, `StringExtChannel` и partial `Localization` — встраивается через asmref в сборку локализации, поэтому `defineConstraints` пакета на него не действует. Копии лежат в обоих пакетах, каждая экранирована `#if` по тому же символу — активна ровно одна. Потребитель (`NaniExtensions/LocalizationSystem`: `LocalizationDriver : IChanneledDriver`, `NaniVortexLocaleConnector`, `NaniLocaleHandler`) правок не требует — типы есть при любой версии. Копии держатся одинаковыми: правка канальных расширений вносится в обе.

## Зависимости

### Core
- `Vortex.Core.System.Abstractions` — `Singleton<T>`
- `Vortex.Core.System.ProcessInfo` — `IProcess`, `ProcessData`
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`
- `Vortex.Core.AudioSystem` — `AudioController.PlaySoundWithControl`, `AudioSampleWrapper`
- `Vortex.Core.AssetHandleSystem` — `AssetHandle<T>`
- `Vortex.Core.LocalizationSystem` — `Localization.GetCurrentChannelLanguage(LocaleChannels.Voice)`
- `Vortex.Core.LoaderSystem` — `Loader.Register`
- `Vortex.Core.Extensions` — `ActionExt`, `ReactiveValues`

### Unity
- `Vortex.Unity.AudioSystem` — `SoundClipFixed`, `AudioChannelNameAttribute`
- `Vortex.Unity.DatabaseSystem` — `RecordPreset<T>`
- `Vortex.Unity.LocalizationSystem` — `LanguageAttribute`
- `Vortex.Unity.EditorTools` — `ClassFilter`, `AutoLink`, `ClassLabel`
- `Cysharp.Threading.Tasks` — `UniTask`
- `Sirenix.Utilities` — строковые расширения (Odin Inspector)

Реализации `AssetHandle<T>` (`DirectAssetHandle` в `Vortex.Unity.AssetHandleSystem`, `AddressableAssetHandle` в `Vortex.Unity.AssetCacheSystem`) пакет напрямую не использует — их выбирает дизайнер в пресете.

## Использование

### Создание пресета

1. `Create → Database → AudioLocaleDataV2`
2. Указать `textGuid` — ключ строки локализации
3. Указать `channel` — аудиоканал реплики
4. Заполнить `voices` — пары «язык → хэндл клипа»: выбрать реализацию хэндла (`DirectAssetHandle` / `AddressableAssetHandle`) и клип; для addressable — группу кэша
5. Проверить блок ошибок над `voices`: инспектор сообщает о пустом ключе, пустом канале, пустом или повторном языке, незаданном хэндле и хэндле без клипа (для `DirectAssetHandle` / `AddressableAssetHandle`). Проверка пересчитывается не чаще раза в секунду

### Воспроизведение из кода

```csharp
// По текстовому ключу
AudioLocalizationController.PlayForText("quest_intro_text_guid");
```

### Воспроизведение через Handler

1. Добавить `AudioLocaleHandler` на GameObject
2. Указать `source` — компонент с `IDataStorage`, предоставляющий `StringData`
3. При изменении `StringData.Value` озвучка воспроизведётся автоматически. Уже озвученное значение повторно не играет, пока не сменится: повторное включение компонента (скрытие/показ UI) или перепривязка хранилища реплику не перезапускают. Смена на пустое значение тоже считается сменой — после неё та же реплика озвучится снова

### Доступ к данным напрямую

```csharp
var data = Database.GetRecord<AudioLocaleData>(guid);

// Хэндл клипа для текущего языка. Load/Release — забота держателя: хэндл записи общий,
// и его же использует AudioLocalizationController.
AssetHandle<AudioClip> handle = data.GetLocale();
```

### Editor-инструмент «Грубый инструментарий массовой замены»

В инспекторе `AudioLocaleDataPreset` есть свёртываемая группа `Грубый инструментарий массовой замены`. Это **Editor-only** хелперы для частых сценариев пересборки озвучки реплик. Все три действия пишут `EditorUtility.SetDirty` — изменения попадут в коммит.

Клипы ищутся по точному имени файла `AudioClip` в проекте (`AssetDatabase`, без учёта регистра). Хэндл перенацеливается с сохранением своей реализации: у `DirectAssetHandle` меняется ссылка на клип, у `AddressableAssetHandle` — GUID ассета в `AssetReference` (группа кэша сохраняется). Если клип ещё не addressable — его нужно пометить addressable вручную.

| Кнопка | Что делает | Когда полезно |
|---|---|---|
| `Update Voices Array` | Для каждого `voice` берёт текущий клип хэндла, проверяет наличие точки в его имени. Затем ищет клип с именем `"{TextGuid}.{voice.language}"` и перенацеливает на него хэндл. | После переименования `textGuid` пресета — массово пересобрать ссылки на звук под новый ключ |
| `Замена первой части ключа` | Принимает поле `charName` и для каждого `voice` ищет клип с именем `"{charName}.{oldSuffix}"`, где `oldSuffix` — остаток имени старого клипа после первой точки. Перенацеливает хэндл на найденный. Дополнительно перевешивает `textGuid` пресета на ключ локализации `"{charName}.{Name}"` (через `Localization.GetLocalizationKeys()`, case-insensitive). | Скопировать пресет под другого персонажа: меняешь префикс — реплика автоматически перевешивается на его озвучку |
| `Скопировать первую запись под другой язык` | Принимает поле `_newLanguage`. Берёт первый `voice` как шаблон, заменяет в имени его клипа подстроку `.{templateLang}` на `.{newLanguage}`, находит соответствующий клип и добавляет новую запись `voice` для `_newLanguage` с хэндлом той же реализации. Если для языка уже есть запись — действие пропускается; если клип не найден — `InvalidOperationException`. | Добавить новый язык: первая локализация уже сделана, под остальные нужны заглушки в одно нажатие |

**Конвенция именования**, которую инструмент эксплуатирует:
- Имя файла клипа имеет вид `Character.{anything}.{lang}.{index}` или похожий — с языком в одной из позиций между точками.
- `textGuid` имеет вид `Character.LineKey`.
- `voice.language` соответствует коду языка из `Localization`.

При нарушении конвенции инструмент **молча пропускает** запись (проверяет наличие точки в имени, существование клипа с нужным именем, etc.) — пакет не упадёт, но и эффекта не даст. Исключение — копирование под язык: ненайденный клип бросает исключение. Это сделано осознанно: «грубый инструментарий» по контракту — best-effort, ручной просмотр результата обязателен.

## Архитектура

```
AudioLocalizationController (Singleton, IProcess)
├── Index: Dictionary<string, AudioLocaleData>   ← TextGuid → данные
├── PlayForText(string)                          ← загрузка + воспроизведение по ключу
└── RunAsync()                                   ← индексация при загрузке

AudioLocaleData (Record)
├── TextGuid: string                             ← ключ локализации
├── Voices: Dictionary<string, AssetHandle<AudioClip>>  ← язык → хэндл клипа
├── Channel: string                              ← аудиоканал реплики
└── GetLocale() → AssetHandle<AudioClip>         ← хэндл для текущего языка

AudioLocaleDataPreset (RecordPreset<AudioLocaleData>)
├── textGuid [LocalizationKey]                   ← привязка к строке локализации
├── channel [AudioChannelName]                   ← аудиоканал
└── voices: List<LangGroup>                      ← пары язык + [SerializeReference] хэндл клипа

AudioLocaleHandler (MonoBehaviour)
└── IDataStorage → StringData → PlayForText()    ← автовоспроизведение при изменении текста
```

### Поток `PlayForText(text)`

```
1. Index[text] → data; нет — выход
2. data.GetLocale() → handle; нет озвучки языка — выход
3. handle уже грузится (повторный запрос той же реплики) — выход
4. канал data.Channel не найден — LogError, выход
5. AudioController.StopAllSounds(channel)   ← звучащая реплика завершается → её OnFinished отпускает клип
6. отмена загрузки предыдущей реплики (CancellationToken)
7. await handle.LoadAsync(ct)
   ├── отмена → выход (владение снято кэшем)
   ├── ошибка загрузки → LogError, выход
   └── пустой клип → LogError, handle.Release(), выход
8. PlaySoundWithControl(new SoundClipFixed(clip, false, channel)) → wrapper
   └── null → LogError, handle.Release()
9. wrapper.OnFinished += handle.Release     ← естественный конец / вытеснение / стоп канала
```

### Компоненты

| Класс | Тип | Назначение |
|-------|-----|-----------|
| `AudioLocalizationController` | `Singleton<T>`, `IProcess` | Индексация, загрузка и воспроизведение озвучки по ключу, отпуск клипа по завершению |
| `AudioLocaleData` | `Record` | Данные маппинга: ключ → языки → хэндлы клипов, канал |
| `AudioLocaleDataPreset` | `RecordPreset<AudioLocaleData>` | ScriptableObject-пресет для настройки в редакторе |
| `AudioLocaleHandler` | `MonoBehaviour` | Автовоспроизведение озвучки при изменении `StringData` из `IDataStorage` |

## Контракт

### Вход
- `Database` — записи `AudioLocaleData` загружены до вызова `RunAsync`
- `Localization.GetCurrentChannelLanguage(LocaleChannels.Voice)` — текущий язык озвучки
- `IDataStorage` + `StringData` — источник текстового ключа для `AudioLocaleHandler`

### Выход
- `AudioLocalizationController.PlayForText(string)` — воспроизвести озвучку для текстового ключа
- `AudioLocaleData.GetLocale()` — получить хэндл клипа для текущего языка

### Гарантии
- `PlayForText` останавливает предыдущий клип на канале перед воспроизведением нового
- Клип отпускается (`AssetHandle.Release`) при любом завершении звука: естественный конец, вытеснение следующей репликой, остановка канала; а также если воспроизведение не стартовало
- Реплика, вытесненная во время загрузки, не заиграет
- `RunAsync` батчит индексацию по 50 записей за кадр
- `WaitingFor() → Database` — загрузка начнётся только после готовности БД

### Ограничения
- Один голосовой клип одновременно (канал очищается целиком)
- Задержка старта: для `AddressableAssetHandle` голос стартует после асинхронной загрузки клипа (для HIT из survivors `AssetCache` — сразу)
- Odin Inspector обязателен (`Sirenix.Utilities`)
- `AudioLocaleData` не сохраняется (`GetDataForSave() → null`)

### Выгрузка и группа кэша

Пакет только зовёт `LoadAsync`/`Release` хэндла. Что значит «выгрузить» — решает реализация:
- `DirectAssetHandle` — no-op: клип живёт вместе с пресетом (поведение как в V1);
- `AddressableAssetHandle` — `AssetCache.Release` в группе, выбранной в поле `group` хэндла. Реальная выгрузка — по ёмкости survivors этой группы (0 — сразу после `Release`). Для реплик стоит завести отдельную группу в `AssetCacheSettings`, чтобы поток реплик не вытеснял survivors остальных ассетов.

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `PlayForText` с незарегистрированным ключом | Игнорируется (early return) |
| Нет озвучки для текущего языка | `GetLocale()` → `null`, звук не воспроизводится |
| Канал реплики не задан / не найден | `Debug.LogError`, звук не воспроизводится |
| Повторный `PlayForText` той же реплики во время загрузки | Игнорируется — реплика заиграет по окончании загрузки |
| Повторный `PlayForText` той же реплики во время звучания | Звучащая останавливается (клип отпущен), клип грузится заново (для addressable — HIT, если ещё в survivors) |
| Новая реплика во время загрузки предыдущей | Загрузка предыдущей отменяется, она не заиграет |
| Реплика A запрошена снова, пока её вытесненная загрузка ещё в полёте (A → B → A) | Новый цикл загрузки. Отменённая загрузка снимает в `AssetCache` только своё удержание, клип держит звучащая реплика |
| Ошибка загрузки клипа | `Debug.LogError`, звук не воспроизводится |
| Смена языка между вызовами | Следующий вызов берёт хэндл нового языка |
| Дублирование `language` в пресете | Ошибка валидации в инспекторе; при загрузке `ToDictionary` кинет `ArgumentException` |
| Хэндл озвучки для языка не задан | Ошибка валидации в инспекторе; `Debug.LogError` при индексации; для этого языка звука нет |
| Хэндл без клипа (`DirectAssetHandle` с пустым `asset`) | Ошибка валидации в инспекторе; `Debug.LogError` при воспроизведении, хэндл отпускается |
| Воспроизведение не запустилось (`PlaySoundWithControl` → `null`) | `Debug.LogError`, хэндл отпускается |
| `StringData.Value` == null/empty | `AudioLocaleHandler` игнорирует (проверка `IsNullOrWhitespace`) |
| Повторное включение `AudioLocaleHandler` / перепривязка хранилища с тем же значением | Повторно не воспроизводится |
| `AudioLocaleHandler` получил значение до окончания индексации | `PlayForText` его игнорирует, но значение считается озвученным — заиграет только после следующей смены |
| Отмена загрузки (`CancellationToken`) | Индексация прерывается, `Index` может быть неполным |

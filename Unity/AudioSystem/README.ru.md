# AudioSystem (Unity)

Unity-реализация драйвера аудиосистемы.

## Назначение

Платформенная адаптация `AudioProvider`: воспроизведение звуков и музыки через `AudioSource`, пул звуков, fade-переходы, ситуативная музыка, система каналов, сохранение настроек, сцено-ориентированные компоненты.

- Воспроизведение звуков через пул с автоматическим освобождением
- Воспроизведение музыки с fade in/out через `AsyncTween`
- Ситуативная музыка с автоматическим возвратом к основной теме
- Именованные каналы с независимой громкостью и mute
- Сохранение/загрузка настроек через `PlayerPrefs`
- Компоненты для инспектора: хэндлеры звуков, музыки, UI-переключатели, слайдеры каналов

Вне ответственности: пространственный звук (3D), одновременное микширование нескольких музыкальных дорожек, динамическая подгрузка аудиоресурсов.

## Зависимости

- `Vortex.Core.AudioSystem` — шина `AudioProvider`, модели, `IDriver`
- `Vortex.Core.DatabaseSystem` — `Database`, `Record`
- `Vortex.Unity.AppSystem.System.TimeSystem` — `TimeController` (отложенные вызовы, пул-очистка)
- `Vortex.Unity.UI.PoolSystem` — `Pool` (пул звуковых источников)
- `Vortex.Unity.UI.TweenerSystem.UniTaskTweener` — `AsyncTween` (fade-анимации)
- `Vortex.Unity.DatabaseSystem` — `RecordPreset`, атрибут `[DbRecord]`
- `Sirenix.OdinInspector` — редакторские атрибуты

---

## AudioDriver

Реализация `IDriver`. Partial-класс из трёх файлов.

### Архитектура

```
AudioDriver (Singleton<AudioDriver>, IDriver)
├── AudioDriver.cs              — Init/Destroy, заполнение индексов из Database, Save/LoadSettings
├── AudioDriverExtLoading.cs    — [RuntimeInitializeOnLoadMethod] авторегистрация
├── AudioDriverExtEditor.cs     — [InitializeOnLoadMethod] регистрация в редакторе
└── AudioDriverExtPlayControl.cs — делегирование IDriver-методов к AudioPlayer
```

### Контракт

**Вход:**
- Автоматическая регистрация через `[RuntimeInitializeOnLoadMethod]`
- Заполнение индексов при `Database.OnInit`
- Загрузка каналов из `AudioChannelsConfig` (Resources)

**Выход:**
- Заполненные реестры `IndexSound` / `IndexMusic` в `AudioProvider`
- Заполненный `Settings.Channels` из `AudioChannelsConfig`
- Событие `OnInit` после заполнения индексов
- Настройки в `PlayerPrefs` (ключ `AudioSettings`)

**Формат сохранения:**

```
MasterOn;MasterVol;MusicOn;MusicVol;SoundOn;SoundVol[;ChName:MuteFlag:Vol]...
```

Пример: `Y;0.8;Y;1;Y;1;dialog:Y:0.7;ambient:Y:0.5`

Значения `MuteFlag`: `Y` — не замьючен, `N` — замьючен. Числа — `CultureInfo.InvariantCulture`.

**Гарантии:**
- Настройки сохраняются при каждом изменении через подписку на `AudioProvider.OnSettingsChanged`
- Загрузка из `PlayerPrefs` с `try/catch` — при некорректных данных настройки сбрасываются к дефолтам
- Каналы из `PlayerPrefs`, не совпадающие с текущим `AudioChannelsConfig`, игнорируются
- `TimeController.RemoveCall(this)` при `Destroy()` — очистка отложенных вызовов

**Ограничения:**
- Если `AudioProvider.SetDriver` вернул `false` — экземпляр уничтожается (`Dispose()`)
- Зависит от `Database.OnInit` — индексы пусты до инициализации базы данных

---

## Каналы

### AudioChannelsConfig

`ScriptableObject` (`ICoreAsset`), размещается в Resources. Определяет список именованных каналов для проекта.

- Поле `channels: string[]` — имена каналов
- При изменении в редакторе — автоматический `AudioDriver.ResetChannels()`

Меню: `Vortex/Configs/Audio Channels Settings` — навигация к конфигу.

### AudioChannelNameAttribute

Атрибут `[AudioChannelName]` для `string`-полей. Отрисовывает dropdown со списком каналов из `AudioProvider.GetChannelsList()`.

### AudioChannelVolumeSlider

UI-слайдер громкости канала.

- Поле `channel` с атрибутом `[AudioChannelName]` — выбор канала
- `OnEnable` — читает текущую громкость канала, подписка на `onValueChanged`
- `OnDisable` — отписка
- При ненайденном канале — слайдер устанавливается в 0

Слайдер является источником изменений, не визуализатором. Внешние изменения канала не отражаются на слайдере — это осознанное решение для исключения рекурсивных обновлений.

### Канал в пресетах и хэндлерах

Канал назначается в двух точках:

- **Пресет** (`SoundSamplePreset`, `MusicSamplePreset`) — канал по умолчанию для сэмпла. Используется при воспроизведении через пул.
- **Хэндлер** (`AudioHandler`) — канал конкретного экземпляра компонента. Используется при воспроизведении через личный `AudioSource`.

При воспроизведении через `AudioPlayer` (пул) канал определяется пресетом. Параметр `defaultChannel` в `IDriver` — fallback, если канал не задан в модели звука.

---

## AudioPlayer

Центральный контроллер воспроизведения. `MonoBehaviourSingleton`, internal API.

### Архитектура

```
AudioPlayer (MonoBehaviourSingleton<AudioPlayer>)
├── pool              — Pool (пул звуковых AudioSource)
├── musicPlayer       — MusicPlayer (основная музыка)
├── musicCoverPlayer  — MusicPlayer (ситуативная музыка)
├── musicFadeTime     — float (0–3с, default 1с)
├── FadeTween         — AsyncTween (fade основной музыки)
└── FadeCoverTween    — AsyncTween (fade ситуативной музыки)
```

### Воспроизведение звуков

`PlaySound(object, bool loop, string channelOverrideName)` — pattern matching по типу:

| Тип | Поведение |
|-----|-----------|
| `string` | Поиск `Sound` в `Database` по GUID. Канал: override или из пресета |
| `Sound` | Прямой доступ к `Sample` |
| `AudioClip` | Обёртка в `SoundClipFixed` |

Создаёт `SoundClipFixed`, добавляет в пул. Для не-loop звуков — автоматическое удаление через `TimeController.Call` по длительности клипа.

`StopAllSounds(string channel)` — при `null` очищает весь пул, при указанном канале — удаляет из пула только звуки с совпадающим `Channel.Name`.

### Воспроизведение музыки

Одновременно может играть только один основной и один ситуативный трек.

**Основная музыка (`PlayMusic`):**
1. Если играет текущий трек и `fadingEnd = true` — fade out → callback → запуск нового трека
2. Если `fadingEnd = false` — мгновенная остановка → запуск
3. Новый трек запускается с fade in (при `fadingStart = true`) или мгновенно

**Ситуативная музыка (`PlayCoverMusic`):**
1. Fade out текущей ситуативной или основной музыки
2. Запуск ситуативного трека через `musicCoverPlayer`
3. При `StopCoverMusic` — ситуативный трек затухает, основная тема восстанавливается с fade in

`GetMusicClip` — pattern matching: `string`, `Music`, `SoundClip`, `AudioClip`. Все ветки создают `SoundClipFixed` с пробросом `overrideChannel`.

---

## MusicPlayer

Компонент воспроизведения музыки. Один `AudioSource`.

### Контракт

- `Play(SoundClip)` / `Play(AudioClip)` — запуск с настройками pitch/volume из клипа, сохранение канала клипа
- `Stop()` — остановка воспроизведения
- `IsPlay()` — проверка состояния
- `SetVolumeMultiplier(float)` / `GetVolumeMultiplier()` — множитель громкости (для fade)
- При `OnEnable` — подписка на `AudioProvider.OnSettingsChanged`, применение настроек
- При `OnDisable` — отписка, остановка
- Mute/unmute переключается автоматически при изменении настроек. При unmute (`mute → !mute`) вызывается `audioSource.Play()` для возобновления
- Итоговая громкость: `GetMusicVolume(channel) × clip.volume × volumeMultiplier`

---

## Модели

### SoundClip

Аудиоклип с диапазонами pitch и volume. Каждое воспроизведение — случайные значения из диапазонов.

```
SoundClip
├── AudioClips    — AudioClip[] (массив клипов для рандомизации)
├── PitchRange    — Vector2
├── ValueRange    — Vector2
├── Channel       — AudioChannel (канал звука)
├── Loop          — bool
├── GetPitch()    → Random.Range(PitchRange.x, PitchRange.y)
├── GetVolume()   → Random.Range(ValueRange.x, ValueRange.y)
└── GetClip()     → случайный из массива (или единственный)
```

Конструкторы принимают `string channelName` или `AudioChannel channel`. При `channelName` — резолв через `AudioProvider.GetChannel()`.

### SoundClipFixed

Наследник `SoundClip`. Значения pitch, volume и клип фиксируются при создании.

```
SoundClipFixed (: SoundClip)
├── AudioClip     — выбранный клип
├── GetPitch()    → фиксированное значение
├── GetVolume()   → фиксированное значение
├── GetDuration() → clip.length / |pitch| (или float.MaxValue при pitch == 0)
└── GetClip()     → фиксированный клип
```

Конструкторы поддерживают `channelOverrideName` / `channelOverride` — переопределение канала.

### Sound / Music

Типизированные обёртки для Unity:
- `Sound : SoundSample<SoundClip>` — звуковой эффект
- `Music : MusicSample<SoundClip>` — музыкальный трек

---

## Компоненты (Handlers)

### AudioHandler

Компонент воспроизведения звука. Работает с личным `AudioSource` или ретранслирует на `AudioPlayer`.

- GUID сэмпла через `[DbRecord(typeof(Sound))]`
- Канал через `[AudioChannelName]` — используется для расчёта громкости личного `AudioSource`
- При `Play()`: если `audioSource != null` — `PlayOneShot`; иначе — `AudioProvider.PlaySound`
- `SetVolumeMultiplier(float)` / `GetVolumeMultiplier()` — множитель громкости
- Итоговая громкость: `GetSoundVolume(channel) × clip.volume × volumeMultiplier`
- Итоговый mute: `!GetSoundOn(channel)`
- `OnEnable` — подписка на `AudioProvider.OnSettingsChanged`, применение настроек
- `OnDisable` — отписка, остановка воспроизведения
- Инициализация отложена до `AudioProvider.OnInit` через `TimeController.Accumulate`

### MusicHandler

Компонент запуска музыки при активации GameObject.

- GUID сэмпла через `[DbRecord(typeof(Music))]`
- `OnEnable` → запуск музыки с задержкой `UniTask.DelayFrame(2)` для гарантии порядка после `OnDisable`
- `OnDisable` → отложенная остановка через `TimeController.Call` (обход «горячего рестарта»)
- Поле `isCoverMusic` — переключение между основной и ситуативной музыкой
- Поля `fadeStart` / `fadeEnd` — управление fade-переходами
- Инициализация отложена до `AudioProvider.OnInit` через `TimeController.Accumulate`

### AudioSourceHandler

Воспроизведение звука из `IDataStorage`.

- `[RequireComponent(typeof(AudioSource))]`
- Поле `dataStorageObject` (`GameObject`) — источник `IDataStorage` (внешний объект)
- Получает `SoundClip` через `IDataStorage.GetData<SoundClip>()`
- Канал берётся из `SoundClip.Channel`
- Итоговая громкость: `GetSoundVolume(channel) × clip.volume`
- `OnEnable` → Play, `OnDisable` → Stop

### AudioSwitcher

UI-переключатель вкл/выкл.

- Работает через `UIComponent` (`SetAction`, `SetSwitcher`)
- Тип контроля: `SoundType` (Master / Sound / Music)

### AudioValueSlider

UI-слайдер громкости.

- Привязан к `UnityEngine.UI.Slider`
- Тип контроля: `SoundType` (Master / Sound / Music)
- `OnEnable` — синхронизация значения, подписка на `onValueChanged`
- `OnDisable` — отписка

---

## Пресеты

### SoundSamplePreset

`ScriptableObject` (`RecordPreset<Sound>`), меню: `Database/SoundSample`.

- `AudioClip[]` — массив клипов
- `pitchRange` / `valueRange` — диапазоны через `[MinMaxSlider]`
- `channel` — канал через `[AudioChannelName]`
- `RecordTypes.Singleton` (форсируется в `OnValidate`)
- Editor: кнопка `TestSound` — создаёт временный `AudioSource`, самоуничтожается после воспроизведения

### MusicSamplePreset

`ScriptableObject` (`RecordPreset<Music>`), меню: `Database/MusicSample`.

- Один `AudioClip`, фиксированные pitch и volume
- `channel` — канал через `[AudioChannelName]`
- `Duration` — автоматический расчёт: `clip.length / |pitch|`
- `RecordTypes.Singleton` (форсируется в `OnValidate`)
- Editor: кнопки `TestSound` / `StopSound`

---

## Использование

### 1. Настройка каналов

1. Создать `AudioChannelsConfig` в Resources
2. Задать имена каналов: `dialog`, `ui`, `ambient`, `sfx` и т.д.
3. Каналы станут доступны в dropdown `[AudioChannelName]` и в API `AudioProvider`

### 2. Создание пресетов

1. `Create → Database → SoundSample` — настроить клипы, диапазоны pitch/volume, канал
2. `Create → Database → MusicSample` — настроить клип, pitch, volume, канал
3. Зарегистрировать пресеты в базе данных (уникальный GUID)

### 3. Воспроизведение из кода

```csharp
// Звук по GUID
AudioProvider.PlaySound("explosion_01");

// Звук по экземпляру
var sound = AudioProvider.GetSample("explosion_01") as Sound;
AudioProvider.PlaySound(sound);

// Музыка
AudioProvider.PlayMusic("main_theme");

// Ситуативная музыка
AudioProvider.PlayCoverMusic("battle_theme");
AudioProvider.StopCoverMusic(); // основная тема восстановится
```

### 4. Компонент звука

Добавить `AudioHandler` на GameObject, назначить GUID сэмпла через `[DbRecord]`, выбрать канал через `[AudioChannelName]`. Опционально добавить `AudioSource` — если его нет, звук пойдёт через пул `AudioPlayer`.

### 5. Компонент музыки

Добавить `MusicHandler` на GameObject, назначить GUID через `[DbRecord(typeof(Music))]`. Музыка запускается при `OnEnable`, останавливается при `OnDisable`. Для ситуативной музыки — включить `isCoverMusic`.

### 6. UI настроек

- `AudioSwitcher` — переключатель вкл/выкл (Master / Sound / Music)
- `AudioValueSlider` — слайдер громкости (Master / Sound / Music)
- `AudioChannelVolumeSlider` — слайдер громкости канала

## Редакторские инструменты

- `SoundSamplePreset.TestSound()` — воспроизведение случайного клипа с случайными pitch/volume
- `MusicSamplePreset.TestSound()` / `StopSound()` — прослушивание музыкального трека
- `AudioHandler`, `MusicPlayer` — кнопки Play/Stop в инспекторе (Play Mode)
- `[DbRecord]` — picker сэмплов с фильтрацией по типу
- `[AudioChannelName]` — dropdown каналов
- `Vortex/Configs/Audio Channels Settings` — быстрая навигация к конфигу каналов

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| `PlaySound` с несуществующим GUID | Лог `[AudioPlayer] Unknown sound ID`, звук не воспроизводится |
| `AudioHandler` с пустым GUID | Лог `[AudioHandler] Empty Sample data.` при инициализации |
| `AudioHandler` без `AudioSource` | Звук ретранслируется через `AudioProvider.PlaySound` (пул) |
| `PlayMusic` при играющей музыке | Текущий трек fade out → новый трек fade in |
| `PlayCoverMusic` при играющей основной | Основная приглушается, ситуативная запускается |
| `StopCoverMusic` | Ситуативная затухает, основная восстанавливается с fade in |
| `AudioPlayer.Instance == null` | `PlaySound` — silent return |
| Нет настроек в `PlayerPrefs` | Значения по умолчанию (всё включено, громкость 1) |
| Некорректные данные в `PlayerPrefs` | `try/catch`, настройки сбрасываются к дефолтам |
| `MusicHandler` быстрый disable/enable | `TimeController.RemoveCall` отменяет pending stop, `UniTask.DelayFrame(2)` обеспечивает play после stop |
| pitch == 0 в `SoundClipFixed` | `GetDuration()` → `float.MaxValue` |
| `StopAllSounds(channel)` с `null` | Очищает весь пул |
| `StopAllSounds(channel)` с именем | Удаляет из пула только звуки с совпадающим `Channel.Name` |
| Канал удалён из `AudioChannelsConfig` | Старые данные канала в `PlayerPrefs` игнорируются при загрузке |
| `AudioChannelVolumeSlider` с несуществующим каналом | Слайдер устанавливается в 0, подписка не создаётся |

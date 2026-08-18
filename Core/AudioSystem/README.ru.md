# AudioSystem (Core)

Платформонезависимая шина аудиосистемы.

## Назначение

Абстрактный контроллер аудио: реестры сэмплов, трёхуровневая модель громкости (Master → тип → канал), событийная модель, интерфейс драйвера.

- Хранение и доступ к индексам звуков и музыки по GUID
- Управление настройками (громкость, вкл/выкл) раздельно для Master, звуков и музыки
- Система именованных каналов — независимые множители громкости и mute
- Делегирование воспроизведения платформенному драйверу
- Разделение звуков на основную музыку и ситуативную (cover)
- Событие изменения настроек

Вне ответственности: воспроизведение, fade-переходы, пулы источников, сохранение настроек — задача драйвера.

## Зависимости

- `Vortex.Core.System.Abstractions` — `SystemController`, `Singleton`, `ISystemDriver`, `IReactiveData`
- `Vortex.Core.DatabaseSystem` — `Record` (базовый класс сэмплов)
- `Vortex.Core.LoggerSystem` — логирование ошибок

## Архитектура

```
AudioController (SystemController<AudioController, IDriver>)
├── IndexSound       — Dictionary<string, IAudioSample>
├── IndexMusic       — Dictionary<string, IAudioSample>
├── Settings         — AudioSettings
├── OnSettingsChanged — event Action
└── → Driver         — IDriver (платформенная реализация)

AudioSettings
├── MasterVolume — float (0–1, default 1)
├── MasterOn     — bool (default true)
├── SoundVolume  — float (0–1, default 1)
├── SoundOn      — bool (default true)
├── MusicVolume  — float (0–1, default 1)
├── MusicOn      — bool (default true)
└── Channels     — Dictionary<string, AudioChannel>

AudioChannel (IReactiveData)
├── Name         — string (immutable)
├── Volume       — float (0–1, default 1)
├── Mute         — bool (default false)
├── OnUpdateData — event Action
└── ToSave() / FromSave() — сериализация "Name:MuteFlag:Volume"

Иерархия моделей:
Record → AudioSample<T> (abstract, IAudioSample)
           ├── SoundSample<T>
           └── MusicSample<T> (abstract)
```

### Трёхуровневая модель громкости

```
Итоговая громкость = MasterVolume × TypeVolume × ChannelVolume
Итоговый mute      = !MasterOn || !TypeOn || Channel.Mute
```

- `GetSoundVolume(channelName)` → `MasterVolume × SoundVolume × ChannelVolume`
- `GetMusicVolume(channelName)` → `MasterVolume × MusicVolume × ChannelVolume`
- `GetSoundOn(channelName)` → `MasterOn && SoundOn && !Channel.Mute`
- `GetMusicOn(channelName)` → `MasterOn && MusicOn && !Channel.Mute`

Если канал не указан или не найден — множитель канала = 1, mute канала = false.

### SoundType

```csharp
enum SoundType { Master, Sound, Music }
```

Используется UI-компонентами для выбора типа контроля.

### IDriver

Контракт платформенного драйвера:

| Метод | Возврат | Описание |
|-------|---------|----------|
| `SetLinks(indexSound, indexMusic, settings)` | `void` | Получение ссылок на реестры и настройки |
| `PlaySound(object, bool loop, string defaultChannel)` | `void` | Воспроизведение звука (fire-and-forget) |
| `PlaySoundWithControl(object, bool loop, string defaultChannel)` | `AudioSampleWrapper` | То же, но с хэндлом управления |
| `StopAllSounds(string channel)` | `void` | Остановка всех звуков (или по каналу) |
| `PlayMusic(object, fadingStart, fadingEnd, string defaultChannel)` | `void` | Воспроизведение основной музыки |
| `PlayMusicWithControl(object, fadingStart, fadingEnd, string defaultChannel)` | `AudioSampleWrapper` | То же, с хэндлом |
| `StopMusic()` | `void` | Остановка основной музыки |
| `PlayCoverMusic(object, fadingStart, fadingEnd, string defaultChannel)` | `void` | Воспроизведение ситуативной музыки |
| `PlayCoverMusicWithControl(object, fadingStart, fadingEnd, string defaultChannel)` | `AudioSampleWrapper` | То же, с хэндлом |
| `StopCoverMusic()` | `void` | Остановка ситуативной музыки |

Параметр `object` — платформенный тип аудиоданных. Типизация определяется драйвером через pattern matching. Параметр `defaultChannel` — fallback-канал, если канал не задан в модели звука. Про `*WithControl` и `AudioSampleWrapper` — раздел [«Управление воспроизведением через обёртки»](#управление-воспроизведением-через-обёртки-audiosamplewrapper).

## Контракт

### Вход
- Регистрация драйвера через `AudioController.SetDriver(IDriver)` — вызывает `OnDriverConnect`, передаёт ссылки на реестры
- Заполнение `IndexSound` / `IndexMusic` — ответственность драйвера
- Заполнение `Settings.Channels` — ответственность драйвера

### Выход
- Настройки: `AudioController.Settings` (чтение свойств)
- Сэмплы: `AudioController.GetSample(guid)` → `IAudioSample` или `null`
- Воспроизведение: `PlaySound`, `PlayMusic`, `PlayCoverMusic`, `StopAllSounds`, `StopMusic`, `StopCoverMusic`
- Громкость: `GetSoundVolume(channel)`, `GetMusicVolume(channel)`, `GetSoundOn(channel)`, `GetMusicOn(channel)`
- Каналы: `GetChannelsList()`, `GetChannels()`, `GetChannel(name)`, `GetChVolume(id)`, `SetChVolume(id, value)`
- Событие: `AudioController.OnSettingsChanged`

### Гарантии
- `GetSample` ищет по обоим реестрам (звуки, затем музыка)
- При ненайденном GUID — возврат `null` + лог `Error`
- Каждый вызов `Set*State` / `Set*Volume` / `SetChVolume` инициирует `OnSettingsChanged`
- Все вызовы воспроизведения делегируются драйверу без трансформации
- `SetChVolume` вызывает `AudioChannel.OnUpdateData` для реактивной подписки

### Ограничения
- `AudioSettings` имеет `internal set` — модификация только через методы `AudioController`
- `AudioChannel.Volume` / `Mute` имеют `internal set` — модификация через `SetChVolume` или `FromSave`
- `AudioSample<T>.GetDataForSave()` возвращает `null` — сэмплы не участвуют в системе сохранений
- `MusicSample<T>` — abstract, прямое создание экземпляров невозможно

## Использование

### Настройки

```csharp
// Чтение
bool soundOn = AudioController.Settings.SoundOn;
float musicVol = AudioController.Settings.MusicVolume;
bool masterOn = AudioController.Settings.MasterOn;

// Изменение
AudioController.SetMasterState(false);     // выключить всё
AudioController.SetSoundState(false);      // выключить звуки
AudioController.SetMusicVolume(0.5f);      // громкость музыки 50%
AudioController.SetMasterVolume(0.8f);     // общая громкость 80%

// Подписка
AudioController.OnSettingsChanged += () => UpdateUI();
```

### Каналы

```csharp
// Список каналов
var channels = AudioController.GetChannelsList();

// Громкость канала
float vol = AudioController.GetChVolume("dialog");
AudioController.SetChVolume("dialog", 0.7f);

// Расчётная громкость с учётом Master и типа
float finalVol = AudioController.GetSoundVolume("ui");  // Master × Sound × Channel

// Extension на AudioChannel
var channel = AudioController.GetChannel("ambient");
channel.SetVolume(0.5f);
```

### Воспроизведение

```csharp
// Звук (fire-and-forget)
AudioController.PlaySound(sample);
AudioController.PlaySound(sample, loop: true);
AudioController.StopAllSounds();

// Звук с хэндлом управления (см. раздел про обёртки)
var voice = AudioController.PlaySoundWithControl(sample);
voice.OnFinished += OnDone;

// Основная музыка
AudioController.PlayMusic(music, fadingStart: true, fadingEnd: true);
AudioController.StopMusic();

// Ситуативная музыка
AudioController.PlayCoverMusic(battleTheme);
AudioController.StopCoverMusic(); // основная тема восстановится (драйвер)
```

### Получение сэмпла

```csharp
IAudioSample sample = AudioController.GetSample("explosion_01");
```

## Управление воспроизведением через обёртки (`AudioSampleWrapper`)

Хэндл конкретного проигрывания: управление (Play/Pause/Stop) и наблюдение (события Play/Paused/Finished,
состояние, длительность) над **одним** запущенным звуком/музыкой. Абстракция и фасад — в Core; конкретные
обёртки (`SoundWrapper` через пул, `MusicWrapper` через плеер) — в Unity-слое.

Обычный `PlaySound` — fire-and-forget: запустил и потерял ссылку, единственный рычаг обратно —
`StopAllSounds(channel)` (грубо, по каналу). Обёртка закрывает пробел: `*WithControl`-версии возвращают
токен управления именно этим инстансом. Отдельная точка входа (а не «всегда возвращать хэндл») — намеренно:
95% вызовов одноразовые, и обычный `PlaySound` остаётся `void` без лишних аллокаций.

### API: `Play` vs `PlayWithControl`

| Fire-and-forget (`void`) | С хэндлом (`AudioSampleWrapper`) |
|---|---|
| `PlaySound(sound, loop)` | `PlaySoundWithControl(sound, loop)` |
| `PlayMusic(clip, fadingStart, fadingEnd)` | `PlayMusicWithControl(clip, fadingStart, fadingEnd)` |
| `PlayCoverMusic(clip, fadingStart, fadingEnd)` | `PlayCoverMusicWithControl(clip, fadingStart, fadingEnd)` |

`*WithControl` возвращает `AudioSampleWrapper` или `null`, если воспроизведение не стартовало (неизвестный id,
драйвер не зарегистрирован и т. п.).

### Хэндл

```
AudioSampleWrapper : IDisposable
├── event OnPlay        — реально заиграл (в т.ч. resume после паузы)
├── event OnPaused      — встал на паузу
├── event OnFinished    — завершён (стоп / естественный конец / вытеснение)
├── IsLoop   : bool     — зациклен ли
├── IsPaused : bool     — на паузе (ставит только контроллер)
├── Duration : float    — длительность (с учётом pitch)
├── State    : PlaybackState  — состояние (ставит только контроллер)
└── (Play/Resume/Stop/Pause — protected internal; наружу — через контроллер)
```

`OnPlay` начального старта держатель поймать не успевает (для SFX он синхронен внутри `PlaySoundWithControl`,
до возврата хэндла) — стартовое состояние читай через `State`; практическая ценность `OnPlay` — **resume после паузы**.

### Фасад управления (`AudioSampleWrapperController`)

Единственная публичная точка управления (extension-методы). Только он меняет `State`/`IsPaused` — сырые
`Play/Stop/Pause` модели скрыты.

```csharp
wrapper.Play();    // старт или resume (по состоянию: из Paused — UnPause, иначе Play)
wrapper.Pause();   // пауза (только из Playing)
wrapper.Stop();    // внешний стоп: гасит источник + завершает
wrapper.Finish();  // завершить хэндл БЕЗ остановки источника (вытеснение; аудио гасят твины/плеер)
```

| Вызов | Из состояния | Действие |
|-------|--------------|----------|
| `Play` | `Pending` | `→ Playing`, старт (`Play`), `OnPlay` |
| `Play` | `Paused` | `→ Playing`, **`UnPause`** (продолжение), `OnPlay` |
| `Play` | `Finished` | игнор (терминальное) |
| `Pause` | `Playing` | `→ Paused`, `OnPaused` |
| `Pause` | иное | игнор |
| `Stop` / `Finish` | не `Finished` | `→ Finished`, `OnFinished`, `Dispose` |
| `Stop` / `Finish` | `Finished` | игнор (без повторного `OnFinished`) |

`Paused` достижимо только из `Playing` → resume всегда через `UnPause`. `Finished` — терминальное, дальнейшие
вызовы — no-op (идемпотентно; двойное владение держатель+пул безопасно).

`PlaybackState`: `Pending`(0, ещё не заиграл) / `Playing` / `Paused` / `Finished`.

### Конкретные обёртки (Unity)

- **`SoundWrapper` (SFX через пул):** живёт в данных пул-элемента рядом с клипом; `AudioSourceHandler`
  находит его и подключает `Init(source, stopCallback)`. Не-луп сам завершается по `Duration`
  (таймер → завершение через контроллер); `Pause`/`Stop`/`Dispose` снимают таймер. `Stop` дёргает
  `stopCallback` → элемент авто-выпиливается из пула.
- **`MusicWrapper` (музыка/cover):** единый владелец — `MusicPlayer` хранит один текущий хэндл; на новый трек
  прошлый завершается через `Finish` (без остановки источника — fade-out не срезается). `*WithControl`
  отдаёт этот хэндл во всех ветках, включая отложенный старт после фейд-аута (тогда он живёт в `Pending`).

### Владение и время жизни

Fire-and-forget звук владеется пулом (авто-освобождение). `*WithControl` — со-владение: **держатель
контролирует — держатель и завершает**. В частности: `Pause()` снимает авто-стоп-таймер, а `Resume` его
**не восстанавливает** — поставил на паузу и возобновил, сам вызови `Stop()`, когда звук не нужен.

### Переход со старой системы

Старая система была fire-and-forget: `Play*` ничего не возвращали, точечно остановить можно было только
`StopAllSounds(channel)`.

- **Существующий код трогать не нужно.** Сигнатуры обычных `Play*` не изменились (`void`, те же параметры) —
  все старые вызовы компилируются и работают как прежде.
- **Чтобы получить управление** — замени вызов на `*WithControl` и работай с хэндлом (проверяй `null`):
  ```csharp
  var line = AudioController.PlaySoundWithControl("hero_line");
  line.OnFinished += OnLineDone;
  line.Stop(); // гасит именно этот звук, а не весь канал
  ```
- **Кастомные драйверы (`IDriver`)** должны реализовать `*WithControl`-методы в дополнение к `void`-версиям
  (штатный `AudioDriver` уже реализует).

## Граничные случаи

- **Драйвер не зарегистрирован:** методы шины зовут `Driver.X(...)` напрямую, без null-guard в `SystemController` — обращение к незарегистрированному драйверу приводит к `NullReferenceException`.
- **Дубликат GUID:** при заполнении индексов драйвером — зависит от реализации (Unity-драйвер использует `AddNew`, последний перезаписывает).
- **OnSettingsChanged без подписчиков:** безопасный вызов через `?.Invoke()`.
- **Канал не найден:** `GetChVolume` возвращает `baseValue` (default 1f), `GetChannel` возвращает `null`, расчётные методы используют множитель 1.
- **AudioChannel.FromSave с некорректными данными:** fail-fast — исключение. Обработка ошибок — ответственность вызывающего кода.

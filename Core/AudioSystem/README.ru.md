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

| Метод | Описание |
|-------|----------|
| `SetLinks(indexSound, indexMusic, settings)` | Получение ссылок на реестры и настройки |
| `PlaySound(object, bool loop, string defaultChannel)` | Воспроизведение звука |
| `StopAllSounds(string channel)` | Остановка всех звуков (или по каналу) |
| `PlayMusic(object, fadingStart, fadingEnd, string defaultChannel)` | Воспроизведение основной музыки |
| `StopMusic()` | Остановка основной музыки |
| `PlayCoverMusic(object, fadingStart, fadingEnd, string defaultChannel)` | Воспроизведение ситуативной музыки |
| `StopCoverMusic()` | Остановка ситуативной музыки |

Параметр `object` — платформенный тип аудиоданных. Типизация определяется драйвером через pattern matching. Параметр `defaultChannel` — fallback-канал, если канал не задан в модели звука.

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
// Звук
AudioController.PlaySound(sample);
AudioController.PlaySound(sample, loop: true);
AudioController.StopAllSounds();

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

## Граничные случаи

- **Драйвер не зарегистрирован:** вызовы воспроизведения обращаются к `Driver` — поведение определяется `SystemController` (null-guard на уровне базового класса).
- **Дубликат GUID:** при заполнении индексов драйвером — зависит от реализации (Unity-драйвер использует `AddNew`, последний перезаписывает).
- **OnSettingsChanged без подписчиков:** безопасный вызов через `?.Invoke()`.
- **Канал не найден:** `GetChVolume` возвращает `baseValue` (default 1f), `GetChannel` возвращает `null`, расчётные методы используют множитель 1.
- **AudioChannel.FromSave с некорректными данными:** fail-fast — исключение. Обработка ошибок — ответственность вызывающего кода.

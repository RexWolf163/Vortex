# NaniExtensions

Мост между фреймворком Vortex и визуальной новеллой Naninovel. Содержит подпакеты разных слоёв.

## Назначение

- Трансляция настроек громкости Vortex в Naninovel
- Управление Nani-сценами из Vortex (пауза, стоп, сброс акторов)
- Многоканальная локализация (UI, диалоги, озвучка)
- Управление катсценами на Spine-анимациях через Nani-бэкграунды

Вне ответственности: Naninovel-скрипты, контент сцен, настройка Spine-ассетов.

## Подпакеты

| Подпакет | Сборка | Слой | Назначение |
|----------|--------|------|-----------|
| [Core](Core/) | `ru.vortex.nani.core` | 3 | `NaniWrapper` — ленивый доступ к сервисам Naninovel |
| [AudioSystem](AudioSystem/) | `ru.vortex.nani.audiosystem` | 3 | Трансляция громкости, управление Nani-аудио |
| [CutsceneSystem](CutsceneSystem/) | `ru.vortex.nani.cutscenes` | 3 | Контроллер Spine-катсцен |
| [LocalizationSystem](LocalizationSystem/) | `ru.vortex.nani.localization` | 2 | Драйвер локализации с каналами |

---

## Core

**Namespace:** `Vortex.NaniExtensions.Core`

`NaniWrapper` — статический класс с lazy-кешированным доступом к сервисам Naninovel через `Engine.GetService<T>()`.

### Доступные сервисы

`AudioManager`, `StateManager`, `L10N`, `CommunityL10N`, `ScriptPlayer`, `BackgroundManager`, `CharacterManager`, `TextPrinterManager`, `ChoiceHandlerManager`, `UnlockableManager`, `UIManager`, `VariablesManager`

### Жизненный цикл

- `[RuntimeInitializeOnLoadMethod]` — подписка на `GameController.OnNewGame`, `OnLoadGame`, `OnGameStateChanged`
- `OnNewGame` / `OnLoadGame` → `ScriptPlayer.Stop()` + `ResetNani()`
- `GameStates.Off/Win/Fail` → `ScriptPlayer.Stop()` + `ResetNani()`

### API

| Метод | Описание |
|-------|----------|
| `ResetNani()` | Остановка всего аудио, сброс переменных, скрытие бэкграундов, персонажей, текстовых принтеров, сброс выборов |
| `NaniIsPlaying()` | `true` если ScriptPlayer играет или видим хэндлер выбора |

---

## AudioSystem

**Namespace:** `Vortex.NaniExtensions.AudioSystem`, `Audio`

### NaniVortexAudioConnector

Трансляция настроек громкости каналов Vortex → Naninovel.

- Каналы (`bgm`, `sfx`, `voice`, `voiceCutscene`) загружаются из `AudioChannelsConfig` через partial-расширение `AudioChannelsConfigExtNani`
- При `GameStates.Off` — изменение настроек сразу проецируется в Nani
- При запущенной игре — только через явные вызовы `GetNaniBgmVolume()`, `GetNaniSfxVolume()`, `GetNaniVoiceVolume()`
- `SetCutsceneMode(bool)` — переключает источник громкости голоса между `voiceChannel` и `voiceCutsceneChannel`

### AudioNaniController

Управление Nani-аудио из Vortex.

| Метод | Описание |
|-------|----------|
| `StopNaniMusic()` | Пауза текущего BGM, сохранение пути в `PausedMusicPath` |
| `PlayNaniMusic()` | Возобновление BGM из `PausedMusicPath` |
| `StopNaniVoice()` | Остановка голоса |
| `StopNaniSfx()` | Остановка всех SFX |

### AudioChannelsConfigExtNani

Partial-расширение `AudioChannelsConfig` (сборка `ru.vortex.unity.audiosystem.ext`). Добавляет 4 поля с атрибутом `[AudioChannelName]` для маппинга Nani-каналов на Vortex-каналы.

---

## CutsceneSystem

**Namespace:** `Vortex.NaniExtensions.CutsceneSystem`, `Vortex.NaniExtensions.CutsceneSystem.Models`

Контроллер Spine-анимированных катсцен, отображаемых как Naninovel `SpineBackground`.

### CutsceneController

Статический контроллер. Жизненный цикл:

```
Open(key) → загрузка CutsceneData → SpineBackground → LoadPhase → [NextPhase]* → Close
```

| Метод | Описание |
|-------|----------|
| `Open(key, canBeClosedByButton)` | Загрузка катсцены по Addressable-ключу, запуск первой фазы |
| `NextPhase()` | Переход к следующей фазе |
| `Close()` | Остановка, очистка, удаление бэкграунда |

Фазы:
- Каждая `CutscenePhase` — анимация (looped/не looped) + опциональный ambient-звук
- Не looped-анимации автоматически переходят к следующей фазе
- Spine-события (`Event`) маппятся на звуки через `EventToAudioData`

Реакция на `GameStates`:
- `Play` → `timeScale = 1`, возобновление звуков
- `Paused` → `timeScale = 0`, остановка всех звуков
- `Off` → `Close()`

### Модели

| Класс | Описание |
|-------|----------|
| `CutsceneData` | ScriptableObject: `SkeletonDataAsset`, `List<CutscenePhase>`, `List<EventToAudioData>`, `List<string> SexSceneAmbients` |
| `CutscenePhase` | Фаза: `AnimationKey`, `AnimationLooped`, `AmbientAudioPack` |
| `CutscenePhaseData` | Диалоговые данные: `AuthorTextKey`, `DialogueTextKey`, озвучка (Ru/En) |
| `EventToAudioData` | Маппинг Spine-события → звук: `EventName`, `AudioPack` |

`CutsceneData.SyncWithSpine()` — Editor-кнопка: синхронизирует фазы и события с `SkeletonDataAsset`.


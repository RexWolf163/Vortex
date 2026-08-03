# NaniExtensions

Мост между фреймворком Vortex и визуальной новеллой Naninovel. Содержит подпакеты разных слоёв.

## Назначение

- Трансляция настроек громкости Vortex в Naninovel
- Управление Nani-сценами из Vortex (пауза, стоп, сброс акторов)
- Многоканальная локализация (общий язык, диалоги, озвучка)
- Управление катсценами на Spine-анимациях через Nani-бэкграунды

Вне ответственности: Naninovel-скрипты, контент сцен, настройка Spine-ассетов.

## Подпакеты

| Подпакет | Сборка | Слой | Назначение |
|----------|--------|------|-----------|
| [Core](Core/) | `ru.vortex.nani.core` | 3 | `NaniWrapper` — ленивый доступ к сервисам Naninovel |
| [AudioSystem](AudioSystem/) | `ru.vortex.nani.audiosystem` | 3 | Трансляция громкости, управление Nani-аудио, `NaniVoicePlayBus` |
| [CutsceneSystem](CutsceneSystem/) | `ru.vortex.nani.cutscenes` | 3 | Контроллер Spine-катсцен |
| [LocalizationSystem](LocalizationSystem/) | `ru.vortex.nani.localization` | 2 | Драйвер локализации с каналами |
| [QuestSystem](QuestSystem/) | `ru.vortex.nani.quests` | 3 | Условие квеста `NaniPlayerState` — реакция на старт/стоп Nani-плеера (`defineConstraints`: `USING_VORTEX_QUESTS`, `USING_NANINOVELL`) |
| [SaveSystem](SaveSystem/) | `ru.vortex.nani.saves` | 3 | `NaniDataSaveController` — сохранение/восстановление Nani-переменных через `GameController` |
| [Misc](Misc/) | `ru.vortex.nani.misc` | 3 | Точечные хэндлеры сцены (активный говорящий, баббл, эмоции, ZPosition, тестовые тулзы галереи) |

Активация всего семейства Nani-пакетов — через символ `USING_NANINOVELL`, который выставляется тогглом `naninovellExt` (`SdkSettings`, атрибут `[DefineSymbol("USING_NANINOVELL")]`).

---

## Core

**Namespace:** `Vortex.NaniExtensions.Core`

`NaniWrapper` — статический класс с lazy-кешированным доступом к сервисам Naninovel через `Engine.GetService<T>()`.

### Доступные сервисы

`AudioManager`, `StateManager`, `L10N`, `CommunityL10N`, `ScriptPlayer`, `BackgroundManager`, `CharacterManager`, `TextPrinterManager`, `ChoiceHandlerManager`, `UnlockableManager`, `UIManager`, `VariablesManager`

### Жизненный цикл

- `[RuntimeInitializeOnLoadMethod]` — подписка на `GameController.OnNewGame`, `OnLoadGame`, `OnGameStateChanged`
- `OnNewGame` → `ScriptPlayer.Stop()` + `ResetNani()` + `VariablesManager.ResetAllVariables()`
- `OnLoadGame` → `ScriptPlayer.Stop()` + `ResetNani()`
- `GameStates.Off/Win/Fail` → `ScriptPlayer.Stop()` + `ResetNani()`

### API

| Метод | Описание |
|-------|----------|
| `ResetNani()` | Остановка всего аудио, сброс переменных, скрытие бэкграундов, персонажей, текстовых принтеров, сброс выборов |
| `NaniIsPlaying()` | `true` если ScriptPlayer играет **или видим хэндлер выбора** (проверка на choice несущая — см. ниже) |

### События воспроизведения

```csharp
public static event Action OnNaniStart;   // воспроизведение скрипта началось
public static event Action OnNaniStop;     // воспроизведение остановилось (устаканилось)
```

Оба выводятся из `ScriptPlayer.OnPlay`/`OnStop`, но фильтруются через `NaniIsPlaying()` и **однокадровый settle-дебаунс**, чтобы не сообщать о транзиентных остановах:

- **`@if` / `goto` / смена скрипта** уходят в `ScriptPlayer.Resume()`, который синхронно зовёт `Stop()` (→ `OnStop`, `Playing == false`) и тут же пересоздаёт play-routine (→ `OnPlay`, `Playing == true`) в одном кадре. Репортить эту пару — значит стрелять ложным `OnNaniStop`/`OnNaniStart` на каждом ветвлении.
- Поэтому **останов подтверждается через кадр**: решение откладывается, и на следующем кадре `NaniIsPlaying()` перепроверяется — если воспроизведение возобновилось (Resume пересоздал routine), останов гасится; встречный `OnPlay` в том же кадре тоже снимает назначенную проверку. **Старт репортится мгновенно** (и снимает отложенный останов).
- **Реальный останов** (`Stop()` без последующего `Resume`) остаётся `false` и в следующем кадре → `OnNaniStop` (с задержкой в один кадр).
- **Проверка на choice в `NaniIsPlaying()` остаётся** — видимый хэндлер выбора считается «играет». Это отдельный, многокадровый случай (`@stop` + показанные варианты): воспроизведение стоит `false` много кадров, пока ждём выбор, и однокадровый дебаунс его не покрывает. Без choice-проверки `OnNaniStop` летел бы на каждом выборе.

Контракт: `OnNaniStop` приходит на кадр позже; синхронная пара `Stop→Play` (в т.ч. смена скрипта через `Play()`) трактуется как продолжение воспроизведения и не даёт start/stop. Если нужен сигнал именно на **смену** скрипта — подписывайтесь на `ScriptPlayer.OnPlay` напрямую (другой `PlayedScript`).

### NaniSessionService

Адаптер `Naninovel.Engine` под контракт `IGameSessionService` из `Sdk/Core`. Регистрируется автоматически через `[RuntimeInitializeOnLoadMethod]`. После регистрации `GameController` дожидается `Engine.Initialized` перед каждым переходом в `Play` (после `NewGame` и `OnLoad`). До этого ожидание было жёстко зашито в `Sdk/Core` — теперь Sdk/Core не знает о Naninovel, и зависимость живёт исключительно здесь.

```csharp
public sealed class NaniSessionService : IGameSessionService
{
    public bool IsReady => Engine.Initialized;
    public string Name => "Naninovel.Engine";
}
```

### LogicChainsExt — `NaninovelInitialized`

Условие для логических цепочек (`LogicChainsSystem`), лежит в `Core/LogicChainsExt/`. Наследник `UnityCondition`, ждёт `Engine.Initialized` (через `Engine.OnInitializationFinished`).

Симметрично условию `SystemsLoaded` из `ru.vortex.unity.logicconditions` (которое ждёт `App.Running`): поставленные **на один коннектор** вместе они дают конъюнкцию — переход цепочки происходит только когда готовы обе подсистемы, и Vortex, и Naninovel, независимо от того, кто финишировал первым. Типовое применение — этап «Waiting Loading» цепочки-загрузчика приложения (детальный пример — в README `LogicChainsSystem (Unity)`).

Появляется в дропдаунах условий цепочки автоматически, пока подключён `ru.vortex.nani.core`.

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

### NaniVoicePlayBus

Шина событий начала/завершения реплики персонажа. Объединяет два источника — `ITextPrinterManager` и `IAudioManager` — в единый контракт:

```csharp
public static event Action<string> OnVoiceStart;   // authorId говорящего
public static event Action<string> OnVoiceStop;    // authorId говорящего
```

Воспроизведение voice детектируется поллингом `IAudioManager.GetPlayedVoice()`. Шина корректно ловит:
- конец voice (path → null);
- смену voice на другого актора (pathA → pathB) даже без null-окна между ними;
- «тихий переход» (тот же автор продолжает с новой voice-дорожкой — аниматор остаётся в Forward, ни Stop, ни Start не эмитятся);
- реплику без voice — Stop эмитится в `PrintFinished`, чтобы подписчик не залипал в состоянии «говорит» при отсутствии следующей `PrintStarted` (конец диалога, пауза на выбор).

Алгоритм:
1. `OnPrintStarted` — закрытие предыдущей реплики (если без voice), эмит `OnVoiceStart`, старт/продолжение поллинга, немедленная реконсиляция через `PollVoice()` (voice мог уже играть к моменту события: `@print` awaitит `PlayVoice` до фаер'инга).
2. Поллинг через `TimeController.AddCallback` ловит любые transitions path и эмитит `OnVoiceStop` / запоминает нового автора как cached.
3. `OnPrintFinished` — `OnVoiceStop`, если у завершившейся реплики не было собственной voice. Если voice играет — закрытие через поллинг (voice имеет право продолжаться после печати текста).
4. `OnNaniStop` / `App.OnExit` — `FlushAll`, закрытие всех открытых реплик.

Жизненный цикл: подписки в `App.OnStart`, отписка в `App.OnExit`. Не требует регистрации/инициализации со стороны проекта.

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

---

## Misc

**Namespace:** `Vortex.NaniExtensions.Misc`
**Сборка:** `ru.vortex.nani.misc`

Точечные хэндлеры для интеграции Nani-сцены с проектным UI и логикой. Каждый — самостоятельный MonoBehaviour, вешается на UI-объекты сцены и работает поверх `NaniWrapper`/`NaniVoicePlayBus`.

| Хэндлер | Что делает |
|---|---|
| `ActiveCharacterHandler` | Отслеживает «кто говорит» через `NaniVoicePlayBus.OnVoiceStart/Stop` и переключает визуал активного актора (StateSwitcher / выделение). |
| `CharacterVoiceTweenerHandler` | Связывает `NaniVoicePlayBus` с `TweenerHub` на персонаже: Forward на старте voice, Back на завершении. |
| `LookCharacterHandler` | Поворот персонажа на текущего говорящего (источник — `NaniVoicePlayBus`). |
| `VisibilityCharacterHandler` | Скрытие/показ персонажей по правилам сцены (на основе `CharacterManager.GetActor`/`Appearance`). |
| `DialogBubbleSwitcher` | Переключатель состояний диалогового баббла (типы реплик, эмоции, фон). |
| `BubblePositionTarget` | Якорь позиции баббла относительно мирового объекта (актор/слот). |
| `TextBubbleResizer` | Динамический размер баббла под длину текста реплики (после `OnPrintStarted`). |
| `ZPositionSwitch` | Переключение Z-позиции по доменному стейту (Active/Inactive говорящий). |
| `ResetAllGalleryCardsHandler` | Editor-инструмент: сбрасывает прогресс открытых карт галереи в `UnlockableManager` (для тестирования сцен галереи в чистом состоянии). |
| `AutoPlaySwitcher` | Тоггл «авто-режима» Naninovel. Привязывается к `UIComponent` (Switcher On/Off). На клике переключает `IScriptPlayer.SetAutoPlayEnabled(!cached)`, по событию `OnAutoPlay` синхронизирует визуал с реальным состоянием плеера. |
| `SkipDialogueSwitcher` | Тоггл «скип-режима» диалогов. Кроме собственного состояния учитывает `PlayerSkipMode`: при `Everything` скип всегда доступен, иначе — только если текущая позиция уже была воспроизведена (`Player.HasPlayed(path, index+1)`). Дополнительный `UIStateSwitcher` (`enableSwitcher`) показывает «доступно/недоступно» — для затемнения кнопки на ещё непрочитанных репликах. По `OnStop` (смене скрипта) скип принудительно сбрасывается. |


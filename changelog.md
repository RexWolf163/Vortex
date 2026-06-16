# Changelog: 1.0.260612 → main

## Addressable-ассеты

Сквозная тема релиза — перевод тяжёлых ассетов (аудио, Spine-скелеты) на addressable-ссылки при сохранении лёгких записей `Database` и синхронного контракта шины.

### AudioSystem — отложенная загрузка звуков

- **`SoundSampleAddressablePreset`** — addressable-вариант пресета звука (`AssetReferenceAudioClip[]`). Тестовое проигрывание в редакторе — через `editorAsset` (без рантайм-загрузки).
- **`AssetReferenceAudioClip`** — `[Serializable] AssetReferenceT<AudioClip>` под `#if ENABLE_ADDRESSABLES` (встроенного типа для `AudioClip` в Addressables нет).
- **`SoundClip`** — ленивая загрузка по требованию: `GetClip()` → `LoadAssets()` через `LoadAssetAsync<T>().WaitForCompletion()` (синхронно, без дедлока). Политика **never-release**: клип грузится один раз и живёт до выхода из приложения, хендлы не хранятся (выгрузка всегда происходит со смертью приложения — счётчик владельцев был бы нарушением нулевого принципа).
- **`SoundSamplePresetConverter`** (Editor) — меню `Assets/Vortex/Convert to Addressable Sound`. Конвертирует обычный пресет в addressable **на месте, сохраняя GUID** записи (`CopyFromSerializedProperty` с пропуском `m_Script` и `audioClips`), регистрирует клипы в дефолтной addressable-группе.

### SpineExtensions/Addressable

- **`AssetReferenceSkeletonDataAsset`** — addressable-ссылка на `SkeletonDataAsset` (`#if ENABLE_ADDRESSABLES`).
- **`SpineReadyGateHandler`** — гейт слоёв/визуала по готовности скелета: ждёт инициализации `SkeletonDataAsset` и открывает зависимые слои.

### AssetCacheSystem

- **`DataStorageTransport`** — транспорт-обёртка для кеша addressable-ассетов (owner-ref-count + inflight-dedup).
- Добавлен **режим синхронной загрузки** (`WaitForCompletion`) — для случаев, когда ассет нужен немедленно в текущем кадре.

---

## LogicChains — распределённые по пакетам элементы

Элементы цепочек логики размещены в пакетах-владельцах соответствующих систем (package-composition-first), а не свалены в один пакет:

- **`Unity/LogicChainsSystem/Actions/UnLoadScene`** (новый) + доработка `LoadScene` (case-фикс, `FormerlySerializedAs`).
- **`Unity/UIProviderSystem/LogicChains/`** (новые): `OpenUI`, `CloseUI`, `CloseAllUI` — действия цепочек, живущие в пакете `UIProvider`.
- **`NaniExtensions/Core/LogicChainsExt/NaninovelInitialized`** (новое условие) — гейт по факту инициализации Naninovel.
- **`Unity/LogicConditionsSystem/Conditions/`** — уточнения `SceneLoaded`, `SystemsLoaded`.
- Добавлены ссылки в asmdef-ы пакетов-владельцев (`uiprovider`, `nani.core`, `components`, `sdk.game.core`).

---

## CursorSystem — наборы по разрешению

- **`CursorPack`**, **`CursorResolutionPack`** (новые) — наборы курсоров, выбираемые по высоте экрана.
- `CursorController` — `SelectPack` по `Screen.height`, `RefreshResolution()` при смене разрешения.
- Используется `CursorMode.ForceSoftware` — обход ограничений ОС на размер/формат аппаратного курсора (32×32 и т.п.). *Это не фикс Android (там курсора нет вовсе).*
- `SettingsModelExtCursor` — partial-расширение модели настроек под наборы курсоров.

---

## Производительность и устойчивость

- **`ObjectExtCopy`** — кеширование `PropertyInfo` (`ReadablePropertiesCache` / `WritablePropertiesCache`, `Dictionary<Type, …>`) в глубокой копии. Устраняет повторную рефлексию на каждый объект.
- **`DatabaseDriverExtLoadingSystem`** (ResourcesDriver) — yield по бюджету кадра вместо `await UniTask.Yield()` на каждую запись: `Stopwatch` + бюджет 8 мс. Устраняет ~4-секундный фриз на старте при большой базе.
- **`TimeController`** — реентрантная отмена внутри волны: `ReadyQueue → List<QueuedAction>` (снимок волны с owner), `HotRemovedOwners` (HashSet), `_inWave`. `RemoveCall(owner)` из выполняющегося в волне action'а теперь гасит ещё не выполненные action'ы того же owner из уже снятого снимка. Доработана связка с твиннерами.

---

## Bugfixes и мелкие правки

- **`SettingsDriver`** — фикс Android-сборки: проверка/создание путей (`CheckPath`) обёрнута в `#if UNITY_EDITOR` (на устройстве пути создаются иначе).
- **`RollbackSettings`** — переход с прямой ссылки `UserInterface ui` на `[DbRecord(typeof(UserInterfaceData))] string ui` (закрытие UI по id из записи `Database`); убран редакторный `OnValidate`-автолинк.
- **`LoadSceneHandler`** — интеграция с `TimeController`.
- **`LoaderStarter`** — `.Forget()` на fire-and-forget запуск загрузчика.
- **`MiniGameStateSwitcher`** — приведение к актуальному API.
- **`DataStorage`** (UI/Misc) — точки расширения `protected`/`virtual`.
- **`TweenLogic`** — guard `_isDead` против обращения к уничтоженному объекту.
- **`SpineAnimationLogicBase`** — доработка под addressable Spine.

---

## Документация

- **Core/DatabaseSystem** (ru/en) — раздел «границы применимости»: матрица размещения данных (частное/общее × лёгкое/тяжёлое), когда тяжёлый ассет идёт addressable-ссылкой, а не телом записи.
- **LogicChains** (ru/en) — описание распределения элементов цепочек по пакетам-владельцам.
- **TimeSystem** (ru/en) — раздел реентрантной отмены в волне, паттерн паузируемого отложенного действия (`Timer` vs `Call`).
- **AudioSystem**, **SpineExtensions/Addressable**, **UI/Misc** (ru/en) — addressable-варианты и точки расширения.
- **`PACKAGES.md`**, **`CLAUDE.md`** — зарегистрированы ранее не внесённые пакеты (`CursorSystem`, `AssetCacheSystem`, `EffectSpawnSystem`, `RewardsSystem`), подпапка `SpineExtensions/Addressable`.

---

## Migration notes

- **Addressables-зависимости в рантайм-asmdef.** `ru.vortex.unity.audio` теперь ссылается на сборки Addressables. Проверьте, что в плеер-сборке нет ссылок на Editor-only сборку Addressables (иначе билд упадёт). Addressable-код звуков целиком под `#if ENABLE_ADDRESSABLES`.
- **`RollbackSettings`** — поле `ui` сменило тип `UserInterface` → `string` ([DbRecord]). Сериализованные ссылки в префабах/сценах потребуют переназначения записи UI.
- **`SoundSampleAddressablePreset`** — конвертация обычных пресетов выполняется через меню `Assets/Vortex/Convert to Addressable Sound` (GUID записи сохраняется). Клипы должны быть зарегистрированы как addressable.
- **Database stays synchronous.** Отложенная загрузка реализована на уровне ассета (`SoundClip.GetClip()` синхронен изнутри), а не через асинхронный доступ к шине. Записи остаются лёгкими и полностью резидентными.


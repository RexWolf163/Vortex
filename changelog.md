# Changelog: 1.0.260520 → main

**Файлов изменено:** 80 (+3744 / −267 строк)

Документ описывает изменения относительно базовой версии `1.0.260520`. Раздел «Migration notes» внизу — единственный блок, который влияет на существующий код проектов; всё остальное — аддитивные расширения.

---

## 🆕 Новые пакеты

### `Sdk/RewardsSystem`

Полноценная система выдачи наград:

- **`RewardBus`** — статическая шина с событиями `OnRewardGiven` / `OnRewardFailed`, передающими `RewardEventData { Reward, TargetId, Power, Result }`.
- **`RewardPreset`** — `ScriptableObject` с массивом `RewardPack` (вес + список наград), взвешенный розыгрыш одного пака на `GetReward()`.
- **`RewardsExtLogic`** — extension-методы: `GetReward()` на пресете, `ValidateRewardConditions()` и `GiveReward()` на `RewardData`.
- **`RewardStrategy`** (abstract) — точка полиморфизма через `[SerializeReference]`. Поля: `GetLabel()`, `Type`, `Validation()`, `GiveReward()`.
- **`RewardType : ExtensibleEnum`** — partial, расширяется проектными пакетами (Item, Currency и т. д.). Используется для фильтрации/группировки результата.
- **`RewardResult`** (struct) — `Success`, `FailReason`, `AppliedAmount`, `Type`. Контракт «дискретной награды» (целочисленное количество).
- **`RewardData`** — сериализуемая обёртка с именем и стратегией (`internal` для инкапсуляции).
- **`RewardPack`** — взвешенная группа наград.

Документация: `Sdk/RewardsSystem/README.{ru,en}.md`. Сборка `ru.vortex.sdk.game.rewards`. Зависимости: `Vortex.Core.Extensions`, `Vortex.Core.ExtensibleEnumSystem` + Odin.

### `Unity/UI/CursorSystem`

Кастомный системный курсор для UGUI:

- **`CursorController`** (static) — приоритеты LMB > RMB > Hover > Default, подписка на InputSystem через `InputAction`, soft-reset на alt-tab.
- **`CursorSettings`** (`SettingsPreset`) — `cursorDefault`, `cursorLeftMouseDown`, `cursorRightMouseDown`, массив `cursorOnHover[]`.
- **`MouseHoverListener`** (MonoBehaviour) — на UI-объектах, ValueDropdown по именам спрайтов из активного `CursorSettings`.
- **`MouseKeyMap`** — `BoolData LeftKeyPressed`, `BoolData RightKeyPressed`, `IntData HoverIndex` с owner-защищённой записью.
- **`SettingsModelExtCursor`** + `.asmref` — partial-расширение `SettingsModel` (канонический Vortex-паттерн).

Документация: `Unity/UI/CursorSystem/README.{ru,en}.md`. Сборка `ru.vortex.unity.cursorsystem`.

---

## 🎬 SpineExtensions/UIs — новые хэндлеры и переработка

Целый набор расширений Spine-интеграции:

- **`SpineRandomizationStart`** — рассинхрон стартовой фазы анимации (для массовых одинаковых скелетов).
- **`SpineAnimationScrubHandlerBase`** + `SpineAnimationScrubHandler` (`SkeletonGraphic`) + `SpineSkeletonAnimationScrubHandler` (`SkeletonAnimation`) — скраб-трек анимации по `FloatData` 0..1 (например, прогресс-бар → поза скелета).
- **`MeshRendererOrderSwitch`** — переключатель `sortingOrder` у `MeshRenderer` (для `SkeletonAnimation` без UGUI Canvas).
- **`AnimatorPauseHandler`** — пауза Mecanim Animator по `GameStates.Paused/Loading` (по аналогии с `SpinePauseHandler` для Spine).

### `SpinePauseHandler` — несколько итераций фикса

Финальная версия использует **`spineAnimation.enabled = false`** на паузе вместо `timeScale = 0` / `updateMode = UpdateMode.Nothing`. Причина: в проектах с Naninovel character actor внешний update-цикл каждый кадр пишет в `timeScale` нужное значение, и наши изменения затираются за 1–2 кадра. `enabled = false` Unity-механизм поллер обратно включить не может.

Для `SkeletonGraphic` остаётся штатный флаг `freeze`.

### `SpineSkinSwitch` — поддержка `SkeletonAnimation`

`StateItem` для `UIStateSwitcher` теперь поддерживает оба типа скелетов: `SkeletonGraphic` (UGUI) и `SkeletonAnimation` (MeshRenderer). `Clone()` копирует обе ссылки, `GetListSkins()` подхватывает данные из любой назначенной.

---

## 🎨 Unity/UI/Misc — новые компоненты

### `CounterViewBase<T>` + `CounterStates`

Базовый класс счётчика с минимумом/максимумом/текущим значением. Поддерживает:

- Slider, TweenerHub-пульсацию, UIStateSwitcher с порогами (`Empty`/`Less20`/`Less50`/`Less80`/`Less100`/`Fill`).
- Кешированный `Data` с автоинвалидацией на `OnUpdateLink`.
- Фикс `FormatException` в `OnEnable` (паттерн с тремя аргументами).
- Раздельное `onUp`/`onDown` для пульсации.

### `CounterViewAdvanced`

Готовый наследник `CounterViewBase<T>` под типовой сценарий «модель с тремя `IntData`».

### `AdvancedButton` — переход на `IPointerClickHandler`

Реализация претерпела заметные изменения:

- **Защита от scroll-drag из коробки.** `OnClick` и `OnUpInBorders` теперь идут через канонический `IPointerClickHandler` Unity — EventSystem сама не вызовет `OnPointerClick`, если жест классифицирован как drag (актуально для кнопок внутри `ScrollRect`).
- **Корректная семантика дистанции и времени.** Старая формула с приоритетом операторов исправлена; внешний `Release()` теперь работает.
- **`OnPointerEnter` сохраняет Pressed**, если кнопка всё ещё прижата (сценарий «нажали → увели → вернули»).
- **`AddOnClick` идемпотентен** — повторная подписка одного `UnityAction` игнорируется.

### Прочее

- **`SliderView`** — мелкие правки.
- **`DataStorageView`** — мелкие правки.
- **`DropDownComponent`** — мелкие правки.

---

## 🛠️ Editor-инструменты и UX

### `DbRecordAttributeDrawer`

- **Кеш TTL 1 сек** на пары `(RecordType, RecordClass)`. До правки `ReloadDatabase()` + полная пересборка списка имён/GUID'ов выполнялись на **каждый** OnGUI каждого `[DbRecord]`-поля — на инспекторе с большим `Sound`-каталогом редактор тормозил до неюзабельности. Теперь — раз в секунду на уникальные настройки атрибута.
- **Переход на stateless-перегрузку `SearchablePopup.Draw`** — корректное обновление `ValueEntry.SmartValue` без гонки с внутренним `SelectionCache` попапа.
- **Корректная вёрстка для пустого label** — больше не теряется ширина у элементов массива.
- **Bugfix:** для `RecordType.Singleton` дропдаун не отрисовывался (рисовка была только в ветке MultiInstance). Теперь обе ветки симметричны.
- **Поиск по полному пути.** Ввод имени раздела (например, `February`) находит все его листья; имена с `.` преобразуются в иерархию через `/`.

### `EffectKeyAttributeDrawer` (EffectSpawnSystem)

- **Опциональный ключ.** Первой строкой попапа идёт пункт `[NONE]` со значением `""`. Без него dropdown при первой отрисовке автоматически перезаписывал пустое поле на первый попавшийся ключ.
- **Fail-loud на несуществующий ключ.** Если ключ из удалённого/переименованного эффекта — поле подсвечивается красным с пометкой «отсутствует», `property.stringValue` **не** перезаписывается до явного выбора дизайнера.
- **Симметрия источников ключей.** Обе ветки (шина и AssetDatabase) идут через один пайплайн `Distinct → OrderBy → Insert("")`.

### `SearchablePopup` (EditorTools/Elements)

Добавлена **stateless-перегрузка `Draw`** для использования в drawer'ах с внешним источником истины (Odin `ValueEntry`, `SerializedProperty`). Решает race condition между внутренним `SelectionCache` и авторитативным внешним состоянием — выбор уходит **только** через переданный `onPicked` callback.

### `RecordPreset` (DatabaseSystem)

- **Защита от невалидного имени ассета.** Если имя не начинается с буквы (цифра, символ, пробел) — подставляется `DefaultName`. Это защищает C#-кодогенерацию от падений на невалидных идентификаторах.
- **Debug-cache `_data` с TTL 1 сек.** Поле `_data` в инспекторе (для Singleton, в Play-режиме) больше не вызывает `Database.GetRecord<T>()` + `CopyFrom` на каждой перерисовке. Теперь — раз в секунду.
- **Видимость debug-поля для MultiInstance** скорректирована.

### `AudioLocaleDataPreset` (Sdk/AudioLocalizationSystem)

Добавлен Editor-toolkit «Грубый инструментарий массовой замены»:

- **Update Voices Array** — массово пересобирает ссылки на Sound под новый `textGuid`.
- **Замена первой части ключа** — переключает пресет под другого персонажа (меняет префикс ключей).
- **Скопировать первую запись под другой язык** — клонирует первую voice-запись под новый locale.

Документация конвенции именования включена в README.

---

## 🐛 Bugfixes

### `NaniVoicePlayBus` (NaniExtensions/AudioSystem)

Новая шина событий начала/завершения реплики персонажа. Объединяет `ITextPrinterManager` и `IAudioManager.GetPlayedVoice()` в единый контракт `OnVoiceStart(authorId)` / `OnVoiceStop(authorId)`. Корректно обрабатывает:

- silent transition (тот же автор, новая voice-дорожка) без лишних Stop/Start;
- реплику без voice — Stop эмитится в `OnPrintFinished` (без этого подписчик залипал при отсутствии следующей `OnPrintStarted` — конец диалога или пауза на выбор);
- смену voice на другого актора без null-окна;
- `OnNaniStop` / `App.OnExit` → `FlushAll`.

### `UIStateSwitcher`

- **Перехват исключений** в `foreach stateItem.Set()`: одна сломанная StateItem не валит весь switcher, остальные пункты состояния не выполняются (fail-fast), в Console — `LogError` + `LogException`.
- **Откат к `stateOnEnable`** при попытке `Set("несуществующее")` — switcher не остаётся в неопределённом состоянии.

### Прочее

- `CharacterVoiceTweenerHandler` — fix lip-sync (`998ba54`).
- `LanguageAttributeDrawer` — точечная правка дровера.
- `InputActionHandler` — точечная правка.
- `ToggleButtonAttributeDrawer` — правка.
- `DrawingUtility` — правки.

---

## 📚 Документация

Обновлены / созданы README:

### Новые
- `Unity/UI/CursorSystem/README.{ru,en}.md`
- `Sdk/RewardsSystem/README.{ru,en}.md`

### Обновлённые
- `README.{md,ru,en}.md` (корневой Vortex)
- `Unity/UI/README.{ru,en}.md` — добавлен CursorSystem в перечень подсистем
- `Unity/UI/Misc/README.{ru,en}.md` — `CounterViewBase`/`CounterViewAdvanced`, новый `AdvancedButton`
- `Unity/UI/StateSwitcher/README.{ru,en}.md` — try/catch, fallback к `stateOnEnable`
- `Unity/EditorTools/README.{ru,en}.md` — stateless-перегрузка `SearchablePopup`
- `Unity/DatabaseSystem/README.{ru,en}.md` — name-guard, debug-cache, drawer TTL
- `Unity/AppSystem/System/TimeSystem/README.{ru,en}.md` — расширение
- `Unity/DebugSystem/README.{ru,en}.md` — обновление
- `Unity/UIProviderSystem/README.{ru,en}.md` — обновление
- `Sdk/EffectSpawnSystem/README.{ru,en}.md` — опциональность, fail-loud, симметрия веток
- `Sdk/AudioLocalizationSystem/README.{ru,en}.md` — раздел про toolkit массовой замены
- `SpineExtensions/README.{ru,en}.md` — `enabled = false`-подход к паузе
- `NaniExtensions/README.{ru,en}.md` — `NaniVoicePlayBus`, подпакет `Misc`

### Новый AI-tool

`AITools/ClaudeCodeSkills/tz-design/SKILL.md` — skill для Claude Code «оформление технического задания» с трёхфазным алгоритмом (критический разбор → декомпозиция данных → структурированное оформление).

---

## ⚠️ Migration notes

Изменения в существующих публичных API, которые могут потребовать правок на проектной стороне.

### `AdvancedButton`

- **Добавлен интерфейс `IPointerClickHandler`.** Если в проектном коде есть кастомные `EventSystem`-подписки на `AdvancedButton`, проверьте, что они не конфликтуют с новым каналом.
- **Поведение в режимах `OnClick` / `OnUpInBorders` внутри `ScrollRect` изменилось** — drag во время скролла теперь корректно гасит клик. Если в проекте есть кнопки в скролле, чьё прежнее срабатывание «через scroll-жест» было намеренной фичей, она пропадёт. Альтернативно: использовать `OnUpAnywhere` (в этом режиме защиты от drag нет по контракту).
- **`AddOnClick` идемпотентен.** Если код раньше намеренно подписывался дважды для двойного счёта вызовов — теперь это не работает.

### `SpinePauseHandler`

- **Механика паузы для `SkeletonAnimation` изменилась** — раньше менялся `timeScale`/`updateMode`, теперь `enabled = false`. Если в проекте есть код, который во время паузы читает/пишет в `SkeletonAnimation.timeScale` (например, для синхронизации с другим аниматором), он будет работать с замороженным компонентом — поведение может расходиться с ожиданиями.

### `DbRecordAttributeDrawer`

- **Лаг до 1 секунды** между добавлением/удалением пресета в проект и обновлением выпадашки — компромисс ради FPS редактора. На практике незаметно; если критично — TTL настраивается константой `CacheTtlSeconds` (или нужно вызвать `ReloadDatabase()` на драйвере явно).

### `RewardsSystem`

Новый пакет, на существующий код проектов не влияет. Использовать опционально.

### `CursorSystem`

Новый пакет, на существующий код проектов не влияет. Если в проекте до этого был свой кастомный курсор — пакет с ним не конфликтует, но один из двух нужно отключить, чтобы не было race на `Cursor.SetCursor`.

---

## 📊 Сводно по затронутым областям

| Область | Файлов изменено | Доля от total |
|---|---:|---:|
| `Sdk/RewardsSystem/` (новый) | 17 | 21 % |
| `SpineExtensions/UIs/` | 12 | 15 % |
| `Unity/UI/Misc/` | 9 | 11 % |
| `Unity/UI/CursorSystem/` (новый) | 7 | 9 % |
| `Sdk/EffectSpawnSystem/` | 3 | 4 % |
| Прочее (Unity-инфраструктура, Nani, документация) | 32 | 40 % |
| **Итого** | **80** | **100 %** |
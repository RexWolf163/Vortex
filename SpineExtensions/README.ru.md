# SpineExtensions

Мост между фреймворком Vortex и Spine (Esoteric Software). Содержит подпакеты, использующие `Spine.Unity` напрямую и потому вынесенные за пределы основных слоёв.

## Назначение

- Tween-логика, переключающая состояния скелета синхронно с `TweenerHub` — одиночная и взвешенная случайная
- Скраб Spine-анимации по `FloatData` (привязка трека к параметру 0..1)
- Реакция Spine-скелетов на смену `GameStates` (заморозка на паузе/загрузке)
- Переключение скина скелета через `UIStateSwitcher`
- Рассинхрон однотипных анимаций (случайный сдвиг по времени трека)

Все Tween- и Scrub-классы реализованы парой `SkeletonGraphic` (UGUI) / `SkeletonAnimation` (MeshRenderer) поверх общей generic-базы.

Вне ответственности: рантайм Spine, импорт ассетов, рендеринг скелетов, логика катсцен (см. [`NaniExtensions/CutsceneSystem`](../NaniExtensions/README.ru.md)).

## Активация

Пакет активируется через `SdkSettingsSystem`: ассет `SdkSettings` (`Vortex → Configs → SDK Settings`), тоггл **`spineExt`**. Тоггл помечен `[DefineSymbol("USING_SPINE")]` и при изменении синхронизирует символ `USING_SPINE` в Scripting Define Symbols всех платформ.

Сборка `ru.vortex.spine` имеет `defineConstraints: ["USING_SPINE"]` — при выключенном тоггле модуль не компилируется, в плеер не попадает и `Spine.Unity` не загружает. Partial-расширение `SdkSettings` лежит в `DefineSettings/SdkSettings.Spine.cs` и собирается только когда сборка `sdk.settings.system` присутствует (через `.asmref`).

## Сборка

Единый asmdef на весь модуль: **`ru.vortex.spine`** (в корне `SpineExtensions/`).

## Подпапки

| Подпапка | Назначение |
|----------|-----------|
| [TweenerSystem](TweenerSystem/) | `TweenLogic`-реализации: одиночная и взвешенная случайная анимация для `SkeletonGraphic` и `SkeletonAnimation` |
| [UIs](UIs/) | Хэндлеры сцены: скраб-анимация по `FloatData`, заморозка по `GameStates`, рассинхрон, переключатель скина |
| [DefineSettings](DefineSettings/) | Partial-расширение `SdkSettings` с тогглом `spineExt` (`USING_SPINE`) |

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `spine-unity` | `SkeletonGraphic`, `SkeletonAnimation`, `AnimationState`, `SkeletonData`, `TrackEntry` |
| `ru.vortex.unity.ui.misc` | Базовый `TweenLogic`, `DataStorageView<T>`, `StateItem` |
| `ru.vortex.extensions` | `IsNullOrWhitespace`, `ActionExt`, `ReactiveValue`/`FloatData` |
| `ru.vortex.unity.editortools` | Атрибуты `[ValueSelector]`, `[AutoLink]`, `[ClassFilter]`, `[ClassLabel]` |
| `ru.vortex.unity.app` | `TimeController` (планирование re-roll и рассинхрона) |
| `ru.vortex.sdk.game.core` | `GameController`, `GameStates` |
| `ru.vortex.system` | базовые абстракции (через ссылку asmdef) |
| `sdk.settings.system` (через `.asmref`) | partial `SdkSettings` + `[DefineSymbol]` |
| Sirenix Odin Inspector | `[InfoBox]`, `[OnValueChanged]`, `[OnInspectorGUI]`, `[HideReferenceObjectPicker]` |

> Модуль одновременно опирается на сборки слоя 2 (Unity) и слоя 3 (Sdk). Это сделано осознанно: единая сборка под `USING_SPINE` проще в управлении, изоляция от остального фреймворка обеспечивается constraint'ом, а не разделением слоёв.

---

## TweenerSystem

**Namespace:** `Vortex.SpineExtensions.TweenerSystem`

Все классы — `TweenLogic`-реализации, вызываемые из `TweenerHub`. Каждое семейство построено как `Base<TSkeleton>` + два готовых `[Serializable]`-подкласса:

| Класс | Базовый | Тип скелета |
|-------|---------|-------------|
| `SpineAnimationLogic` | `SpineAnimationLogicBase<SkeletonGraphic>` | UGUI |
| `SpineSkeletonAnimationLogic` | `SpineAnimationLogicBase<SkeletonAnimation>` | MeshRenderer |
| `SpineAnimationRandomLogic` | `SpineAnimationRandomLogicBase<SkeletonGraphic>` | UGUI |
| `SpineSkeletonAnimationRandomLogic` | `SpineAnimationRandomLogicBase<SkeletonAnimation>` | MeshRenderer |

Поле `skeleton` в базе ограничено `[ClassFilter(typeof(IAnimationStateComponent), typeof(IHasSkeletonDataAsset))]` — оба интерфейса задаются Spine и общие для UGUI- и Mesh-вариантов.

### SpineAnimationLogicBase&lt;T&gt; — одиночная анимация

Бинарный принцип переключения:
- `value == 0` → idle-анимация `animationIdle0`
- `value == 1` → idle-анимация `animationIdle1`
- промежуточное значение и переход вперёд → анимация `animationFrw`, далее `animationIdle1`
- промежуточное значение и переход назад → анимация `animationBack`, далее `animationIdle0`

Если соответствующая анимация не указана (или `[NONE]`) — переключение в эту сторону пропускается.

| Поле | Тип | Описание |
|------|-----|----------|
| `skeleton` | `TSkeleton` | Целевой скелет |
| `animationChannel` | `byte` (0..10) | Канал AnimationState (track index) |
| `animationIdle0` | `string` (selector) | Анимация в положении Back |
| `animationIdle1` | `string` (selector) | Анимация в положении Forward |
| `animationFrw` | `string` (selector) | Анимация перехода в Forward |
| `animationBack` | `string` (selector) | Анимация перехода в Back |
| `skipIfNotEqual` | `bool` | Запускать переход только если активна совпадающая исходная idle-анимация |

`SwitchOn` / `SwitchOff` управляют активностью `skeleton.gameObject` (соответствует `TweenPreset.offOnStartPoint/EndPoint`).

В Editor-режиме `[ValueSelector("GetListAnimations")]` подтягивает список из `skeleton.SkeletonDataAsset`, плюс пункт `[NONE]`.

### SpineAnimationRandomLogicBase&lt;T&gt; — взвешенная случайная анимация

Поведение идентично одиночной версии, но каждое из четырёх полей анимации — массив `SpineAnimationVariant` (имя + вес 0..100). При переключении конкретная анимация выбирается случайным образом с вероятностью, пропорциональной весу.

| Поле | Тип | Описание |
|------|-----|----------|
| `skeleton` | `TSkeleton` | Целевой скелет |
| `animationChannel` | `byte` (0..10) | Канал AnimationState |
| `animationsIdle0` | `SpineAnimationVariant[]` | Варианты idle в Back |
| `animationsIdle1` | `SpineAnimationVariant[]` | Варианты idle в Forward |
| `animationsFrw` | `SpineAnimationVariant[]` | Варианты перехода в Forward |
| `animationsBack` | `SpineAnimationVariant[]` | Варианты перехода в Back |
| `skipIfNotEqual` | `bool` | Запускать переход, только если активна одна из вариантов исходного idle |

**Idle re-roll.** Если массив idle содержит более одного варианта, после установки idle планируется `TimeController.Call` на длительность анимации — по истечении выбирается новый случайный вариант, и цикл повторяется. Это даёт «живой» idle с чередующимися вариациями. Отмена планируется на каждое новое переключение (`CancelIdleReroll` → `TimeController.RemoveCall(this)`).

**Инспектор.** `SpineAnimationVariant` рендерится через `[ClassLabel("$Label")]` с пометкой имени и расчётной долей вероятности (`weight / Σweights`). Пересчёт долей запускается через `[OnValueChanged]`; при смене `skeleton` массивы получают свежий список имён.

### Граничные случаи (общие для обоих семейств)

| Ситуация | Поведение |
|----------|-----------|
| Анимация перехода не задана / массив пуст | Переключение в эту сторону пропускается |
| `skipIfNotEqual = true`, активна другая анимация | Переход не запускается |
| Idle-анимация пустая | На канал ставится `SetEmptyAnimation` |
| Повторный вызов во время перехода | Игнорируется (флаг `_isRunningState`) |
| Сумма весов вариантов ≤ 0 | Анимация не выбирается, переход отменяется |

---

## UIs

**Namespace:** `Vortex.SpineExtensions.UIs`

### SpineAnimationScrubHandler / SpineSkeletonAnimationScrubHandler

Хэндлер «скраб»: привязывает позицию трека Spine-анимации к значению `FloatData` (0..1). Реализованы парой над общей базой `SpineAnimationScrubHandlerBase<TSkeleton>`, наследующей `DataStorageView<FloatData>` (источник данных — любой `IDataStorage`).

Анимация ставится на канал с `TimeScale = 0` (штатный ход замораживается), а `TrackTime` выставляется руками в `OnDataUpdated`:

```
track.TrackTime = Mathf.Clamp01(Data.Value) * track.Animation.Duration;
```

Spine применяет позу в `LateUpdate`.

| Поле | Тип | Описание |
|------|-----|----------|
| `skeleton` | `TSkeleton` | Целевой скелет |
| `animationName` | `string` (selector) | Анимация на скраб-треке |
| `channel` | `byte` (0..10) | Канал AnimationState |
| `source` | `IDataStorage` (от `DataStorageView`) | Источник `FloatData` |

В `DeInit` трек очищается через `SetEmptyAnimation`.

| Подкласс | Тип скелета |
|----------|-------------|
| `SpineAnimationScrubHandler` | `SkeletonGraphic` (UGUI) |
| `SpineSkeletonAnimationScrubHandler` | `SkeletonAnimation` (MeshRenderer) |

### SpinePauseHandler

`MonoBehaviour`-хэндлер, останавливающий скелет на паузе/загрузке. Поддерживает одновременно `SkeletonGraphic` и `SkeletonAnimation` на одном объекте — оба поля привязываются через `[AutoLink]`.

Механика заморозки различается по типу скелета: `SkeletonGraphic` управляется штатным флагом `freeze`, `SkeletonAnimation` — отключением самого MonoBehaviour (`enabled = false`). Когда компонент выключен, Unity не вызывает у него ни `Update`, ни `LateUpdate`, и `AnimationState` физически не двигается, кто бы что снаружи ни писал в поля.

> `timeScale = 0` и `updateMode = UpdateMode.Nothing` намеренно не используются. В проектах со внешним поллингом Spine-актёров (типичный кейс — Naninovel-character: его update-цикл каждый кадр пишет в `timeScale` нужное себе значение для fast-forward / skip-mode) обе настройки затираются за 1–2 кадра, и скелет продолжает анимироваться. `enabled = false` Unity-механизм поллер обратно включить не может — внешние системы работают через свой Actor-слой, а не через прямой доступ к Spine-компонентам, поэтому ссылку на конкретный MonoBehaviour не держат.

| Поле | Тип | Описание |
|------|-----|----------|
| `spine` | `SkeletonGraphic` | UGUI-скелет (опционально) |
| `spineAnimation` | `SkeletonAnimation` | Mesh-скелет (опционально) |

Реакция на `GameController.OnGameStateChanged`:

| `GameStates` | `spine.freeze` | `spineAnimation.enabled` |
|--------------|----------------|--------------------------|
| `Off`, `Play`, `Win`, `Fail` | `false` | `true` |
| `Loading`, `Paused` | `true` | `false` |

Подписка/отписка — в `OnEnable`/`OnDisable`.

### SpineRandomizationStart

Хэндлер для рассинхрона однотипных анимаций: при `OnEnable` через `TimeController.Accumulate` ставится в очередь вызов, который сдвигает `TrackTime` текущего трека на случайное значение в диапазоне `[0; Animation.Duration]`. Используется, когда на сцене несколько одинаковых скелетов и их фазы нужно развести.

| Поле | Тип | Описание |
|------|-----|----------|
| `skeletonGraphic` | `SkeletonGraphic` | UGUI-скелет (опционально), `[AutoLink]` |
| `skeletonAnimation` | `SkeletonAnimation` | Mesh-скелет (опционально), `[AutoLink]` |
| `channelAnimation` | `int` (0..10) | Канал, на котором сдвигается трек |

Отмена запланированного вызова — в `OnDisable` (`TimeController.RemoveCall(this)`).

### SpineSkinSwitch

`StateItem` для `UIStateSwitcher`: при срабатывании состояния назначает указанный скин скелету.

| Поле | Тип | Описание |
|------|-----|----------|
| `skin` | `string` (selector) | Имя скина из `SkeletonData.Skins` |
| `spine` | `SkeletonGraphic` | Целевой скелет |

В `Set()` вызывается `Skeleton.SetSkin` + `SetSlotsToSetupPose` + `UpdateMesh`. В DropDown инспектора `UIStateSwitcher` пункт регистрируется как **Animator Control → Switch Spine Skin**.

---

## Установка

1. Импортировать Spine Unity Runtime (Esoteric Software).
2. Открыть `Vortex → Configs → SDK Settings` и включить тоггл **`spineExt`**. Символ `USING_SPINE` добавится во все платформы автоматически.
3. После включения сборка `ru.vortex.spine` начнёт компилироваться.

Выключение тоггла снимает символ и отключает SpineExtensions целиком без правок кода.

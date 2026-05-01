# SpineExtensions

Мост между фреймворком Vortex и Spine (Esoteric Software). Содержит подпакеты, использующие `Spine.Unity` напрямую и потому вынесенные за пределы основных слоёв.

## Назначение

- Tween-логика, переключающая состояния `SkeletonGraphic` синхронно с `TweenerHub`
- Реакция Spine-скелетов на смену `GameStates` (заморозка на паузе/загрузке)

Вне ответственности: рантайм Spine, импорт ассетов, рендеринг скелетов, логика катсцен (см. [`NaniExtensions/CutsceneSystem`](../NaniExtensions/README.ru.md)).

## Условная компиляция

Все подпакеты SpineExtensions компилируются только при наличии символа `USING_SPINE` (`defineConstraints: ["USING_SPINE"]`). Без символа сборки не попадают в плеер и не загружают `Spine.Unity`. По образцу Steam-интеграции: символ управляется вручную через PlayerSettings или централизованным `DefineSymbolManager`.

## Сборка

Единый asmdef на весь модуль: **`ru.vortex.spine`** (в корне `SpineExtensions/`).

## Подпапки

| Подпапка | Назначение |
|----------|-----------|
| [TweenerSystem](TweenerSystem/) | `SpineAnimationLogic` — TweenLogic для `SkeletonGraphic` |
| [UIs](UIs/) | `SpinePauseHandler` — заморозка скелета по `GameStates` |

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `spine-unity` | `SkeletonGraphic`, `AnimationState`, `SkeletonData` |
| `ru.vortex.unity.ui.misc` | Базовый `TweenLogic` |
| `ru.vortex.extensions` | `IsNullOrWhitespace`, `ActionExt` |
| `ru.vortex.unity.editortools` | Атрибуты `[ValueSelector]`, `[AutoLink]` |
| `ru.vortex.sdk.game.core` | `GameController`, `GameStates` |
| Sirenix Odin Inspector | `[InfoBox]` |

> Модуль одновременно опирается на сборки слоя 2 (Unity) и слоя 3 (Sdk). Это сделано осознанно: единая сборка под `USING_SPINE` проще в управлении, изоляция от остального фреймворка обеспечивается constraint'ом, а не разделением слоёв.

---

## TweenerSystem

**Namespace:** `Vortex.SpineExtensions.TweenerSystem`

### SpineAnimationLogic

Реализация `TweenLogic`, переключающая состояния скелета синхронно с прогрессом твина.

Бинарный принцип переключения:
- `value == 0` → idle-анимация `animationIdle0`
- `value == 1` → idle-анимация `animationIdle1`
- промежуточное значение и переход вперёд → анимация `animationFrw`, далее `animationIdle1`
- промежуточное значение и переход назад → анимация `animationBack`, далее `animationIdle0`

Если соответствующая анимация не указана (или `[NONE]`) — переключение не производится.

| Поле | Тип | Описание |
|------|-----|----------|
| `skeleton` | `SkeletonGraphic` | Целевой скелет |
| `animationChannel` | `byte` (0..10) | Канал AnimationState (track index) |
| `animationIdle0` | `string` (selector) | Анимация в положении Back |
| `animationIdle1` | `string` (selector) | Анимация в положении Forward |
| `animationFrw` | `string` (selector) | Анимация перехода в Forward |
| `animationBack` | `string` (selector) | Анимация перехода в Back |
| `skipIfNotEqual` | `bool` | Запускать переход только если активна совпадающая исходная idle-анимация |

`SwitchOn` / `SwitchOff` управляют активностью `skeleton.gameObject` (соответствует `TweenPreset.offOnStartPoint/EndPoint`).

В Editor-режиме `[ValueSelector("GetListAnimations")]` подтягивает список из `skeleton.SkeletonData.Animations`.

### Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Анимация перехода не задана | Переключение в эту сторону пропускается |
| `skipIfNotEqual = true`, активна другая анимация | Переход не запускается |
| Idle-анимация пустая | На канал ставится `SetEmptyAnimation` |
| Повторный вызов во время перехода | Игнорируется (флаг `_isRunningState`) |

---

## UIs

**Namespace:** `Vortex.SpineExtensions.UIs`

### SpinePauseHandler

`MonoBehaviour`-хэндлер, синхронизирующий флаг `SkeletonGraphic.freeze` с `GameStates`.

| Поле | Описание |
|------|----------|
| `spine` | `SkeletonGraphic`, привязка через `[AutoLink]` |

Реакция на `GameController.OnGameStateChanged`:

| `GameStates` | `freeze` |
|--------------|----------|
| `Off`, `Play`, `Win`, `Fail` | `false` |
| `Loading`, `Paused` | `true` |

Подписка/отписка — в `OnEnable`/`OnDisable`.

---

## Установка

1. Импортировать Spine Unity Runtime (Esoteric Software).
2. В `Project Settings → Player → Scripting Define Symbols` добавить `USING_SPINE` (для всех целевых платформ).
3. После добавления символа сборка `ru.vortex.spine` начнёт компилироваться.

Удаление символа отключает SpineExtensions целиком без правок кода.

# SpineExtensions — Cutscenes

> ⚠️ **Экспериментальный пакет** — спутник [CutsceneSystem](../../Unity/CutsceneSystem/README.ru.md), который не прошёл обкатку в продуктовой эксплуатации. Состав и настройки хэндлеров может измениться вместе с ним без обратной совместимости.

**Namespace:** `Vortex.SpineExtensions.Cutscenes`
**Сборка:** `ru.vortex.spine.cutscene` (defineConstraints: `USING_SPINE`)

---

## Назначение

Всё, что относится к Spine-реализации катсцен для [`CutsceneSystem`](../../Unity/CutsceneSystem/README.ru.md). Сам пакет катсцен про Spine не знает; здесь лежат хэндлеры, которые ставятся в префаб контента.

- `SpineCutsceneAutoNext` — автопереход узла графа катсцены по концу анимации.
- `SpineCutsceneEventSounds` — звуки на события анимации.

Оба хэндлера принимают `SkeletonGraphic` либо `SkeletonAnimation`: задаётся одно поле из двух. Если заданы оба, берётся `SkeletonGraphic`.

Вне ответственности:

- Запуск анимации фазы — это `SpineAnimationSwitch` в состоянии свитчера контента.
- Заморозка скелета на паузе — `SpinePauseHandler`.

У подпакета своя сборка, а не общая `ru.vortex.spine`: ему нужны ссылки на пакет катсцен, звук и базу, которые остальным Spine-расширениям не требуются.

---

## Зависимости

| Зависимость | Назначение |
|---|---|
| `spine-unity`, `spine-csharp` | скелет, `AnimationState`, события |
| `Vortex.Unity.CutsceneSystem` | `CutsceneContentHandler.Next` — сигнал автоперехода |
| `Vortex.Core.AudioSystem`, `Vortex.Unity.AudioSystem` | `AudioController.PlaySound`, тип записи `Sound` |
| `Vortex.Core.DatabaseSystem`, `Vortex.Unity.DatabaseSystem` | `[DbRecord]` — выбор записи звука |

---

## SpineCutsceneAutoNext

Когда текущая анимация трека отыграла проход, шине уходит сигнал «дальше». Сигнал идёт через хэндлер контента (`CutsceneContentHandler.Next()`): тот называет шине показанный узел графа, и сигнал от узла, с которого граф уже ушёл, отбрасывается. В графе у такого узла переход «по `Next`».

| Поле | Назначение |
|---|---|
| `content` | хэндлер контента катсцены, через него уходит сигнал |
| `skeletonGraphic` / `skeletonAnimation` | скелет катсцены |
| `track` | трек, конец анимации которого ждём |

- Работает, пока включён, и срабатывает **один раз за включение**.
- Ставится на объект, который включает состояние фазы с автопереходом (`GameObjectsSwitch`). В фазах, ждущих команды, его быть не должно.
- Учитывается только текущая анимация трека: уходящая в миксе анимация сигнал не даёт.
- На паузе скелет заморожен, поэтому сигнала нет.

---

## SpineCutsceneEventSounds

На событие скелета играет запись звука из базы.

| Поле | Назначение |
|---|---|
| `skeletonGraphic` / `skeletonAnimation` | скелет катсцены |
| `sounds` | таблица: имя события → запись `Sound` |

- Имя события выбирается из списка событий скелета.
- Звуки событий лёгкие и повторяются между катсценами, поэтому берутся из общей базы и вместе с контентом не выгружаются.
- Событие без строки в таблице или с пустым звуком пропускается.
- Уже звучащий звук события на паузе доигрывает; новых на паузе не появляется.

---

## Пример

`Example/baseSpine42Cutscene.prefab` и `Example/baseSpine43Cutscene.prefab` — основы префаба контента Spine-катсцены под Spine 4.2 и 4.3. Ниже — основа 4.2: мировой объект, корень — `Transform`, скелет — `SkeletonAnimation`.

```
baseSpine42Cutscene     StateGraphView, CutsceneContentHandler, UIStateSwitcher
├── spine               SkeletonAnimation, SpinePauseHandler; выключен
├── [AutoNextControl]   SpineCutsceneAutoNext; выключен
└── [AudioControl]      AdvancedMusicHandler, AdvancedSoundHandler, SpineCutsceneEventSounds; выключен
```

| Состояние | Элементы |
|---|---|
| `End` | пусто — состояние конечного узла графа |
| `Phase 1 (auto next)` | `GameObjectsSwitch` (`spine`, `[AudioControl]`); `SpineAnimationSwitch` без `loop`; `GameObjectsSwitch` (`[AutoNextControl]`) — фаза уходит дальше сама по концу анимации |
| `Phase 2 (loop)` | `GameObjectsSwitch` (`spine`, `[AudioControl]`); `SpineAnimationSwitch` с `loop` — фаза ждёт команды «дальше» |

- **Порядок элементов состояния важен:** сначала включение `spine`, потом `SpineAnimationSwitch`. Скелет создаёт `AnimationState` при включении объекта.
- **`[AutoNextControl]` выключается без задержки**, остальные объекты — с отложенным выключением. Поэтому автопереход взводится заново в каждой фазе, а скелет и звук между фазами не перезапускаются.
- **Слоты звука** — хэндлеры [`AudioAdvancedControl`](../../Sdk/AudioAdvancedControl/README.ru.md) из SDK.
- **Граф катсцены** в основе не задан: у каждой катсцены свой. `StateGraphView` в режиме `Restart` без задержки переключения.

Своя катсцена:

1. Создать вариант префаба (`Prefab Variant`).
2. Назначить `SkeletonDataAsset` в `SkeletonAnimation`.
3. В состояниях фаз выбрать анимации вместо заглушки `[NONE]`; новая фаза — новое состояние.
4. Создать граф катсцены (`Database/State Graph`): узел на каждое состояние с тем же именем, включая `End`; фазы связать переходами «по `Next`», последнюю — с `End`. Назначить граф в `StateGraphView`. Кнопка Sync у View сверяет состояния с узлами.
5. На `[AudioControl]` заполнить таблицу звуков событий и клипы музыкального и звукового хэндлеров.

Шаги 1–4 умеет делать [фабрика ассетов](../../../Plugins/AssetFactory/README.ru.md).

Ограничения основы:

- Пока скелет не назначен, свитчер в редакторе пишет ошибку проверки состояний.
- Слот звука с пустым списком клипов при включении пишет `Debug.LogError` «Список клипов пуст». Ненужный хэндлер слота удаляется с `[AudioControl]`: все три компонента стоят на одном объекте, и убрать объект из списков `GameObjectsSwitch` значит отключить их все.
- Для катсцены под канвасом нужен `SkeletonGraphic`: компонент скелета и ссылки на него в хэндлерах и состояниях меняются вручную.

Основа спрайтовой катсцены лежит в [`Sdk/CutsceneSystem`](../../Sdk/CutsceneSystem/README.ru.md).

---

## Граничные случаи

| Ситуация | Поведение |
|---|---|
| Не задан ни один скелет | `NullReferenceException` при включении |
| У скелета нет данных | `NullReferenceException` при включении |
| Анимация трека сменилась до конца прохода | ждётся конец новой анимации |
| Событие встречается в таблице дважды | играет первая строка; если звук в ней пуст, не играет ничего |

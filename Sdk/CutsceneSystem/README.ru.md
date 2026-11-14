# CutsceneSystem

**Namespace:** `Vortex.Sdk.CutsceneSystem.*`
**Сборка:** `ru.vortex.sdk.cutscene` (defineConstraints: `USING_SPINE`)

---

## Назначение

Показ Spine-катсцены, разбитой на фазы: одна катсцена в моменте, переход по фазам командой или автоматически, музыка, эмбиент фазы и звуки событий анимации.

- **Шина управления** `CutsceneController` — хранит, какая катсцена открыта и на какой она фазе, сообщает об изменениях событиями.
- **Представление** `CutsceneView` — показывает открытую катсцену на своём Spine-компоненте, озвучивает её, грузит и отпускает её ассеты.
- **Описание катсцены** `CutsceneData` — лёгкий ассет: скелет, эмбиенты и музыка лежат за `AssetHandle<T>` и занимают память только на время показа.
- **Каталог** `CutsceneCatalog` — индекс катсцен проекта по ключу (имя ассета).

Вне ответственности:

- **Запуск катсцены.** Пакет не знает, кто и когда открывает катсцену: сценарий, галерея и хэндлеры запуска вызывают `CutsceneController.Open` сами.
- **Naninovel.** Пакет от него не зависит.
- **Заморозка скелета на паузе.** Её делает `SpinePauseHandler` из `SpineExtensions`.
- **Разблокировка и сохранение.** Состояние показа в сейв не входит.

---

## Зависимости

| Зависимость | Назначение |
|---|---|
| `spine-unity`, `spine-csharp` | `SkeletonAnimation`, `SkeletonGraphic`, `SkeletonDataAsset`, `AnimationState` |
| `Vortex.Core.AssetHandleSystem` | `AssetHandle<T>` — скелет и клипы катсцены |
| `Vortex.Core.AudioSystem`, `Vortex.Unity.AudioSystem` | `AudioController`, хэндлы управления звуком, `SoundClip` (выбор клипа с антиповтором), `AntiRepeatMode`, `[AudioChannelName]` |
| `Vortex.Core.DatabaseSystem`, `Vortex.Unity.DatabaseSystem` | `[DbRecord(typeof(Sound))]` — звуки событий анимации |
| `Vortex.Sdk.Core.GameCore` | `GameController.OnGameStateChanged`, `GameStates` — пауза и закрытие при выходе из сессии |
| `Vortex.Unity.Extensions` | `AssetDatabaseExt.GetSingletonAsset` — поиск каталога |
| `UniTask` | асинхронная загрузка внутри представления |
| Odin Inspector | атрибуты инспектора модели и каталога |

Реализации `AssetHandle<T>` пакет не выбирает: `DirectAssetHandle<T>` или `AddressableAssetHandle<T>` задаётся в данных катсцены.

---

## Использование

### Настройка катсцены (для дизайнера)

1. Создать ассет: `Create → Vortex → Cutscenes → Cutscene Data`. Имя ассета — ключ катсцены.
2. В поле `skeleton` выбрать реализацию хэндла и указать `SkeletonDataAsset`.
3. Нажать **Sync Spine Phases** — фазы и события подтянутся из скелета.
4. Расставить порядок фаз. Для каждой фазы:
   - `Animation Looped` включён — зафиксированный луп: фаза крутится до команды `Next`;
   - выключен — сквозная фаза: отыграв один проход, сама переключается на следующую.
5. Задать эмбиенты фаз, музыку и звуки событий (см. «Звук»).
6. Добавить катсцену в `CutsceneCatalog` (`Create → Vortex → Cutscenes → Cutscene Catalog`, один на проект).

### Объект представления

На объекте сцены или префабе:

- `CutsceneView` — в нём задаётся ровно одно из полей: `Skeleton Animation` либо `Skeleton Graphic`;
- сам Spine-компонент без назначенного скелета — скелет подставит представление;
- `SpinePauseHandler` — чтобы анимация замирала на паузе.

### Запуск из кода

```csharp
public class CutsceneLauncher : MonoBehaviour
{
    [CutsceneKey] [SerializeField] private string cutscene;

    public void Launch() => CutsceneController.Open(cutscene);
    public void Continue() => CutsceneController.Next();
    public void Interrupt() => CutsceneController.Close();
}
```

`Open` принимает и сам ассет: `CutsceneController.Open(cutsceneData)` — каталог для этого не нужен.

---

## Архитектура

```
Bus
  CutsceneController (static)      — состояние показа + события; ассеты не грузит, ничего не показывает

Models/
  CutsceneModel                    — состояние показа: Cutscene, PhaseIndex (пишет только контроллер)
  CutsceneData : ScriptableObject  — описание катсцены
  CutscenePhase                    — фаза: анимация, зацикленность, эмбиент
  EventToAudioData                 — событие анимации → звук из базы
  CutsceneSound                    — набор клипов за AssetHandle + канал + антиповтор

Catalog/
  CutsceneCatalog : ScriptableObject — индекс «ключ → CutsceneData»

View/
  CutsceneView : MonoBehaviour     — показ, звук, загрузка и отпуск ассетов

Attributes/, Editor/
  CutsceneKeyAttribute (+ drawer)  — выпадашка ключей каталога на string-поле
```

Контроллер статический, без конфига и без интерфейса: у него три команды и два поля состояния.

### Фазы

Анимация фазы всегда запускается зацикленной. Различается только выход из фазы:

| Фаза | `AnimationLooped` | Выход |
|---|---|---|
| Зафиксированный луп | да | только по `CutsceneController.Next()` |
| Сквозная | нет | сама, по концу первого прохода |

Номер фазы хранит контроллер. О конце прохода сквозной фазы представление сообщает ему через `CompletePhase(index)`, фазу переключает контроллер — тем же событием, что и `Next`. На сквозной фазе `Next` ничего не делает: это исключает двойной шаг, когда команда совпала с автопереходом.

### Звук

| Что | Где задаётся | Хранение | Жизнь в памяти |
|---|---|---|---|
| Музыка | `CutsceneData.music` | `CutsceneSound` | на время показа |
| Эмбиент фазы | `CutscenePhase.Ambient` | `CutsceneSound` | на время показа |
| Звук события анимации | `EventToAudioData.AudioPack` | `[DbRecord(typeof(Sound))]` | по политике базы звуков |

`CutsceneSound` — набор клипов, канал и механика антиповтора. На каждое проигрывание выбирается один клип; выбор делает `SoundClip` из AudioSystem — так же, как у звуков базы. Громкость и высота не настраиваются.

Звуки событий лёгкие и повторяются между катсценами, поэтому идут из общей базы и вместе с катсценой не грузятся.

**Эмбиент фазы** определяется пересчётом от текущей фазы назад:

- у фазы задан эмбиент — звучит он;
- не задан, `EmptyAmbient = Inherit` — берётся эмбиент ближайшей прошлой фазы;
- не задан, `EmptyAmbient = Stop` — тишина.

При переходе на фазу с тем же итоговым эмбиентом звук не перезапускается.

**Музыка** играет через слот cover-музыки AudioSystem. Клип выбирается один раз на показ; после паузы звучит он же.

Свои звуки представление запускает с хэндлом управления и останавливает само. Чужие звуки оно не трогает.

### Загрузка

Загрузкой и отпуском владеет представление.

```
CutsceneController.OnOpened
    ↓
[View] стартовый набор, параллельно:
    ├── скелет
    ├── музыка
    └── эмбиент текущей фазы
    ↓
[View] скелет установлен → анимация фазы, музыка, эмбиент
    ↓
[View] в фоне: эмбиенты остальных фаз
```

- Фаза переключается сразу: все анимации лежат в одном скелете. Эмбиент фазы, если ещё грузится, включается по окончании загрузки.
- Закрытие катсцены и `OnDisable` отменяют незавершённые загрузки и отпускают все загруженные хэндлы.
- Признак загрузки хэндла живёт в самом ассете `CutsceneData`: рантайм-копии данных нет. Владелец хэндлов — активное представление, поэтому оно одно.

### Пауза

| `GameStates` | Звук представления | Скелет |
|---|---|---|
| `Paused`, `Loading` | эмбиент и звуки событий — на паузу, музыка остановлена | замораживает `SpinePauseHandler` |
| остальные | снятие паузы, музыка запускается снова | размораживает `SpinePauseHandler` |
| `Off` | контроллер закрывает катсцену | — |

Представление скелет не замораживает: `timeScale = 0` затирается внешним поллингом Spine-актёров (см. README `SpineExtensions`).

---

## Контракт

### Гарантии

- **Катсцена одна.** `Open` поверх открытой сначала закрывает прежнюю: подписчик получает `OnClosed`, затем `OnOpened`.
- **Состояние раньше события.** `Data` меняется до рассылки — подписчик всегда видит итог команды, в том числе при вызове команд из обработчика.
- **Поздний подписчик.** Представление, включённое при уже открытой катсцене, подхватывает её с текущей фазы.
- **Идемпотентность.** `Close` без открытой катсцены, `Next` на последней фазе, устаревший `CompletePhase` — ничего не делают.
- **Отпуск.** Всё, что представление загрузило, оно отпускает на закрытии катсцены и на `OnDisable` (в том числе при уничтожении объекта).

### Дисциплина

- **Активное представление одно.** Второе, включённое одновременно, в показе не участвует — ошибка в лог.
- **В `CutsceneView` задан ровно один скелет.**
- **`CutsceneCatalog` один на проект** и лежит в `Resources` — иначе в билде поиск по ключу не работает.
- **`SpinePauseHandler` на объекте представления** — без него анимация на паузе не замирает.
- **Ключи анимаций фаз существуют в скелете.** Сквозная фаза с несуществующей анимацией не завершится.

### Ограничения

- Сбой загрузки и ошибки настройки пишутся в лог; катсцена при этом остаётся открытой — закрывает её вызывающий код.
- Слот cover-музыки в AudioSystem один на приложение: катсцена и другая система, играющая cover-музыку одновременно, перебивают друг друга.
- В режиме редактирования скелет ленивого хэндла ищется по GUID его поля `reference` — выпадашки анимаций и **Sync Spine Phases** зависят от этого имени поля в `AddressableAssetHandle<T>`.

---

## Граничные случаи

| Ситуация | Поведение |
|---|---|
| `Open(null)` | `Debug.LogError`, состояние не меняется |
| `Open` катсцены без фаз | `Debug.LogError`, состояние не меняется |
| `Open(key)` без каталога или с незнакомым ключом | `Debug.LogError`, состояние не меняется |
| `Open` при открытой катсцене | `OnClosed`, затем `OnOpened` |
| `Next` на сквозной фазе | ничего |
| `Next` / конец сквозной фазы на последней фазе | ничего; анимация продолжает крутиться |
| `Next` раньше, чем загрузился стартовый набор | номер фазы меняется; представление стартует уже с новой фазы |
| `Next` раньше, чем загрузился эмбиент фазы | анимация переключается сразу, эмбиент включается по загрузке |
| `GameStates.Off` | катсцена закрывается |
| Представление включено при открытой катсцене | показ с текущей фазы |
| Представление выключено и включено снова | ассеты отпущены и загружены заново, показ с текущей фазы |
| Второе представление включено одновременно | `Debug.LogError`, в показе не участвует |
| В представлении не задан скелет или заданы оба | `Debug.LogError`, показа нет |
| У катсцены не задан или пуст скелет | `Debug.LogError`, показа нет |
| В скелете нет анимации фазы | `Debug.LogError`; анимация не меняется, эмбиент переключается |
| Сбой загрузки скелета | `Debug.LogError`, показа нет, катсцена остаётся открытой |
| Сбой загрузки звука | `Debug.LogError`, показ идёт без этого звука |
| Пустой элемент в наборе клипов | пропускается |
| У события анимации не задан звук | ничего |
| Дубли имён в каталоге | предупреждение в инспекторе каталога и в лог; по ключу отдаётся первая катсцена |
| Пауза во время загрузки | звук не запускается до снятия паузы |

---

## Editor

- **Sync Spine Phases** (кнопка `CutsceneData`) — удаляет фазы, чьих анимаций нет в скелете, добавляет фазы для новых анимаций; то же для событий, дубли событий удаляются.
- **Выпадашки** `AnimationKey` и `EventName` — списки анимаций и событий скелета катсцены.
- **`[CutsceneKey]`** — выпадашка ключей каталога на `string`-поле. Первый пункт `[NONE]` — пустое значение. Ключ, которого больше нет в каталоге, не перезаписывается: поле подсвечивается красным с пометкой «отсутствует», замена — только явным выбором.
- **Каталог** — предупреждение о дублях ключей прямо в инспекторе.

---

## Публичный API

```csharp
namespace Vortex.Sdk.CutsceneSystem
{
    public static class CutsceneController
    {
        public static CutsceneModel Data { get; }
        public static bool IsOpen { get; }
        public static IReadOnlyList<string> AllKeys { get; }

        public static event Action OnOpened;
        public static event Action OnPhaseChanged;
        public static event Action OnClosed;

        public static void Open(CutsceneData cutscene);
        public static void Open(string key);
        public static void Next();
        public static void CompletePhase(int phaseIndex);   // сообщение представления
        public static void Close();
    }
}

namespace Vortex.Sdk.CutsceneSystem.Models
{
    public class CutsceneModel
    {
        public CutsceneData Cutscene { get; }    // null — катсцена не открыта
        public int PhaseIndex { get; }
    }

    public class CutsceneData : ScriptableObject
    {
        public AssetHandle<SkeletonDataAsset> Skeleton { get; }
        public List<CutscenePhase> Phases { get; }
        public List<EventToAudioData> EventToAudioDatas { get; }
        public CutsceneSound Music { get; }
    }

    public class CutscenePhase
    {
        public enum EmptyAmbientMode { Inherit, Stop }

        public string AnimationKey { get; }
        public bool AnimationLooped { get; }
        public CutsceneSound Ambient { get; }
        public EmptyAmbientMode EmptyAmbient { get; }
    }

    public class EventToAudioData
    {
        public string EventName { get; }
        public string AudioPack { get; }         // GUID записи Sound
    }

    public class CutsceneSound
    {
        public IReadOnlyList<AssetHandle<AudioClip>> Clips { get; }
        public string Channel { get; }
        public AntiRepeatMode AntiRepeat { get; }
        public bool IsEmpty { get; }
    }
}

namespace Vortex.Sdk.CutsceneSystem.Catalog
{
    public class CutsceneCatalog : ScriptableObject
    {
        public IReadOnlyList<string> Keys { get; }
        public CutsceneData Get(string key);
    }
}

namespace Vortex.Sdk.CutsceneSystem.View
{
    public class CutsceneView : MonoBehaviour { }   // настраивается в инспекторе
}

namespace Vortex.Sdk.CutsceneSystem.Attributes
{
    public class CutsceneKeyAttribute : PropertyAttribute { }
}
```

Своё представление вместо `CutsceneView` пишется по тому же контракту: подписка на три события контроллера, чтение `CutsceneController.Data`, вызов `CompletePhase(index)` по концу прохода сквозной фазы, загрузка и отпуск хэндлов катсцены на своей стороне.

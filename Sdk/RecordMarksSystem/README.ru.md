# RecordMarksSystem

**Namespace:** `Vortex.Sdk.RecordMarksSystem.*`
**Assembly:** `ru.vortex.sdk.recordmarks`

---

## Назначение

Реестр именованных булев-меток на GUID пресетов `Database`. Метка — факт «для этой записи произошло такое событие» (разблокировано, посмотрено, покупалось). Хранит только принадлежность GUID к метке — контент разблокированных сущностей вне ответственности пакета.

Возможности:

- Именованные метки. Каждое имя декларируется в SO-конфиге и живёт либо per-slot, либо per-account.
- Ratchet-семантика: `Mark` идемпотентен, `Unmark` доступен, но по контракту — для debug/cheat.
- Роутинг сохранения. `slotMarks` пишутся в `SaveController` через `IGameData`; `globalMarks` — в `GlobalSaveController` через `IGlobalData`.
- События изменения. `OnMarked` / `OnUnmarked` фаерятся в конце кадра батчем через `TimeController.Accumulate`.
- Реактивный пайплайн Vortex. `GameController.CallUpdateEvent` поднимается вместе с батчем событий.
- Editor-окно. Read-only дамп текущего состояния через `Tools/Vortex/Record Marks/Index`.
- Static Bootstrap. Инициализируется через `[RuntimeInitializeOnLoadMethod]`, не входит в `Loader`/`DriverConfig`.

Вне ответственности:

- Контент разблокированных сущностей (галерея, катсцены, записи бестиария).
- Подсчёт полного списка кандидатов (для прогресса «X из Y») — задача consumer'а.
- UI-хендлеры и представление меток.
- Driver-подстановка. Пакет фиксирован на связку `SaveController` + `GlobalSaveController`.

---

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `Vortex.Sdk.Core` (`GameController`, `GameModel.IGameData`) | Резолв слот-модели, батчинг обновлений |
| `Vortex.Core.SaveSystem` (`GlobalSaveController`, `IGlobalData`) | Резолв global-модели, commit после мутации |
| `Vortex.Core.LoggerSystem` | Диагностика |
| `Vortex.Unity.AppSystem.System.TimeSystem` (`TimeController.Accumulate`) | Батчинг событий за кадр |
| `Vortex.Unity.CoreAssetsSystem` (`ICoreAsset`) | Автосоздание SO настроек в `Resources/Settings/` |
| Unity Engine | `ScriptableObject`, `Resources.Load`, `RuntimeInitializeOnLoadMethod`, EditorWindow |

---

## Архитектура

```
[Resources/Settings/RecordMarksSettings.asset]     — SO с двумя списками имён
             │
             ▼
      RecordMarksBus (static)                       — точка входа, API + события
             │
             ├──► RecordMarksSlotData  : IGameData    — Dictionary<mark, List<guid>>
             │      (per-slot, в GameModel)
             │
             └──► RecordMarksGlobalData : IGlobalData — Dictionary<mark, List<guid>>
                    (per-account, в GlobalModel)
```

Bootstrap идёт двухфазно:

1. `[RuntimeInitializeOnLoadMethod BeforeSceneLoad]` — сбрасывает статик-состояние, читает SO, валидирует имена меток, подписывается на `GlobalSaveController.OnInit` и `GameController.OnNewGame` / `OnLoadGame`.
2. Готовность выставляется по факту резолва обеих моделей. До `IsReady = true` любой `Mark`/`Unmark` — warning + no-op.

Runtime-кеш `Dictionary<mark, HashSet<guid>>` держит O(1)-операции над множеством GUID. Модели хранят `List<guid>` — потому что сериализатор Vortex обрабатывает только `IList`, не `ISet`. Bus поддерживает `List` и `HashSet` синхронно при каждой мутации.

Merge-семантика Vortex-сериализатора: при загрузке слота внешний `Dictionary<mark, List<guid>>` мержится (новые метки из SO остаются с пустыми списками), внутренний `List<guid>` заменяется целиком (state берётся из сейва).

События `OnMarked` / `OnUnmarked` не фаерятся синхронно. Каждая мутация добавляет запись в очередь `_pending` и планирует `FlushEvents` через `TimeController.Accumulate` — очередь опустошается один раз за `LateUpdate`, вместе с ней поднимается `GameController.CallUpdateEvent`.

---

## Контракт

### Вход

- `Assets/Resources/Settings/RecordMarksSettings.asset` — SO со списками `slotMarks: string[]` и `globalMarks: string[]`. Имена уникальны в пределах каждого списка и не пересекаются между списками. Формат имён не валидируется — только на непустоту.
- `GlobalSaveController` — готов (проходит `IProcess`). `GameController` — прошёл `NewGame` или `LoadGame`.

### Выход

- `RecordMarksBus.IsMarked(mark, guid)` — есть ли метка на GUID.
- `RecordMarksBus.GetMarked(mark)` — все GUID под меткой.
- События `OnReady`, `OnMarksLoaded`, `OnMarked`, `OnUnmarked`.
- Мутация `RecordMarksSlotData.Data` / `RecordMarksGlobalData.Data` — только через Bus. Поле `Data` — `internal`, снаружи сборки недоступно.
- Сериализация — стандартный контракт `IGameData` / `IGlobalData`; ручного save-кода в пакете нет.

### Гарантии

- **Ratchet.** `Mark(m, g)` при уже помеченном `(m, g)` — тихий no-op, событие не фаерится.
- **Батчинг.** `OnMarked` / `OnUnmarked` фаерятся один раз за `LateUpdate`, в порядке накопления. Между мутацией и событием есть задержка в 1 кадр.
- **Owner-lock.** Единственный писатель `_slotCache` / `_globalCache` / `_pending` — Bus. Модели закрыты **структурно на уровне сборки**: поле `Data` — `internal` + `[IsPOCO]`, снаружи сборки его нельзя ни прочитать, ни заменить; сериализатор Vortex работает с ним рефлексией.
- **Идемпотентность повторного Bootstrap.** Fast Enter Play не удваивает подписки: `-= then +=` перед каждой.
- **Идемпотентность OnGlobalReady / OnSlotChanged.** Дублирующий вызов пересинхронизирует состояние с SO без потери данных.
- **Изоляция слотов.** При `OnLoadGame` / `OnNewGame` `_pending.Clear()` вызывается первой строкой — событие предыдущей сессии не долетит до consumer'а следующей.
- **Fail-safe диагностика.** Unknown mark, empty guid, mutation до `IsReady`, пересекающаяся метка, метка из save не в SO — все дают Log-warning / Log-error, не крашат систему.

### Ограничения

| Ограничение | Причина |
|-------------|---------|
| Один SO на проект в `Resources/Settings/` | Bootstrap читает единственный ассет по фиксированному пути |
| Имя метки — глобально уникальная строка | `SlotMarks ∩ GlobalMarks = ∅`, дубликат внутри списка тоже исключается из индекса |
| Смена имени метки между релизами теряет её state в сейве | Стабильный ключ — контракт разработчика |
| Live-editing SO в рантайме не поддерживается | Bootstrap читает SO один раз |
| Отсутствие SO отключает пакет | SO авто-создаётся как `ICoreAsset`; если всё же нет — LogError + все `Mark`/`Unmark` возвращают warning |
| Смерть GUID (удалён пресет) не чистит его из HashSet автоматически | Cleanup — задача Editor-инструмента или миграции |
| Не потокобезопасен | Все операции — Unity main-thread |

---

## API

### `RecordMarksBus` (static)

| Член | Описание |
|------|----------|
| `bool IsReady { get; }` | Обе модели резолвлены, SO прочитан, пакет готов к мутациям |
| `event Action OnReady` | Первый раз, когда `IsReady` перешёл в `true` |
| `event Action OnMarksLoaded` | После каждого `OnSlotChanged` (`NewGame` / `LoadGame`). Consumer перечитывает состояние |
| `event Action<string, string> OnMarked` | `(mark, guid)` — метка выставлена; батч в конце кадра |
| `event Action<string, string> OnUnmarked` | `(mark, guid)` — метка снята; батч в конце кадра |
| `void Mark(string mark, string guid)` | Пометить GUID меткой. Ratchet: повторный вызов — no-op |
| `void Unmark(string mark, string guid)` | Снять метку. Публичен, но по контракту — для debug/cheat |
| `bool IsMarked(string mark, string guid)` | Проверка наличия |
| `IReadOnlyCollection<string> GetMarked(string mark)` | Все GUID под меткой; live-обёртка над внутренним HashSet, no-alloc |

### `RecordMarksSettings` (SO)

| Поле | Описание |
|------|----------|
| `SlotMarks : IReadOnlyList<string>` | Метки, живущие per-slot (`SaveController`) |
| `GlobalMarks : IReadOnlyList<string>` | Метки, живущие per-account (`GlobalSaveController`) |

Создание: автоматически — SO реализует `ICoreAsset`, контроллер Core Assets кладёт экземпляр в `Resources/Settings/` (`Tools → Vortex → Debug → Check Core Assets`, либо на перезагрузке домена при включённом авто-режиме). Вручную — `Create → Vortex → Settings → RecordMarks`.

### Модели (для рефлексии реестров, не для прямой работы)

| Тип | Роль |
|-----|------|
| `RecordMarksSlotData : GameModel.IGameData` | Регистрируется рефлексией в `GameModel` |
| `RecordMarksGlobalData : IGlobalData` (`GetGlobalKey() = "Vortex.RecordMarks.Global"`) | Регистрируется рефлексией в `GlobalSaveController` |

Поле `Data` обеих моделей — `internal` + `[IsPOCO]`: снаружи сборки пакета его не заменить и не прочитать (owner-lock у `RecordMarksBus`), а сериализатор Vortex работает с ним рефлексией.

---

## Использование

### Настройка

1. SO настроек создаётся автоматически (реализует `ICoreAsset`): `Tools → Vortex → Debug → Check Core Assets`, либо на перезагрузке домена при включённом авто-режиме. Вручную — `Create → Vortex → Settings → RecordMarks` в `Assets/Resources/Settings/`.
2. Заполнить `slotMarks` и `globalMarks` уникальными именами. Рекомендуемая форма — дот-неймспейс: `"gallery.unlocked"`, `"codex.viewed"`, `"shop.everPurchased"`.

### Потребитель

```csharp
public class GalleryCardView : MonoBehaviour
{
    [SerializeField] private string _cardGuid;

    private void OnEnable()
    {
        RecordMarksBus.OnMarked += HandleMarked;
        Refresh();
    }

    private void OnDisable()
    {
        RecordMarksBus.OnMarked -= HandleMarked;
    }

    private void HandleMarked(string mark, string guid)
    {
        if (mark == "gallery.unlocked" && guid == _cardGuid)
            Refresh();
    }

    private void Refresh()
    {
        var unlocked = RecordMarksBus.IsMarked("gallery.unlocked", _cardGuid);
        // переключить UI
    }
}
```

### Мутация

```csharp
// Штатное — только Mark. Идемпотентно.
RecordMarksBus.Mark("gallery.unlocked", cardGuid);

// Debug/cheat.
RecordMarksBus.Unmark("gallery.unlocked", cardGuid);
```

### Прогресс

```csharp
// RecordMarks знает только сколько помечено. Полный список кандидатов —
// на стороне consumer'а (обычно перечислением пресетов Database).
var unlockedCount = RecordMarksBus.GetMarked("gallery.unlocked").Count;
var total = Database.GetRecords<GalleryCard>().Length;
Debug.Log($"{unlockedCount} / {total}");
```

### Editor-окно

`Tools → Vortex → Record Marks → Index` — read-only дамп текущего состояния Bus. Работает только в Play Mode после Bootstrap. Фильтр по имени, foldout по каждой метке со списком GUID.

`Tools → Vortex → Record Marks → Settings` — подсветить SO-конфиг в Project window.

---

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| SO не найден в `Resources/Settings/RecordMarksSettings` | В норме не бывает — SO авто-создаётся (`ICoreAsset`); если авто-создание не отработало: `LogError` одноразово в Bootstrap, `IsReady` навсегда `false`, каждый `Mark`/`Unmark` даёт warning |
| Метка присутствует и в `slotMarks`, и в `globalMarks` | `LogError`, исключается из обоих списков; операции с этим именем возвращают no-op |
| Пустая строка / whitespace в списке | `LogWarning`, пропускается |
| Дубликат в пределах одного списка | `LogWarning`, второе и последующие пропускаются |
| `Mark(unknown, guid)` | `LogWarning`, no-op |
| `Mark(mark, "")` | `LogWarning`, no-op |
| `Mark` до `IsReady` | `LogWarning`, no-op |
| `Mark(m, g)` при уже помеченном | Тихий no-op, событие не фаерится |
| `Unmark(m, g)` при непомеченном | Тихий no-op, событие не фаерится |
| Метка в сейве, отсутствующая в SO | `LogWarning`, ключ удаляется из модели при `Sync` |
| Метка в SO, отсутствующая в сейве | Ключ создаётся с пустым списком |
| GUID помеченного пресета удалён из Database | Мёртвый ID в HashSet, вреда не наносит; чистка — задача миграции |
| `NewGame` во время активной сессии | `_pending.Clear()` первой строкой `OnSlotChanged`, событие предыдущей сессии не долетит |
| Bus обращается из другого треда | Не поддерживается; операции ожидаются с main-thread |
| Fast Enter Play, статик-состояние не обнулилось | `-= then +=` перед подпиской — не удваивает регистрацию |
| Consumer вызывает `Mark` из handler'а `OnMarked` | Nested Mark добавляется в текущую волну FlushEvents; ratchet-гард гарантирует конечность |
| `GetMarked` возвращает `HashSet` через `IReadOnlyCollection` | Downcast к `HashSet<string>` и мутация нарушают контракт read-only |

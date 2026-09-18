# GallerySystem

**Пакет:** SDK-каркас галлереи + Unity-виджет пула

| Часть | Namespace | Assembly | Слой |
|---|---|---|---|
| Контракт | `Vortex.Sdk.GallerySystem.*` | `ru.vortex.sdk.gallery` | SDK (Layer 3) |
| Виджет | `Vortex.Sdk.GallerySystem.View.*` | `ru.vortex.unity.gallery.view` | Unity/UI (Layer 2) |

Физически лежат вместе: `Assets/Vortex/Sdk/GallerySystem/` — контракт и компараторы, `Assets/Vortex/Sdk/GallerySystem/View/` — виджет.

---

## Назначение

Универсальный галлерейный пул: SDK-часть задаёт контракт `IGalleryEntry` и точку сортировки `IGalleryEntryComparer`, Unity-часть предоставляет виджет `GalleryView`, который собирает записи из `Database`, фильтрует их и наполняет `Pool` из `Vortex.Unity.UI.PoolSystem`. Разблокировка — вне ответственности пакета: статус меток управляется извне через `RecordMarksBus`, пакет только читает.

Возможности:

- Тонкий контракт из пяти членов: `GuidPreset`, `Icon`, `Name`, `Show()`, `Hide()`.
- Синхронные `Show`/`Hide` — асинхронка (загрузка ассетов, запуск сценариев) на ответственности реализации.
- Инспектор-настройка «одной сцены галлереи»: набор меток, белый/чёрный список типов, чёрный список guid, оверрайды превью, сортировщик, режим показа закрытых.
- Focus и Show разведены: Focus обновляет реактив выбранного guid и статик-память «последней просмотренной», Show вызывает сама карточка.
- Внешний позиционер `Open(guid)` — для механик поиска/навигации.
- Заглушка пустого пула через `TweenerHub` (Vortex-native, опциональна).
- Все выпадашки в инспекторе — на `[ValueSelector]` от Vortex, без Odin.

Вне ответственности:

- Хранение факта «разблокирована» — это `RecordMarksSystem`. Пакет ни с какой стороны не касается меток.
- Показ конкретного контента карточки (fullscreen viewer, nani-скрипт) — реализация `Show()` в модели пакета-поставщика.
- Механизм поиска карточки — консьюмер использует `Open(guid)` и `LastViewedGuid`, но что именно они значат — вне пакета.
- Загрузка тяжёлых ассетов — на consumer'ах (карточки-спрайты через `IAssetHandle<Sprite>`, стори через `Naninovel.Script`).

---

## Зависимости

| Зависимость | Часть | Назначение |
|-------------|---|-----------|
| `Vortex.Core.LocalizationSystem` (`StringExt.TryTranslate`) | SDK | Компаратор `GalleryEntrySortByLocalizedName` |
| `Vortex.Core.DatabaseSystem` (`Database.GetRecords`) | View | Источник моделей |
| `Vortex.Sdk.RecordMarksSystem` (`RecordMarksBus`, `RecordMarksSettings`) | View | Чтение статуса меток + список доступных меток для инспектора |
| `Vortex.Core.Extensions.ReactiveValues` (`StringData`, `BoolData`) | View | Реактивы для highlight и lock-состояния |
| `Vortex.Unity.DatabaseSystem.Attributes` (`DbRecord`) | View | Выпадашка для `deniedGuids` |
| `Vortex.Unity.EditorTools.Attributes` (`ValueSelector`, `AutoLink`) | View | Инспектор-выпадашки и авто-биндинги |
| `Vortex.Unity.UI.PoolSystem` (`Pool`, `PoolItem`) | View | Механика пула |
| `Vortex.Unity.UI.TweenerSystem` (`TweenerHub`) | View | Заглушка пустого пула |
| Unity Engine | обе | `Sprite`, `MonoBehaviour` |

---

## Архитектура

```
[SDK-часть]
    IGalleryEntry            — контракт единицы контента
    IGalleryEntryComparer    — маркёр сортировщика
    Comparers/               — коробочные компараторы

[View-часть]
    GalleryView (MonoBehaviour)
        │
        ├── OnEnable → RefreshPool
        │       │
        │       ├─→ Database.GetRecords(typeof(IGalleryEntry))
        │       │
        │       ├─→ filter (allowedTypes ∩ ¬deniedTypes ∩ ¬deniedGuids ∩ marks ∩ lockedMode)
        │       │
        │       ├─→ sort (IGalleryEntryComparer или без)
        │       │
        │       ├─→ если пусто: stubTweener.Forward()
        │       │
        │       └─→ pool.AddItem(preview, isLocked, selectedGuid, entry, callbacks) × N
        │
        ├── HandleFocus(guid) — обновление selectedGuid + LastViewedGuid
        │
        ├── HandleShow(guid)  — точка расширения для наследников (базовая — no-op)
        │
        └── Open(guid)        — внешний позиционер
```

---

## Контракт (SDK)

### `IGalleryEntry`

```csharp
public interface IGalleryEntry
{
    string GuidPreset { get; }   // из RecordPreset<T>.GuidPreset
    Sprite Icon { get; }         // из RecordPreset<T>.Icon
    string Name { get; }         // из RecordPreset<T>.Name (raw, до локализации)
    void Show();
    void Hide();
}
```

- Реализуется **на модели-Record**, не на пресете-SO: `Database.GetRecords(typeof(IGalleryEntry))` возвращает модели.
- Модель должна иметь `RecordType == Singleton` — `Database.GetRecords` работает только по singleton'ам.
- `Show`/`Hide` синхронные. Если реализация запускает асинхронку, она сама владеет её жизненным циклом.
- Реализация **не должна** трогать `RecordMarksBus` — статус меток управляется извне.

### `IGalleryEntryComparer`

```csharp
public interface IGalleryEntryComparer : IComparer<IGalleryEntry> { }
```

Пустой маркёр над `IComparer<IGalleryEntry>`. Любой класс, реализующий его, автоматически попадает в выпадашку `sorter` виджета `GalleryView`. Без маркёра reflection-скан пришлось бы натаскивать на условия «`IComparer<>` c параметром `IGalleryEntry`» и рисковать притянуть системные типы.

### Коробочные компараторы

- **`GalleryEntrySortByRawName`** — `string.Compare(x.Name, y.Name, InvariantCultureIgnoreCase)`. Порядок стабилен на любой пользовательской локали.
- **`GalleryEntrySortByLocalizedName`** — `x.Name.TryTranslate()` vs `y.Name.TryTranslate()`, `CurrentCultureIgnoreCase`. При отсутствии перевода деградирует к raw-порядку.

Consumer-пакеты могут добавлять свои компараторы — попадут в выпадашку автоматически.

---

## API виджета

### Публичный

```csharp
public void Open(string guid);
public static string LastViewedGuid { get; }
```

- `Open(guid)` — устанавливает фокус на карточке в текущем пуле. Если guid не найден — warning без изменений.
- `LastViewedGuid` — сессионный статик, общий на все инстансы. Пишется при Focus и Open, живёт до перезапуска приложения (не сериализуется).

### Наследование

`HandleShow(string guid)` — `protected virtual`. Переопределите для нотификации обрамляющего UI после того, как карточка вызвала свой `entry.Show()` и `callbacks.OnShow()`.

---

## Контракт префаба карточки

Префаб-карточка ставится в поле `pool.itemPrefab`. Внутри префаба виджеты читают данные через `PoolItem.GetData<T>()`:

| Тип | Что делает |
|---|---|
| `Sprite` | превью-иконка |
| `BoolData` | isLocked → визуальное состояние карточки (силуэт/blur/grayscale — на дизайне) |
| `StringData` | selectedGuid — общий реактив на все PoolItem, сравнение с `GetData<IGalleryEntry>().GuidPreset` даёт highlight |
| `IGalleryEntry` | entry — доступ к `GuidPreset` для highlight, `Show()` для кнопки «Смотреть» |
| `GalleryPoolCallbacks` | `OnFocus` / `OnShow` — карточка вызывает на своих жестах |

Data-массив: `{ Sprite, BoolData, StringData, IGalleryEntry, GalleryPoolCallbacks }`.

### Что делает префаб

- На ховер (или Focus-жест) — вызывает `callbacks.OnFocus()`.
- На клик «Смотреть» — достаёт `entry`, вызывает `entry.Show()` и `callbacks.OnShow()`.
- Визуальная трактовка `isLocked` — целиком на дизайне.

---

## Настройка

### `GalleryView` в инспекторе

| Поле | Описание |
|---|---|
| `marks` | Один или несколько mark-имён из `RecordMarksSettings`. Карточка попадает в пул, если хотя бы одной из этих меток помечен её guid. |
| `allowedTypes` | Белый список FullName реализаций `IGalleryEntry`. Пустой — все допустимы. |
| `deniedTypes` | Чёрный список FullName. |
| `deniedGuids` | Точечные исключения guid'ов. |
| `lockedMode` | `Hide` — закрытые не в пуле; `ShowAsLocked` — в пуле, `isLocked=true`. |
| `sorter` | FullName реализации `IGalleryEntryComparer`. Пусто — без сортировки (порядок как из Database — не гарантирован). |
| `iconOverrides` | Список `(guid, Sprite)` — заменить иконку модели на «эту» для конкретной сцены. |
| `pool` | `Pool` из `Vortex.Unity.UI.PoolSystem`. |
| `stubTweener` | Опциональный `TweenerHub`. Forward при пустом пуле, Back — при непустом. |

Перед использованием — добавить нужные mark-имена в `RecordMarksSettings` (`Tools → Vortex → Record Marks → Settings`) в `slotMarks[]` (per-slot) или `globalMarks[]` (per-account).

### Consumer-модель (пример)

```csharp
public class MyCardModel : Record, IGalleryEntry
{
    // GuidPreset / Name / Icon — уже приходят из RecordPreset<MyCardModel>.CopyFrom.
    public string GuidPreset => guid;
    public Sprite Icon => icon;
    public string Name => name;

    public void Show() { /* ваш viewer */ }
    public void Hide() { /* ваш closer */ }
}

public class MyCardPreset : RecordPreset<MyCardModel> { /* поля контента */ }
```

Дальше `GalleryView` в сцене подхватит модель через Database — регистрировать пакет вручную не нужно.

---

## Edge cases

| Ситуация | Поведение |
|---|---|
| Модель без `RecordType == Singleton` | `Database.GetRecords` не вернёт её — карточка не появится в галлерее. |
| `Icon == null` | Если override не задан, PoolItem получит `null`-Sprite — визуальная реакция на дизайне префаба. |
| `Name` — пустая строка | Raw-компаратор поставит запись в начало; локализованный компаратор — как повезёт с `TryTranslate("")` (обычно пустая строка). |
| `Show` бросает исключение | `GalleryView` не оборачивает вызов — исключение уйдёт наверх; реализация обязана быть безопасной. |
| Две модели с одинаковым `GuidPreset` | Database не даст такому случиться (guid уникален). |
| `RecordMarksBus.IsReady == false` при OnEnable | Подписка на `OnReady`, отложенное `RefreshPool`. При закрытии виджета подписка снимается. |
| `Database.GetRecords` пуст, либо всё отфильтровано | Пустой пул → `stubTweener.Forward()`. |
| `sorter` не найден в домене / не реализует `IGalleryEntryComparer` | Warning + порядок как из Database. |
| `LastViewedGuid` отсутствует в текущем пуле | Стартовая позиция не устанавливается — `selectedGuid = null`. |
| `Open(guid)` — guid не в пуле | Warning, состояние не меняется. |
| `pool == null` | LogError при OnEnable, дальнейшая работа отменена. |
| Fast Enter Play | Статик `_lastViewedGuid` сохраняется (по конвенции проекта); подписки очищаются в OnDisable. |

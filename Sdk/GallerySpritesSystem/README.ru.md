# GallerySpritesSystem

**Namespace:** `Vortex.Sdk.GallerySpritesSystem.*`
**Assembly:** `ru.vortex.sdk.gallery.sprites`
**Слой:** SDK
**Тип пакета:** consumer для [GallerySystem](../GallerySystem/README.ru.md) — художественные карточки-спрайты галлереи

---

## Назначение

Один из трёх типов галлерейного контента — **художественные карточки-спрайты**: полноэкранная иллюстрация, которую игрок открывает из галлереи и разглядывает. Классический CG-контент jRPG/визуальных новелл: превью-плитка в списке, при клике — fullscreen с большой картинкой.

Возможности:

- **Модель `GallerySpriteModel`** реализует `IGalleryEntry` из GallerySystem — попадает в галлерейный пул автоматически, `GalleryView` подхватывает через `Database.GetRecords(typeof(IGalleryEntry))`.
- **Пресет `GallerySpritePreset`** держит стандартные поля `RecordPreset<T>` (Guid, Name, Description, Icon-превью) + полиморфный `AssetHandle<Sprite>` для fullscreen-ассета.
- **Дизайнер выбирает per-preset**: `DirectAssetHandle<Sprite>` (fullscreen всегда в памяти) или `AddressableAssetHandle<Sprite>` (lazy-load через Addressables) через Odin `[SerializeReference]`-picker.
- **Показ через static bus** — модель ничего не знает про viewer, только фаерит команды `RequestShow`/`RequestHide`. Один зарегистрированный viewer на проект.
- **Разблокировка ортогональна** — статус «разблокирована» живёт в `RecordMarksSystem`, пакет туда не пишет.

Вне ответственности:

- **UI-компонент fullscreen-viewer'а** — не в пакете, живёт в UI-слое приложения (арт-направление и анимация — специфика игры).
- **Разблокировка карточек** — забота сценарных механик (Nani-команда, drop-триггер), пишут напрямую в `RecordMarksBus`.
- **Загрузка ассета** — делегируется в `AssetHandle<T>`, тот в свою очередь — в `AssetCache` (для Addressable-реализации).

---

## Зависимости

| Зависимость | Назначение |
|---|---|
| `Vortex.Sdk.GallerySystem` | Контракт `IGalleryEntry` |
| `Vortex.Core.AssetHandleSystem` | Абстракция fullscreen-ассета |
| `Vortex.Core.DatabaseSystem` | `Record`, попадание в галлерейный пул |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>` |
| `Vortex.Core.Extensions.LogicExtensions` | `ObjectExtCopy.CopyFrom`, `ObjectExtDeepClone.DeepCopy` |
| `Vortex.Core.LoggerSystem` | Warning при отсутствии viewer'а, LogError при повторной регистрации |
| UniTask | Async-контракт `LoadFullscreenAsync` |

---

## Архитектура

```
GallerySpritePreset (SO)
    ├── [SerializeField] Icon (превью)          — из базы RecordPreset<T>
    └── [SerializeReference] fullscreen         — DirectAssetHandle<Sprite> ИЛИ AddressableAssetHandle<Sprite>
                    │
                    │   Database.GetRecords → RecordPreset<T>.GetData()
                    │       ↓
                    ▼
GallerySpriteModel (Record, IGalleryEntry)
    ├── GuidPreset, Icon, Name, Description     — публичные (из Record + CopyFrom)
    ├── internal Fullscreen                     — deep-clone от FullscreenTemplate пресета
    ├── Show()  → GallerySpritesBus.RequestShow(this, ct)
    └── Hide()  → GallerySpritesBus.RequestHide(this)
                    │
                    ▼
GallerySpritesBus (static, registry)
    ├── Register(showHandler, hideHandler)      — один viewer на проект
    ├── Unregister()
    ├── RequestShow(model, ct)  → showHandler   — Warning если viewer не зарегистрирован
    └── RequestHide(model)      → hideHandler
                    │
                    ▼
Viewer (MonoBehaviour в UI-слое приложения — не в пакете)
    ├── Awake:    GallerySpritesBus.Register(HandleShow, HandleHide)
    ├── OnDestroy: GallerySpritesBus.Unregister()
    ├── HandleShow(model, ct): await model.LoadFullscreenAsync(ct); show UI
    ├── HandleHide(model): if (_current == model) InternalClose()
    ├── OnDisable: InternalClose()
    └── InternalClose(): _current?.ReleaseFullscreen(); _current = null; hide UI
```

---

## Контракт

### `GallerySpriteModel : Record, IGalleryEntry`

```csharp
public class GallerySpriteModel : Record, IGalleryEntry
{
    public Sprite Icon { get; protected set; }
    internal AssetHandle<Sprite> Fullscreen { get; set; }

    public bool CopyFrom(SoData source);       // override, deep-clone для Fullscreen
    public void Show();
    public void Show(CancellationToken ct);
    public void Hide();
}
```

### `GallerySpritePreset : RecordPreset<GallerySpriteModel>`

```csharp
public class GallerySpritePreset : RecordPreset<GallerySpriteModel>
{
    [SerializeReference] private AssetHandle<Sprite> fullscreen;
    public AssetHandle<Sprite> FullscreenTemplate => fullscreen;
}
```

### `GallerySpritesBus` (static)

```csharp
public static class GallerySpritesBus
{
    public static bool HasViewer { get; }

    public static void Register(Action<GallerySpriteModel, CancellationToken> showHandler,
                                Action<GallerySpriteModel> hideHandler);
    public static void Unregister();

    public static void RequestShow(GallerySpriteModel model, CancellationToken ct);
    public static void RequestHide(GallerySpriteModel model);
}
```

### `GallerySpriteController` (extension)

```csharp
public static class GallerySpriteController
{
    public static UniTask<Sprite> LoadFullscreenAsync(this GallerySpriteModel model, CancellationToken ct);
    public static UniTask<Sprite> LoadFullscreenAsync(this GallerySpriteModel model);
    public static void            ReleaseFullscreen(this GallerySpriteModel model);
}
```

### Инварианты

- **I1.** `Fullscreen` — internal; извне сборки недоступен ни для чтения, ни для мутации.
- **I2.** Handle модели — независимый deep-clone handle пресета: состояние загрузки (`_cached`) не разделяется.
- **I3.** Только один viewer активен в момент времени (registry).
- **I4.** `Show`/`Hide` — синхронные (требование `IGalleryEntry`), внутри — только зов bus.

---

## Создание карточки (для дизайнера)

1. **Создать пресет:** Project → Create → Vortex → Presets → Gallery → Sprite. Получится `GallerySpritePreset`.
2. **Заполнить базовые поля:** Name, Description, Icon (превью для галлерейной плитки).
3. **Выбрать реализацию `AssetHandle<Sprite>`:** в инспекторе кликнуть по полю `fullscreen`, Odin picker предложит:
   - **`DirectAssetHandle<Sprite>`** — прямая ссылка. Fullscreen всегда в памяти. Подходит для лёгких/всегда-нужных.
   - **`AddressableAssetHandle<Sprite>`** — Addressables lazy-load. Ассет грузится по требованию, выгружается по `Release`.
4. **Указать ассет** в выбранной реализации.
5. **Метка разблокировки:** в `RecordMarksSettings` должна быть метка, которой сюжет пометит `GuidPreset` карточки для «разблокирована». Дизайнер галлерейной сцены указывает эту метку в `GalleryView.marks`.

Карточка автоматически попадает в `GalleryView`, если её тип разрешён `allowedTypes` (или не запрещён `deniedTypes`), а `GuidPreset` помечен нужной меткой.

---

## Viewer implementation guide

Пакет поставляет **готовый** `GallerySpriteViewer` (`View/`): регистрируется в шине на `Awake`, грузит fullscreen через `GallerySpriteController`, кладёт спрайт в поле `Image`, запускает показ `TweenerHub`'ом (Forward — открыть, Back — закрыть) и реализует `IDataStorage` — отдаёт текущую модель дочерним виджетам панели через `GetData<T>()` / `OnUpdateLink`. Для типовой галереи достаточно повесить его на панель fullscreen'а и назначить `Image` + твинер.

Если нужно кастомное поведение (свои переходы, аналитика, несколько состояний) — пишется свой viewer по тому же контракту. Шаблон:

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Sdk.GallerySpritesSystem.Bus;
using Vortex.Sdk.GallerySpritesSystem.Controllers;
using Vortex.Sdk.GallerySpritesSystem.Models;

public class FullscreenSpriteViewer : MonoBehaviour
{
    [SerializeField] private GameObject panel;   // root UI-панели
    [SerializeField] private Image image;        // компонент для показа спрайта
    [SerializeField] private Button closeButton;

    private GallerySpriteModel _current;

    private void Awake()
    {
        GallerySpritesBus.Register(HandleShow, HandleHide);
        closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnDestroy()
    {
        GallerySpritesBus.Unregister();
        closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnDisable()
    {
        InternalClose();
    }

    private async void HandleShow(GallerySpriteModel model, CancellationToken ct)
    {
        // Переход на новый entry: старый закрывается без вызова его Hide (чтобы не породить петлю).
        if (_current != null && _current != model)
            InternalClose();

        _current = model;
        var sprite = await model.LoadFullscreenAsync(ct);
        if (_current != model) return;   // за время await пришёл ещё Show — этот отменён

        image.sprite = sprite;
        panel.SetActive(true);
    }

    private void HandleHide(GallerySpriteModel model)
    {
        if (_current != model) return;
        InternalClose();
    }

    private void OnCloseClicked()
    {
        // Через модель — единая публичная точка «закрыть», приходит обратно в HandleHide.
        // Это даёт логгерам/аналитике узнать о закрытии.
        _current?.Hide();
    }

    private void InternalClose()
    {
        if (_current == null) return;
        _current.ReleaseFullscreen();
        _current = null;
        panel.SetActive(false);
    }
}
```

**Дисциплина (важно):**

- **Один viewer на весь проект и на всё время сессии.** `Register` — на `Awake`, `Unregister` — на `OnDestroy`. Второй одновременно живой viewer bus отклонит (LogError, первый остаётся активным). Это дизайн-инвариант: два viewer'а спрайтовой галереи не сосуществуют. Смена сцены галереи предполагает, что старый viewer уничтожается (Unregister) до появления нового — держать двух viewer'ов одновременно (аддитивная загрузка с перекрытием) нельзя.
- **`InternalClose` не зовёт `model.Hide()`** — иначе петля через bus. При закрытии по инициативе viewer'а (OnDisable, переход на новый entry) — только напрямую `ReleaseFullscreen` + hide UI.
- **`OnCloseClicked` зовёт `_current.Hide()`**, не `InternalClose()` напрямую. Так закрытие проходит через модель → bus → HandleHide → InternalClose, и любой сторонний слушатель (аналитика) получит событие.
- **Race при быстром переключении**: за время `await LoadFullscreenAsync` может прийти новый `Show`. Проверять `if (_current != model) return` после await.

---

## Edge cases

| Ситуация | Поведение |
|---|---|
| `Show()` при отсутствии зарегистрированного viewer'а | Warning в лог, команда потеряна |
| `Hide()` при отсутствии viewer'а | Warning в лог |
| Повторный `Register` при активном viewer'e | LogError, второй отклонён; первый продолжает работать |
| `Unregister` без предварительного `Register` | Идемпотентно, no-op |
| Viewer уничтожен без `Unregister` | Handler-делегаты держат ссылку на destroyed MonoBehaviour → NRE при следующем `RequestShow`. **Обязательно `Unregister` в OnDestroy** |
| Переход на новый entry во время загрузки предыдущего | Viewer проверяет `_current` после await; если поменялся — предыдущий Show отменяется |
| `AddressableAssetHandle` в проекте без Addressables | При десериализации пресета поле = null; `LoadFullscreenAsync` бросит NRE. Заранее решать в проекте (см. AssetHandle README) |
| `Fullscreen` handle-инстанс в модели vs пресете | Разные объекты (deep-clone в CopyFrom). Release в модели не влияет на пресетный handle |
| `LoadFullscreenAsync` вызван вне сборки пакета | Compile error — extension метод виден, но требует internal `Fullscreen`, к которому доступа нет извне. Всегда через модель как handle-holder |

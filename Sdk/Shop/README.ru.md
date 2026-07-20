# Shop

**Namespace:** `Vortex.Sdk.Shop.*`
**Сборка:** `ru.vortex.sdk.shop`
**Слой:** 3 (SDK)

---

## Назначение

Транзакционный движок покупок, не знающий ни о валюте, ни о товаре. Обе стороны сделки вынесены в подключаемые логики: **`PaymentLogic`** (что и откуда списать) и **`BuyCaseLogic`** (что выдать). Движок оркеструет сделку, ведёт неизменяемый журнал и гарантирует, что сбой на середине не съедает оплату.

Одна система на все виды магазинов: покупка за внутреннюю валюту, обмен ресурсов, выдача награды по условию, дейли-товар с автосписанием — различие только в реализации двух логик на уровне проекта (L4).

Возможности:
- Товар — запись в Database, настраиваемая дизайнером; движок в ценообразование не вмешивается.
- Каждая покупка — цепочка неизменяемых событий в append-only журнале (заказ, оплата, выдача, возврат, отмена). Записи не редактируются задним числом.
- Цена фиксируется в момент сделки (поглощение), поэтому запись остаётся честной после патча коэффициентов.
- Сбой выдачи разрешается по политике: откат оплаты, удержание для повтора, либо ожидание подтверждения игроком.
- Всё переживает перезапуск — в пределах последней точки сохранения (журнал слот-локален).
- Оси времени в каждом событии: sequence (порядок), playSeconds/appSeconds (этап прохождения / вовлечённость), timestamp (недоверенная календарная привязка).

Вне ответственности:
- Конкретные `PaymentLogic`/`BuyCaseLogic` под валюту проекта — живут на L4.
- UI витрины и «моих покупок» — производная от движка, строится потребителем.
- Покупки за реальную валюту вне игровой сессии (DLC) — отдельный контур, в журнал не входят.

---

## Зависимости

| Зависимость | Назначение |
|---|---|
| `Vortex.Sdk.Core.GameCore` | `GameController.PlayTime`/`AppTime` для осей времени события; `GameModel.IGameData` для журнала |
| `Vortex.Core.DatabaseSystem` | `Record`, `Database.GetRecords` — каталог товаров |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>` — пресет товара |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid`, `[POCO]` для сериализации журнала |
| `Vortex.Core.ComplexModelSystem` | база `GameModel` |
| UniTask (Cysharp) | асинхронный контракт логик оплаты/выдачи |

---

## Архитектура

```
[ShopItemPreset] (RecordPreset, SO, Singleton)          [ShopSettings] (SO, вне Database)
   ├── PaymentLogic  [SerializeReference]                   └── FallbackMode: Rollback / Pending / Ready
   ├── BuyCaseLogic  [SerializeReference]
   └── HiddenInShowcase
          │  CopyFrom (DeepCopy-клон логик)
          ▼
[ShopItemRecord : Record]  (Database, GetDataForSave()=>null)

[ShopController] (Singleton, реализует IShopController + IShopJournalReader)
   ├── проводка: Buy → Ordered → Pay → Paid → выдача → терминал
   ├── единственная точка эмиссии событий (Emit)
   ├── рантайм-индекс над журналом (O(1) свёртки)
   └── резолв каталога (пустой товар на неизвестный guid)
          │  события журнала
          ▼
[ShopStatisticsData : GameModel.IGameData]  (тело сейва, append-only)
   ├── List<ShopTransactionEvent>
   └── long Sequence

Две шины (два домена):
[ShopBus]      — проводка: Buy/BuyForget, ConfirmDelivery, RetryDelivery, CancelWithRefund, GetItem, события
[ShopJournal]  — учёт (read-only): GetOpen/GetReady/GetStuck, GetPurchase, GetPurchaseHistory, GetHistory
```

Проводка и учёт — разные домены с разными потребителями, поэтому две точки входа.

### Состояния покупки

Состояние — тип операции, зафиксированный в журнале (не рантайм-флаг), поэтому переживает перезапуск. Свёртка по `purchaseGuid` даёт текущее состояние (по максимальному `Sequence`).

| Состояние | Терминальное | Смысл |
|---|---|---|
| `Ordered` | нет | Заказ заведён, предпроверки пройдены, оплата не проводилась |
| `Paid` | нет | Оплата подтверждена |
| `Ready` | нет | Оплачено, выдача ждёт подтверждения игроком (автовыдачи нет) |
| `Pending` | нет | Выдача сорвалась, покупка удерживается для повтора |
| `Delivered` | да | Товар выдан |
| `Refunded` | да | Оплата возвращена |
| `Cancelled` | да | Оплата не прошла, ничего не списано |
| `Failed` | да | Ни выдать, ни вернуть — у товара нет логик |

### Политики фолбэка (`ShopSettings.FallbackMode`, глобально)

| Политика | Поведение после `Paid` |
|---|---|
| `Rollback` | Автовыдача; при отказе — возврат оплаты, закрытие |
| `Pending` | Автовыдача; при отказе — удержание в `Pending` для внешнего повтора |
| `Ready` | Автовыдачи нет; перевод в `Ready`, ждёт подтверждения игроком |

`Ready` удерживается при неудачном подтверждении независимо от политики; выход — успешная выдача либо ручная отмена с возвратом.

---

## Контракт

### Кто меняет состояние

Эмиссию события выполняет **только контроллер**. Логики не пишут состояние — они докладывают исход вызовом методов через scoped-контекст, привязанный к одной покупке. Статический контроллер не подписывается на события экземпляров логик.

Поток: контроллер заводит покупку (`Ordered`) → передаёт данные в `PaymentLogic` → та по ответу вызывает контекст (`Paid`/`Cancelled`) → контроллер передаёт управление в `BuyCaseLogic` → та вызывает контекст (`Delivered`/`Ready`/`Pending`/`Failed`).

### PaymentLogic

```csharp
public abstract class PaymentLogic
{
    ShopRefusal? CanPay(int requestedCount);        // синхронная предпроверка, null = можно
    UniTask Pay(IPayContext context);               // Paid(payValue) либо Cancelled(reason)
    UniTask Refund(IRefundContext context);         // Refunded() либо Failed(reason)
}
```

### BuyCaseLogic

```csharp
public abstract class BuyCaseLogic
{
    ShopRefusal? CanDeliver(int requestedCount);    // синхронная предпроверка
    UniTask Deliver(IBuyContext context);           // Delivered/Ready/Pending/Failed
}
```

### Требования к логикам

1. **Stateless.** `CopyFrom` даёт каждой записи собственный клон логики (DeepCopy), `GetDataForSave()=>null` — состояние логики не сохраняется. Всё для повтора выдачи после перезапуска — в событии журнала (`RequestedCount`, `PayValue`, `BuyValue` в контексте).
2. **Предпроверка отсекающая, не гарантирующая** — авторитетен результат выполнения.
3. **Единый сохраняемый контур (Inv-9).** Данные, которые логика изменяет, обязаны сохраняться вместе с журналом. Для логики с эффектом в контуре сейва путь `Ordered → Paid/Cancelled` обязан быть **синхронным**. Асинхронный путь — только для эффекта вне контура сейва (серверно-авторитетная оплата); её восстановление после обрыва — ответственность реализации, движок гарантирует стабильный `purchaseGuid` как ключ идемпотентности.
4. **Исключение не оставляет висяк.** Движок оборачивает каждый await логики в `try/catch`: исключение оплаты → покупка закрывается `Cancelled`, исключение выдачи → трактуется как провал (политика фолбэка), исключение возврата → покупка остаётся открытой (Inv-6).

---

## API

### ShopBus (проводка)

| Член | Назначение |
|---|---|
| `Init(ShopSettings)` | Инициализация движка. Вызывается bootstrap-кодом проекта |
| `GetItem(itemGuid)` | Товар. Неизвестный guid → пустой товар (заглушка без логик), не null |
| `UniTask<ShopResult> Buy(itemGuid, count)` | Покупка. Возвращает финальный исход; для UI-await |
| `BuyForget(itemGuid, count)` | Синхронный fire-and-forget враппер |
| `ShopRefusal? ConfirmDelivery(purchaseGuid)` | Подтверждение из `Ready`. `PurchaseBusy` при занятости |
| `ShopRefusal? RetryDelivery(purchaseGuid)` | Повтор из `Pending` |
| `ShopRefusal? CancelWithRefund(purchaseGuid)` | Ручная отмена открытой покупки с возвратом |
| `event OnPurchaseStateChanged(guid, state)` | Состояние покупки изменилось |
| `event OnPurchaseClosed(ShopPurchase)` | Покупка закрыта терминальным событием |

### ShopJournal (учёт, read-only)

| Член | Назначение |
|---|---|
| `GetOpen()` | Открытые (не-терминальные) покупки |
| `GetReady()` | Готовые к получению (`Ready`) |
| `GetStuck()` | Зависшие (`Failed`) — для техподдержки |
| `GetPurchase(guid)` | Свёртка конкретной покупки |
| `GetPurchaseHistory(guid)` | Все события конкретной покупки |
| `GetHistory()` | Полная история журнала |
| `event OnJournalUpdated` | Журнал пополнился |

Перечень `Ready` разведён с `Failed` сознательно: штатные неполученные награды не засоряют разбор сбоев.

---

## Использование

### Товар и настройки

`Assets → Create → Vortex → Shop → Shop Item` — пресет товара: назначить `PaymentLogic`/`BuyCaseLogic` (реализации проекта) и флаг видимости.
`Assets → Create → Vortex → Shop → Shop Settings` — политика фолбэка.

### Инициализация и покупка

```csharp
ShopBus.Init(shopSettings);   // из bootstrap проекта

// покупка с ожиданием исхода (UI)
var result = await ShopBus.Buy(itemGuid, count: 3);
if (!result.Started)
    ShowRefusal(result.Refusal.Value);        // отказ предпроверки
else if (result.Purchase.State == PurchaseState.Delivered)
    ShowSuccess();
else if (result.Refusal.HasValue)
    ShowRefusal(result.Refusal.Value);        // рантайм-отмена (LogicRejected и т.п.)

// автоматика / не нужен await
ShopBus.BuyForget(itemGuid, 1);
```

### Дейли-товар (сценарий Ready)

Товар с `PaymentLogic`, автосписывающей по таймеру, под глобальной политикой `Ready`: `Buy` доходит до `Paid` и переводит покупку в `Ready`. Игрок кликает получение:

```csharp
foreach (var p in ShopJournal.GetReady())
    ShowClaimable(p);

// по клику
var refusal = ShopBus.ConfirmDelivery(purchaseGuid);
if (refusal == ShopRefusal.PurchaseBusy)
    ShowBusy();
```

### Подписка на журнал

```csharp
private void OnEnable()  => ShopJournal.OnJournalUpdated += RefreshShowcase;
private void OnDisable() => ShopJournal.OnJournalUpdated -= RefreshShowcase;
```

---

## Граничные случаи

| Ситуация | Поведение |
|---|---|
| Неизвестный `itemGuid` | Пустой товар (без логик) → `LogicUnavailable` |
| `count <= 0` | `InvalidCount`, журнал не пишется |
| Существует заказ между `Ordered` и `Paid` | `OrderInProgress` (Inv-7) — новый заказ не начинается |
| По покупке уже идёт операция | `PurchaseBusy` (Inv-8) |
| Логика оплаты бросила исключение / промолчала | Покупка закрывается `Cancelled` (причина `LogicRejected`) — не виснет в `Ordered` |
| Логика выдачи бросила исключение | Трактуется как провал → политика фолбэка |
| Упавший возврат | Покупка остаётся открытой, терминал не пишется (Inv-6) |
| Товар «удалён» (флаг + подмена `BuyCaseLogic` на компенсацию, `PaymentLogic` убрана) | Скрыт в витрине; открытая покупка резолвится в компенсацию и доигрывается |
| Товар исчез по-настоящему (нет логик) | Открытая покупка закрывается `Failed`, попадает в `GetStuck()` |
| Загрузка более раннего слота | Журнал откатывается вместе со слотом — принятое поведение (покупки слот-локальны) |
| Сохранение из активной покупки | Незакрытая покупка в журнале; после загрузки доступна для завершения через `GetOpen()`/`RetryDelivery` |
| Неполученные `Ready` копятся | Штатно: ничего не сгорает; отдаются отдельным перечнем `GetReady()` |

# ShopSystem

Sdk-пакет фреймворка Vortex: транзакционный движок покупок. Заказ → оплата → выдача, каждый переход фиксируется событием в append-only журнал внутри сейва. Незавершённые покупки восстанавливаются после перезагрузки, идущая покупка прерывается с возвратом оплаты.

## Назначение

- Проведение покупки как стейт-машины с фиксацией каждого перехода в журнал
- Полиморфные логики оплаты и выдачи (`[SerializeReference]`) — обе стороны сделки определяет проект
- Политика поведения после оплаты (`AfterpayMode`), общая на игру: автовыдача с откатом, удержание для повтора, ожидание подтверждения игроком
- Восстановление незавершённых покупок при загрузке сейва — свёрткой журнала
- Отмена идущей покупки: прерывание процесса по токену и возврат оплаты
- Рантайм-индексы для запросов: открытые / готовые к получению / зависшие покупки, история по покупке и по товару
- Готовые логики: оплата — `FreeLogic` (нулевая цена); выдача — `LimitedLogic` (гейт-ограничитель: лимит количества полученного за период)

Вне ответственности:

- Конкретные правила списания и выдачи (кошелёк, инвентарь, энтайтлменты) — наследники `PaymentLogic` / `DeliveryLogic` в проекте
- Витрина и UI покупки — потребитель подписывается на реактивное состояние операции
- Серверная верификация платежей: пакет не знает о протоколах, сетевая логика сама сверяет факт списания по `PurchaseGuid`
- Композиция нескольких правил на одной стороне сделки — у товара одна логика оплаты и одна логика выдачи; несколько правил одной стороны (напр. «выдача предмета + лимит») собираются одной комбинированной логикой

## Зависимости

| Зависимость | Назначение |
|-------------|-----------|
| `ru.vortex.system` | `SystemController`, `Singleton`, `ISystemDriver` |
| `ru.vortex.database` | `Database`, `Record` — каталог товаров |
| `ru.vortex.extensions` | `ListData` / `EnumData`, `InitValve`, `Crypto`, `[POCO]` |
| `ru.vortex.unity.database` | `RecordPreset<T>` — пресет товара |
| `ru.vortex.unity.extensions` | `SoData` — база `RecordPreset` |
| `ru.vortex.unity.editortools` | `[TimeDraw]` в `LimitedLogic` |
| `ru.vortex.unity.CoreAssetsSystem` | `ICoreAsset` — автосоздание ассета настроек |
| `ru.vortex.sdk.game.core` | `GameController`, `GameModel.IGameData`, `PlayTime` / `AppTime` |
| UniTask | Асинхронные логики оплаты и выдачи |
| Sirenix Odin Inspector | `[SerializeReference]`-пикер, `[InfoBox]`, `[HideReferenceObjectPicker]` |

Assembly: `ru.vortex.sdk.shopsystem`. Весь код пакета закрыт `#if USING_VORTEX_SHOP`; символ включается тумблером `shopSdk` в ассете `SdkSettings`.

## Архитектура

### Конфигурация и каталог

```
ShopItemPreset (ScriptableObject)             ← конфиг дизайнера
   ├─ [SerializeReference] PaymentLogic       ← списание / возврат
   ├─ [SerializeReference] DeliveryLogic      ← выдача
   └─ hiddenInShowcase
            │  CopyFrom при построении записи (RecordTypes.Singleton)
            ▼
      ShopItemModel : Record  ──────────────► каталог: ShopBus.Instance.ShopItems
```

### Две шины

Проводка и учёт разделены: одна шина меняет состояние сделки, вторая только читает данные.

| Шина | Домен | Состав |
|------|-------|--------|
| `ShopBus` | Проводка | `Buy`, `BuyForget`, `ConfirmDelivery`, `RetryDelivery`, `CancelWithRefund`, `RestoreOperation`, `IsBusy`, каталог |
| `ShopOperationsBus` | Учёт (read-only) | `GetOpen`, `GetReady`, `GetStuck`, `GetOperation`, `GetHistory`, `GetPurchaseHistory`, `GetGoodsHistory` |

### Данные и восстановление

```
ShopController (драйвер ShopBus)
       │  каждый переход состояния
       ▼
ShopTransactionEvent ──► ShopOperations.Events      ← журнал, входит в тело сейва
                                │
                                │  свёртка на NewGame / OnLoad
                                ▼
        Operations          — свёрнутая покупка по PurchaseGuid
        Transactions        — события по PurchaseGuid
        GoodsOperations     — покупки по ItemGuid
                                │
                                ▼
                    открытые покупки доигрываются автоматически
```

Источник истины — журнал `Events`. Три индекса производны от него, в сейв не входят и пересобираются при каждой загрузке. Журнал и списки индексов закрыты owner-ключом контроллера: извне пакета их не изменить.

### Стейт-машина

```
Ordered ──оплата ok──► Paid ──┬─ AfterpayMode.Ready ────────► Ready ──ConfirmDelivery──► Delivered
   │                          │
   │ оплата отклонена         ├─ выдача успешна ────────────► Delivered
   ▼                          │
Cancelled                     └─ выдача недоступна/сорвалась:
                                    Pending  → Pending ──RetryDelivery──► Delivered
                                    Rollback → возврат ──► Refunded (успех) / Failed (отказ)
                                    Ready    → состояние не меняется
```

`Ordered` и `Paid` прогоняются автоматически. `Ready` и `Pending` — равновесные: автопрогон на них останавливается, дальше двигает только явный вызов.

## Ключевые концепции

| Концепция | Описание |
|-----------|----------|
| Товар (`ShopItemModel`) | Неизменяемая запись каталога с двумя логиками. Строится из `ShopItemPreset`, тип записи — `Singleton` |
| Логика оплаты (`PaymentLogic`) | Списание и возврат. Обязательна; бесплатный товар — `FreeLogic`, не `null` |
| Логика выдачи (`DeliveryLogic`) | Передача товара игроку. Обязательна |
| Операция (`ShopOperation`) | Рантайм-модель покупки с реактивным `State` (`EnumData<PurchaseState>`) |
| Событие (`ShopTransactionEvent`) | Неизменяемая запись журнала: состояние, значения сделки, три оси времени |
| Журнал (`Events`) | Append-only список событий со сквозным `Sequence`. Источник истины, входит в сейв |
| Политика (`AfterpayMode`) | Что делать после оплаты, если выдача не состоялась. Глобальна на игру |

## Состояния покупки

| Состояние | Смысл | Терминальное |
|-----------|-------|--------------|
| `Ordered` | Заказ заведён, предпроверки пройдены, оплата не проводилась | — |
| `Paid` | Оплата подтверждена | — |
| `Ready` | Оплачено, выдача ждёт подтверждения игроком | — (равновесное) |
| `Pending` | Выдача сорвалась, покупка удерживается для повтора | — (равновесное) |
| `Delivered` | Товар выдан | да |
| `Refunded` | Оплата возвращена | да |
| `Cancelled` | Оплата не прошла, ничего не списано | да |
| `Failed` | Ни выдать, ни вернуть: у товара нет логик либо возврат отклонён | да |
| `NotStarted` | Прерывание на предпроверках. **В журнал не пишется** | — |

## Политика AfterpayMode

Применяется только к автоматической выдаче из `Paid`. Явные `ConfirmDelivery` и `RetryDelivery` под политику не попадают.

| Режим | Поведение |
|-------|-----------|
| `Rollback` | Автовыдача. При отказе — возврат оплаты (`Refunded`, при отклонённом возврате — `Failed`) |
| `Pending` | Автовыдача. При отказе — удержание в `Pending` для внешнего повтора |
| `Ready` | Автовыдачи нет: `Paid` сразу переводится в `Ready`, выдачу инициирует игрок |

## Критические требования

1. **Обе логики обязательны.** Товар без `PaymentLogic` или `DeliveryLogic` нефункционален: покупка закрывается как `Failed` до списания. Бесплатный товар собирается отдельной логикой (`FreeLogic`), а не `null`-ссылкой.
2. **Из `Ready` выдачу инициирует только игрок.** Восстановление и автопрогон `Ready` не двигают — иначе подтверждение обходится при перезагрузке.
3. **Каждый шаг обязан менять состояние или заканчиваться в равновесии.** Логика, вернувшая `false` без последующего перехода, зациклила бы автопрогон. Поэтому возврат оплаты терминализует всегда: успех → `Refunded`, отказ → `Failed`.
4. **Один активный интерактивный процесс.** `Buy` / `BuyForget` / `ConfirmDelivery` / `RetryDelivery` отбиваются, пока предыдущий не завершился. Проверять заранее — `ShopBus.IsBusy`.
5. **Отмена не отменяет возврат.** Токен отмены прокидывается в `CanPay` / `MakePay` / `CanDelivery` / `MakeDelivery`, но **не** в `MakeRefund`: компенсирующее действие обязано доиграть до конца.
6. **Для несетевой оплаты переход `Ordered → Paid` держать синхронным.** Сетевая логика обязана быть готова свериться по `PurchaseGuid` после прерывания или перезапуска.

## Контракт

### Вход

- Ассет `ShopSettings` с выбранным `AfterpayMode` (создаётся автоматически, см. «Использование»)
- Ассеты `ShopItemPreset` с заполненными логиками оплаты и выдачи, зарегистрированные в `Database`
- Драйвер `ShopController`, назначенный `ShopBus` в `DriverConfig`
- Загруженная игровая сессия: журнал живёт в `GameModel`

### Выход

- `ShopBus.Buy` / `BuyForget` → `ShopOperation` с реактивным `State`, либо `null` (занято или неизвестный товар)
- Журнал `ShopOperations.Events` в теле сейва
- Запросы `ShopOperationsBus` — срезы по индексам

### Гарантии

- Каждый переход состояния фиксируется ровно одним событием со сквозным монотонным `Sequence`
- Событие самодостаточно: содержит guid покупки и товара, состояние, значения сделки и три оси времени (UTC-секунды, `PlayTime`, `AppTime`)
- Журнал закрыт owner-ключом: мутация возможна только из контроллера пакета
- Индексы полностью восстанавливаются из журнала при каждой загрузке; при нарушении сквозной нумерации в лог пишется ошибка
- Автопрогон всегда завершается: на терминальном состоянии, на равновесии (`Ready` / `Pending`) или на отмене
- Исключение из логики не рвёт процесс: логируется, покупка остаётся в текущем состоянии и попадёт в `GetOpen`
- Отмена не оставляет промежуточных состояний: процесс разматывается до границы состояния, и только потом принимается решение о возврате

### Ограничения

- **Один активный процесс.** Параллельные интерактивные покупки не поддерживаются.
- **`NotStarted` не журналируется.** Покупка, отклонённая предпроверками, не попадает ни в один индекс.
- **Политика не снапшотится.** Открытая покупка доигрывает по текущему значению `AfterpayMode`, а не по тому, что действовало при заказе.
- **Одна логика оплаты на товар.** Комбинации правил собираются внутри одной логики.
- **Статики `ShopBus` не защищены от отсутствующего драйвера** (кроме `IsBusy`): обращение до регистрации даёт `NullReferenceException`.
- **Восстановление не отменяемо.** Оно идёт на локальном токене и под `CancelWithRefund` не попадает.

## API Reference

```csharp
// ── Проводка ──────────────────────────────────────────────────────────────
// Первый параметр — guid ТОВАРА (ShopItemModel.GuidPreset), не покупки
UniTask<ShopOperation> ShopBus.Buy(string itemGuid, int count);
ShopOperation          ShopBus.BuyForget(string itemGuid, int count);

void ShopBus.ConfirmDelivery(ShopOperation operation);   // из Ready
void ShopBus.RetryDelivery(ShopOperation operation);     // из Pending
void ShopBus.CancelWithRefund(ShopOperation operation);  // прерывание + возврат
void ShopBus.RestoreOperation(ShopOperation operation);  // доигрывание открытой

bool ShopBus.IsBusy;                                     // пред-проверка занятости
IReadOnlyDictionary<string, ShopItemModel> ShopBus.Instance.ShopItems;

// Готовность: InitValve — только Subscribe/Unsubscribe, оператора += нет
ShopBus.OnReady.Subscribe(OnShopReady);

// ── Учёт (read-only) ──────────────────────────────────────────────────────
IReadOnlyList<ShopOperation> ShopOperationsBus.GetOpen();    // Ordered/Paid/Ready/Pending
IReadOnlyList<ShopOperation> ShopOperationsBus.GetReady();   // ждут подтверждения
IReadOnlyList<ShopOperation> ShopOperationsBus.GetStuck();   // Failed — на разбор

ShopOperation                       ShopOperationsBus.GetOperation(string purchaseGuid);
ListData<ShopTransactionEvent>      ShopOperationsBus.GetHistory();
ListData<ShopTransactionEvent>      ShopOperationsBus.GetPurchaseHistory(string purchaseGuid);
ListData<ShopOperation>             ShopOperationsBus.GetGoodsHistory(string itemGuid);

ShopOperationsBus.OnReady.Subscribe(OnDataReady);

// ── Логика оплаты (реализуется в проекте) ────────────────────────────────
public class GoldPayment : PaymentLogic
{
    [SerializeField] private int price = 100;

    public override int GetCount() => price;

    public override UniTask<bool> CanPay(string guid, int count, CancellationToken ct)
        => UniTask.FromResult(Wallet.Gold >= price * count);

    public override UniTask<bool> MakePay(ShopOperation operation, CancellationToken ct)
        => UniTask.FromResult(Wallet.TrySpend(price * operation.RequestedCount));

    // Токена отмены нет: возврат обязан доиграть до конца
    public override UniTask<bool> MakeRefund(ShopOperation operation)
        => UniTask.FromResult(Wallet.Refund(operation.PayValue * operation.RequestedCount));
}

// ── Логика выдачи (реализуется в проекте) ────────────────────────────────
public class ItemDelivery : DeliveryLogic
{
    [SerializeField] private string itemId;
    [SerializeField] private int amount = 1;

    public override int GetCount() => amount;

    public override UniTask<bool> CanDelivery(string guid, int count, CancellationToken ct)
        => UniTask.FromResult(Inventory.HasSlots(amount * count));

    public override UniTask<bool> MakeDelivery(ShopOperation operation, CancellationToken ct)
        => UniTask.FromResult(Inventory.Give(itemId, amount * operation.RequestedCount));
}
```

## Использование

### 1. Ассет настроек

`ShopSettings` реализует `ICoreAsset` и создаётся автоматически в `Assets/Resources/Settings/`. Если автосоздание выключено — `Tools → Vortex → Debug → Check Core Assets`. В ассете выбирается `AfterpayMode`.

### 2. Регистрация драйвера

В ассете `DriverConfig`: **Reload** → для системы `ShopBus` выбрать драйвер `ShopController` → **Save Config**. Без записи в конфиге `SetDriver` отклонит регистрацию, и обращения к шине упадут с `NullReferenceException`.

### 3. Логики и товары

Реализовать наследников `PaymentLogic` и `DeliveryLogic` (см. API Reference), пометив классы `[Serializable]` — иначе они не появятся в `[SerializeReference]`-пикере.

Товар: `Assets → Create → Database → ShopItem`. В пресете задать обе логики и при необходимости `hiddenInShowcase`. Пресет регистрируется в `Database` как обычная запись.

### 4. Покупка

```csharp
public async void OnBuyClicked(string itemGuid)
{
    if (ShopBus.IsBusy)
    {
        ui.ShowBusyHint();
        return;
    }

    var operation = await ShopBus.Buy(itemGuid, count: 1);
    if (operation == null) return;                 // неизвестный товар

    switch (operation.State.Value)
    {
        case PurchaseState.Delivered: ui.ShowSuccess();          break;
        case PurchaseState.Ready:     ui.ShowClaimButton(operation); break;
        case PurchaseState.Pending:   ui.ShowRetryButton(operation); break;
        case PurchaseState.NotStarted:
        case PurchaseState.Cancelled: ui.ShowRefusal();          break;
    }
}
```

### 5. Реакция UI на состояние

`ShopOperation.State` реактивен — UI подписывается и не опрашивает:

```csharp
private void Bind(ShopOperation operation)
{
    operation.State.OnUpdate += OnStateChanged;
}

private void OnStateChanged(PurchaseState state) => view.Apply(state);
```

### 6. Незавершённые покупки

Восстановление открытых покупок запускается автоматически при загрузке. UI показывает то, что осталось в равновесии:

```csharp
foreach (var operation in ShopOperationsBus.GetReady())
    ui.AddClaimCard(operation);       // → ShopBus.ConfirmDelivery(operation)

foreach (var operation in ShopOperationsBus.GetOpen())
    if (operation.State.Value == PurchaseState.Pending)
        ui.AddRetryCard(operation);   // → ShopBus.RetryDelivery(operation)
```

### 7. Отмена

```csharp
ShopBus.CancelWithRefund(operation);
```

Прерывает процесс, если он идёт по этой покупке, дожидается размотки и затем возвращает оплату (`Paid` / `Ready` / `Pending`) либо закрывает заказ как `Cancelled` (`Ordered`).

## Граничные случаи

| Ситуация | Поведение |
|----------|-----------|
| Ассет `ShopSettings` отсутствует | Исключение при подключении драйвера (fail-fast на старте) |
| Драйвер не назначен в `DriverConfig` | `SetDriver` возвращает `false`; обращения к статикам `ShopBus` → `NullReferenceException` |
| Два пресета с одинаковым guid | Исключение в `Init`: каталог наполнен частично, `OnReady` не открывается |
| Неизвестный `itemGuid` в `Buy` | Лог ошибки, возврат `null` |
| Товар без логики оплаты или выдачи | Покупка сразу закрывается как `Failed` с записью в журнал, списания нет |
| Повторный вызов при активном процессе | Предупреждение в лог; `Buy` / `BuyForget` → `null`, `Confirm` / `Retry` → без действия |
| Предпроверка отклонила покупку | `NotStarted`, в журнал не пишется, в индексах отсутствует |
| Логика бросила исключение | Логируется, покупка остаётся в текущем состоянии и видна в `GetOpen` |
| Отмена во время оплаты | Логика получает `OperationCanceledException`; по факту состояния — возврат либо `Cancelled` |
| Возврат отклонён логикой | Покупка закрывается как `Failed` и попадает в `GetStuck` |
| Товар пропал/сломан к моменту восстановления | Лог ошибки, покупка не прогоняется и остаётся в текущем состоянии |
| Нарушена сквозная нумерация журнала | Ошибка в лог один раз, свёртка продолжается |
| Запрос к `ShopOperationsBus` до загрузки сессии | `GetOpen` → `null`, остальные геттеры → `null` |
| Огромная цена × количество пачек | Опорные значения и все умножения бриджа считаются в `long` (`PayValue`/`BuyValue`, агрегатные операции инвентаря). Переполнение `int` в отрицательное (и бесплатная покупка на нём) исключено |
| Выдаваемое не вмещается в инвентарь | `CanDelivery` отклоняет покупку **до оплаты** (`CanAdd` — тихая проба без создания предмета, по верификаторам). Стандартное «нет места». При оплате из того же инвентаря непотраченная валюта на момент проверки считается занимающей место — сперва освободить, потом купить. `AddCount` вдобавок атомарен (откат), так что дублирования нет и на редком пути отказа после оплаты |

## Бридж с инвентарём

Опциональная интеграция с пакетом инвентарей — папка `InventoryBridge/`, за `#if USING_VORTEX_ITEMS && USING_VORTEX_SHOP`. Магазин ссылается на `InventorySystem`/`ItemsSystem` (сборки пустые при выключенном define, ссылка безвредна).

| Единица | Роль |
|---------|------|
| `TradingInventory` | Статик-событие `OnRequested` — «какой инвентарь сейчас торгует». Магазин не знает, где живёт инвентарь игрока; отвечает подписчик из L4 (из своего `IGameData`-модуля, активного персонажа и т.п.). Один активный процесс контроллера делает ответ однозначным |
| `AddToInventoryDelivery : DeliveryLogic` | Выдаёт купленный предмет (`[DbRecord]`) в торгующий инвентарь через `AddCount` |
| `PayWithItemsPayment : PaymentLogic` | Оплата предметами-валютой (`[DbRecord]`, любой предмет — монеты, ключи): списание `RemoveCount` и возврат `AddCount` идут сквозь стеки |

L4 отвечает на событие как ему удобно — идентичность инвентаря не заводится (ни guid, ни маркеров): системные инвентари адресуются типом `IGameData`-модуля, эмбед — ссылкой через хозяина.

## Файловая структура

```
ShopSystem/
├── IShopController.cs                    # контракт драйвера проводки
├── IShopTransactionsController.cs        # контракт писателя журнала
├── Bus/
│   ├── ShopBus.cs                        # проводка: покупка, выдача, отмена, каталог
│   └── ShopOperationsBus.cs              # учёт: запросы к индексам и журналу
├── Controllers/
│   ├── ShopController.cs                 # драйвер: стейт-машина, отмена, busy-guard
│   ├── ShopOperationsController.cs       # owner-ключ, свёртка журнала, индексы
│   └── ShopTransactionsController.cs     # сборка и запись события
├── Model/
│   ├── PurchaseState.cs                  # состояния покупки
│   ├── AfterpayMode.cs                   # политика после оплаты
│   ├── ShopItemModel.cs                  # запись каталога
│   ├── ShopOperation.cs                  # рантайм-модель покупки
│   ├── ShopOperations.cs                 # IGameData: журнал + индексы
│   ├── ShopTransactionEvent.cs           # событие журнала
│   └── Logics/
│       ├── PaymentLogic.cs               # abstract: списание / возврат
│       ├── DeliveryLogic.cs              # abstract: выдача
│       ├── Payments/
│       │   └── FreeLogic.cs              # нулевая цена
│       └── Deliveries/
│           └── LimitedLogic.cs           # гейт-ограничитель: лимит количества за период
├── Presets/
│   ├── ShopItemPreset.cs                 # пресет товара
│   └── ShopSettings.cs                   # ICoreAsset: AfterpayMode
├── DefineSettings/
│   └── SdkSettings.Shop.cs               # тумблер USING_VORTEX_SHOP
└── ru.vortex.sdk.shopsystem.asmdef
```

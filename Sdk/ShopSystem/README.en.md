# ShopSystem

Vortex framework Sdk package: transactional purchase engine. Order → payment → delivery, with every transition recorded as an event in an append-only journal stored inside the save. Unfinished purchases are restored after a reload; an in-flight purchase can be interrupted with a refund.

## Purpose

- Running a purchase as a state machine, recording every transition into the journal
- Polymorphic payment and delivery logics (`[SerializeReference]`) — both sides of the deal are defined by the project
- Post-payment policy (`AfterpayMode`), global per game: auto-delivery with rollback, hold for retry, or wait for player confirmation
- Restoring unfinished purchases on save load by folding the journal
- Cancelling an in-flight purchase: interrupting the process via token and refunding the payment
- Runtime indexes for queries: open / ready-to-claim / stuck purchases, history per purchase and per item
- Built-in payment logics: `FreeLogic` (zero price)

Out of scope:

- Concrete charge and delivery rules (wallet, inventory, entitlements) — subclasses of `PaymentLogic` / `DeliveryLogic` in the project
- Storefront and purchase UI — consumers subscribe to the reactive operation state
- Server-side payment verification: the package knows nothing about protocols; network logic reconciles the charge by `PurchaseGuid` on its own
- Composing several rules on one side of the deal — an item has a single payment logic and a single delivery logic; several rules on the same side are implemented as one combined logic. A quantity cap is not a logic but an item property (`LimitedProperty`), honored by inventory delivery via the `CanAdd` verifiers

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `ru.vortex.system` | `SystemController`, `Singleton`, `ISystemDriver` |
| `ru.vortex.database` | `Database`, `Record` — item catalog |
| `ru.vortex.extensions` | `ListData` / `EnumData`, `InitValve`, `Crypto`, `[POCO]` |
| `ru.vortex.unity.database` | `RecordPreset<T>` — item preset |
| `ru.vortex.unity.extensions` | `SoData` — `RecordPreset` base |
| `ru.vortex.unity.editortools` | `[ToggleButton]` in the `SdkSettings` toggle |
| `ru.vortex.unity.CoreAssetsSystem` | `ICoreAsset` — settings asset auto-creation |
| `ru.vortex.sdk.game.core` | `GameController`, `GameModel.IGameData`, `PlayTime` / `AppTime` |
| UniTask | Asynchronous payment and delivery logics |
| Sirenix Odin Inspector | `[SerializeReference]` picker, `[InfoBox]`, `[HideReferenceObjectPicker]` |

Assembly: `ru.vortex.sdk.shopsystem`. The entire package is guarded by `#if USING_VORTEX_SHOP`; the symbol is toggled by the `shopSdk` switch in the `SdkSettings` asset.

## Architecture

### Configuration and catalog

```
ShopItemPreset (ScriptableObject)             ← designer config
   ├─ [SerializeReference] PaymentLogic       ← charge / refund
   ├─ [SerializeReference] DeliveryLogic      ← delivery
   └─ hiddenInShowcase
            │  CopyFrom when the record is built (RecordTypes.Singleton)
            ▼
      ShopItemModel : Record  ──────────────► catalog: ShopBus.Instance.ShopItems
```

### Two buses

Transaction handling and bookkeeping are separated: one bus changes the state of the deal, the other only reads data.

| Bus | Domain | Members |
|-----|--------|---------|
| `ShopBus` | Transaction | `Buy`, `BuyForget`, `ConfirmDelivery`, `RetryDelivery`, `CancelWithRefund`, `RestoreOperation`, `IsBusy`, catalog |
| `ShopOperationsBus` | Bookkeeping (read-only) | `GetOpen`, `GetReady`, `GetStuck`, `GetOperation`, `GetHistory`, `GetPurchaseHistory`, `GetGoodsHistory` |

### Data and restoration

```
ShopController (ShopBus driver)
       │  every state transition
       ▼
ShopTransactionEvent ──► ShopOperations.Events      ← journal, part of the save body
                                │
                                │  folded on NewGame / OnLoad
                                ▼
        Operations          — folded purchase by PurchaseGuid
        Transactions        — events by PurchaseGuid
        GoodsOperations     — purchases by ItemGuid
                                │
                                ▼
                    open purchases are resumed automatically
```

The journal `Events` is the source of truth. All three indexes are derived from it, are not part of the save, and are rebuilt on every load. The journal and the index lists are locked with the controller's owner key: they cannot be modified from outside the package.

### State machine

```
Ordered ──payment ok──► Paid ──┬─ AfterpayMode.Ready ───────► Ready ──ConfirmDelivery──► Delivered
   │                           │
   │ payment refused           ├─ delivery succeeded ───────► Delivered
   ▼                           │
Cancelled                      └─ delivery unavailable/failed:
                                     Pending  → Pending ──RetryDelivery──► Delivered
                                     Rollback → refund ──► Refunded (ok) / Failed (refused)
                                     Ready    → state unchanged
```

`Ordered` and `Paid` are advanced automatically. `Ready` and `Pending` are equilibrium states: the automatic run stops there, and only an explicit call moves them further.

## Key concepts

| Concept | Description |
|---------|-------------|
| Item (`ShopItemModel`) | Immutable catalog record with two logics. Built from `ShopItemPreset`, record type is `Singleton` |
| Payment logic (`PaymentLogic`) | Charge and refund. Mandatory; a free item uses `FreeLogic`, not `null` |
| Delivery logic (`DeliveryLogic`) | Handing the item to the player. Mandatory |
| Operation (`ShopOperation`) | Runtime purchase model with a reactive `State` (`EnumData<PurchaseState>`) |
| Event (`ShopTransactionEvent`) | Immutable journal record: state, deal values, three time axes |
| Journal (`Events`) | Append-only event list with a continuous `Sequence`. Source of truth, part of the save |
| Policy (`AfterpayMode`) | What to do after payment if delivery did not happen. Global per game |

## Purchase states

| State | Meaning | Terminal |
|-------|---------|----------|
| `Ordered` | Order created, pre-checks passed, payment not attempted | — |
| `Paid` | Payment confirmed | — |
| `Ready` | Paid, delivery awaits player confirmation | — (equilibrium) |
| `Pending` | Delivery failed, purchase held for retry | — (equilibrium) |
| `Delivered` | Item handed over | yes |
| `Refunded` | Payment returned | yes |
| `Cancelled` | Payment did not go through, nothing charged | yes |
| `Failed` | Neither deliverable nor refundable: item has no logics, or the refund was refused | yes |
| `NotStarted` | Aborted at pre-checks. **Not written to the journal** | — |

## AfterpayMode policy

Applies only to automatic delivery from `Paid`. Explicit `ConfirmDelivery` and `RetryDelivery` are not subject to the policy.

| Mode | Behaviour |
|------|-----------|
| `Rollback` | Auto-delivery. On refusal — refund (`Refunded`; `Failed` if the refund is refused) |
| `Pending` | Auto-delivery. On refusal — hold in `Pending` for an external retry |
| `Ready` | No auto-delivery: `Paid` goes straight to `Ready`, the player initiates delivery |

## Critical requirements

1. **Both logics are mandatory.** An item without `PaymentLogic` or `DeliveryLogic` is non-functional: the purchase is closed as `Failed` before any charge. A free item is built with a dedicated logic (`FreeLogic`), not a `null` reference.
2. **Delivery from `Ready` is initiated by the player only.** Restoration and the automatic run never advance `Ready` — otherwise confirmation would be bypassed on reload.
3. **Every step must either change the state or end in equilibrium.** A logic returning `false` without a following transition would loop the automatic run. That is why the refund always terminalizes: success → `Refunded`, refusal → `Failed`.
4. **One active interactive process.** `Buy` / `BuyForget` / `ConfirmDelivery` / `RetryDelivery` are rejected while a previous one is running. Pre-check with `ShopBus.IsBusy`.
5. **Cancellation does not cancel the refund.** The cancellation token is passed to `CanPay` / `MakePay` / `CanDelivery` / `MakeDelivery`, but **not** to `MakeRefund`: a compensating action must run to completion.
6. **For non-network payments keep the `Ordered → Paid` transition synchronous.** Network logic must be able to reconcile by `PurchaseGuid` after an interruption or restart.

## Contract

### Input

- A `ShopSettings` asset with the chosen `AfterpayMode` (auto-created, see "Usage")
- `ShopItemPreset` assets with both logics filled in, registered in `Database`
- The `ShopController` driver assigned to `ShopBus` in `DriverConfig`
- A loaded game session: the journal lives in `GameModel`

### Output

- `ShopBus.Buy` / `BuyForget` → `ShopOperation` with a reactive `State`, or `null` (busy or unknown item)
- The `ShopOperations.Events` journal inside the save body
- `ShopOperationsBus` queries — slices over the indexes

### Guarantees

- Every state transition is recorded by exactly one event with a continuous monotonic `Sequence`
- An event is self-contained: purchase and item guids, state, deal values, and three time axes (UTC seconds, `PlayTime`, `AppTime`)
- The journal is locked with an owner key: mutation is possible only from within the package controller
- Indexes are fully rebuilt from the journal on every load; a break in the continuous numbering is reported to the log
- The automatic run always terminates: on a terminal state, on equilibrium (`Ready` / `Pending`), or on cancellation
- An exception from a logic does not break the process: it is logged, the purchase stays in its current state and appears in `GetOpen`
- Cancellation leaves no intermediate states: the process unwinds to a state boundary, and only then the refund decision is made

### Limitations

- **One active process.** Parallel interactive purchases are not supported.
- **`NotStarted` is not journaled.** A purchase rejected by pre-checks does not enter any index.
- **The policy is not snapshotted.** An open purchase finishes under the current `AfterpayMode` value, not the one in effect when it was ordered.
- **One payment logic per item.** Rule combinations are assembled inside a single logic.
- **`ShopBus` statics are not guarded against a missing driver** (except `IsBusy`): access before registration throws `NullReferenceException`.
- **Restoration is not cancellable.** It runs on a local token and is not affected by `CancelWithRefund`.

## API Reference

```csharp
// ── Transaction ───────────────────────────────────────────────────────────
// The first parameter is the ITEM guid (ShopItemModel.GuidPreset), not the purchase guid
UniTask<ShopOperation> ShopBus.Buy(string itemGuid, int count);
ShopOperation          ShopBus.BuyForget(string itemGuid, int count);

void ShopBus.ConfirmDelivery(ShopOperation operation);   // from Ready
void ShopBus.RetryDelivery(ShopOperation operation);     // from Pending
void ShopBus.CancelWithRefund(ShopOperation operation);  // interrupt + refund
void ShopBus.RestoreOperation(ShopOperation operation);  // resume an open purchase

bool ShopBus.IsBusy;                                     // busy pre-check
IReadOnlyDictionary<string, ShopItemModel> ShopBus.Instance.ShopItems;

// Readiness: InitValve exposes Subscribe/Unsubscribe only, there is no += operator
ShopBus.OnReady.Subscribe(OnShopReady);

// ── Bookkeeping (read-only) ───────────────────────────────────────────────
IReadOnlyList<ShopOperation> ShopOperationsBus.GetOpen();    // Ordered/Paid/Ready/Pending
IReadOnlyList<ShopOperation> ShopOperationsBus.GetReady();   // awaiting confirmation
IReadOnlyList<ShopOperation> ShopOperationsBus.GetStuck();   // Failed — for support

ShopOperation                       ShopOperationsBus.GetOperation(string purchaseGuid);
ListData<ShopTransactionEvent>      ShopOperationsBus.GetHistory();
ListData<ShopTransactionEvent>      ShopOperationsBus.GetPurchaseHistory(string purchaseGuid);
ListData<ShopOperation>             ShopOperationsBus.GetGoodsHistory(string itemGuid);

ShopOperationsBus.OnReady.Subscribe(OnDataReady);

// ── Payment logic (implemented in the project) ───────────────────────────
public class GoldPayment : PaymentLogic
{
    [SerializeField] private int price = 100;

    public override int GetCount() => price;

    public override UniTask<bool> CanPay(string guid, int count, CancellationToken ct)
        => UniTask.FromResult(Wallet.Gold >= price * count);

    public override UniTask<bool> MakePay(ShopOperation operation, CancellationToken ct)
        => UniTask.FromResult(Wallet.TrySpend(price * operation.RequestedCount));

    // No cancellation token: the refund must run to completion
    public override UniTask<bool> MakeRefund(ShopOperation operation)
        => UniTask.FromResult(Wallet.Refund(operation.PayValue * operation.RequestedCount));
}

// ── Delivery logic (implemented in the project) ──────────────────────────
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

## Usage

### 1. Settings asset

`ShopSettings` implements `ICoreAsset` and is created automatically in `Assets/Resources/Settings/`. If auto-creation is disabled — `Tools → Vortex → Debug → Check Core Assets`. The asset holds the `AfterpayMode` choice.

### 2. Driver registration

In the `DriverConfig` asset: **Reload** → pick `ShopController` as the driver for the `ShopBus` system → **Save Config**. Without an entry in the config `SetDriver` rejects the registration, and bus calls fail with `NullReferenceException`.

### 3. Logics and items

Implement `PaymentLogic` and `DeliveryLogic` subclasses (see API Reference) and mark the classes `[Serializable]` — otherwise they will not appear in the `[SerializeReference]` picker.

Item: `Assets → Create → Database → ShopItem`. Set both logics in the preset and `hiddenInShowcase` if needed. The preset is registered in `Database` like any other record.

### 4. Purchase

```csharp
public async void OnBuyClicked(string itemGuid)
{
    if (ShopBus.IsBusy)
    {
        ui.ShowBusyHint();
        return;
    }

    var operation = await ShopBus.Buy(itemGuid, count: 1);
    if (operation == null) return;                 // unknown item

    switch (operation.State.Value)
    {
        case PurchaseState.Delivered: ui.ShowSuccess();              break;
        case PurchaseState.Ready:     ui.ShowClaimButton(operation); break;
        case PurchaseState.Pending:   ui.ShowRetryButton(operation); break;
        case PurchaseState.NotStarted:
        case PurchaseState.Cancelled: ui.ShowRefusal();              break;
    }
}
```

### 5. UI reacting to state

`ShopOperation.State` is reactive — the UI subscribes instead of polling:

```csharp
private void Bind(ShopOperation operation)
{
    operation.State.OnUpdate += OnStateChanged;
}

private void OnStateChanged(PurchaseState state) => view.Apply(state);
```

### 6. Unfinished purchases

Restoration of open purchases starts automatically on load. The UI presents whatever stopped at equilibrium:

```csharp
foreach (var operation in ShopOperationsBus.GetReady())
    ui.AddClaimCard(operation);       // → ShopBus.ConfirmDelivery(operation)

foreach (var operation in ShopOperationsBus.GetOpen())
    if (operation.State.Value == PurchaseState.Pending)
        ui.AddRetryCard(operation);   // → ShopBus.RetryDelivery(operation)
```

### 7. Cancellation

```csharp
ShopBus.CancelWithRefund(operation);
```

Interrupts the process if it is running for this purchase, waits for it to unwind, and then refunds the payment (`Paid` / `Ready` / `Pending`) or closes the order as `Cancelled` (`Ordered`).

## Edge cases

| Situation | Behaviour |
|-----------|-----------|
| `ShopSettings` asset missing | Exception when the driver connects (fail-fast at startup) |
| Driver not assigned in `DriverConfig` | `SetDriver` returns `false`; `ShopBus` static calls → `NullReferenceException` |
| Two presets with the same guid | Exception in `Init`: the catalog is filled partially, `OnReady` never opens |
| Unknown `itemGuid` in `Buy` | Error logged, returns `null` |
| Item without payment or delivery logic | Purchase is immediately closed as `Failed` with a journal record, nothing charged |
| Repeated call while a process is active | Warning logged; `Buy` / `BuyForget` → `null`, `Confirm` / `Retry` → no action |
| Pre-check rejected the purchase | `NotStarted`, not journaled, absent from the indexes |
| A logic threw an exception | Logged; the purchase stays in its current state and is visible in `GetOpen` |
| Cancellation during payment | The logic receives `OperationCanceledException`; depending on the state — refund or `Cancelled` |
| Refund refused by the logic | The purchase is closed as `Failed` and appears in `GetStuck` |
| Item missing/broken at restoration time | Error logged, the purchase is not advanced and stays in its current state |
| Journal numbering continuity broken | Error logged once, folding continues |
| `ShopOperationsBus` queried before the session is loaded | `GetOpen` → `null`, other getters → `null` |
| Huge price × pack count | Reference values and all bridge multiplications are computed in `long` (`PayValue`/`BuyValue`, inventory aggregate ops). `int` overflow into negative (and the free purchase it would cause) is ruled out |
| Delivered goods do not fit the inventory | `CanDelivery` rejects the purchase **before payment** (`CanAdd` — a silent probe, no item creation, via the verifiers). Standard "no room". When paying from the same inventory, the unspent currency counts as occupying space at check time — free space first, then buy. `AddCount` is additionally atomic (rollback), so there is no duplication even on the rare post-payment rejection path |

## Inventory bridge

Optional integration with the inventory package — the `InventoryBridge/` folder, behind `#if USING_VORTEX_ITEMS && USING_VORTEX_SHOP`. The shop references `InventorySystem`/`ItemsSystem` (those assemblies are empty when their define is off, so the reference is harmless).

| Unit | Role |
|------|------|
| `TradingInventory` | Static event `OnRequested` — "which inventory is trading now." The shop does not know where the player inventory lives; a subscriber in L4 answers (from its own `IGameData` module, the active character, etc.). The controller's single active process makes the answer unambiguous |
| `AddToInventoryDelivery : DeliveryLogic` | Delivers the bought item (`[DbRecord]`) into the trading inventory via `AddCount` |
| `PayWithItemsPayment : PaymentLogic` | Payment with currency-items (`[DbRecord]`, any item — coins, keys): removal `RemoveCount` and refund `AddCount` go through stacks |

L4 answers the event however it likes — no inventory identity is introduced (no guid, no markers): systemic inventories are addressed by the `IGameData` module type, embedded ones by reference through the host.

## File structure

```
ShopSystem/
├── IShopController.cs                    # transaction driver contract
├── IShopTransactionsController.cs        # journal writer contract
├── Bus/
│   ├── ShopBus.cs                        # transaction: purchase, delivery, cancel, catalog
│   └── ShopOperationsBus.cs              # bookkeeping: index and journal queries
├── Controllers/
│   ├── ShopController.cs                 # driver: state machine, cancellation, busy guard
│   ├── ShopOperationsController.cs       # owner key, journal folding, indexes
│   └── ShopTransactionsController.cs     # event assembly and writing
├── Model/
│   ├── PurchaseState.cs                  # purchase states
│   ├── AfterpayMode.cs                   # post-payment policy
│   ├── ShopItemModel.cs                  # catalog record
│   ├── ShopOperation.cs                  # runtime purchase model
│   ├── ShopOperations.cs                 # IGameData: journal + indexes
│   ├── ShopTransactionEvent.cs           # journal event
│   └── Logics/
│       ├── PaymentLogic.cs               # abstract: charge / refund
│       ├── DeliveryLogic.cs              # abstract: delivery
│       └── Payments/
│           └── FreeLogic.cs              # zero price
├── Presets/
│   ├── ShopItemPreset.cs                 # item preset
│   └── ShopSettings.cs                   # ICoreAsset: AfterpayMode
├── DefineSettings/
│   └── SdkSettings.Shop.cs               # USING_VORTEX_SHOP toggle
└── ru.vortex.sdk.shopsystem.asmdef
```

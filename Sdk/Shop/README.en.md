# Shop

**Namespace:** `Vortex.Sdk.Shop.*`
**Assembly:** `ru.vortex.sdk.shop`
**Layer:** 3 (SDK)

---

## Purpose

A transactional purchase engine that knows nothing about currency or goods. Both sides of the deal are pluggable logics: **`PaymentLogic`** (what to charge and from where) and **`BuyCaseLogic`** (what to grant). The engine orchestrates the deal, keeps an immutable journal, and guarantees that a mid-flow failure does not eat the payment.

One system for all kinds of shops: purchase for in-game currency, resource swap, conditional reward grant, a daily item with auto-charge — the only difference is the two logic implementations at the project level (L4).

Capabilities:
- An item is a Database record configured by a designer; the engine does not touch pricing.
- Each purchase is a chain of immutable events in an append-only journal (order, pay, deliver, refund, cancel). Records are never edited retroactively.
- Price is captured at deal time (absorption), so the record stays truthful after coefficient patches.
- A delivery failure is resolved by policy: refund, hold for retry, or wait for the player to confirm.
- Everything survives a restart — within the last save point (the journal is slot-local).
- Time axes on every event: sequence (order), playSeconds/appSeconds (playthrough stage / engagement), timestamp (untrusted calendar reference).

Out of scope:
- Concrete `PaymentLogic`/`BuyCaseLogic` for the project's currency — they live at L4.
- Showcase and "my purchases" UI — derived from the engine, built by the consumer.
- Real-money purchases outside a game session (DLC) — a separate contour, not in this journal.

---

## Dependencies

| Dependency | Purpose |
|---|---|
| `Vortex.Sdk.Core.GameCore` | `GameController.PlayTime`/`AppTime` for event time axes; `GameModel.IGameData` for the journal |
| `Vortex.Core.DatabaseSystem` | `Record`, `Database.GetRecords` — item catalog |
| `Vortex.Unity.DatabaseSystem` | `RecordPreset<T>` — item preset |
| `Vortex.Core.Extensions` | `Crypto.GetNewGuid`, `[POCO]` for journal serialization |
| `Vortex.Core.ComplexModelSystem` | `GameModel` base |
| UniTask (Cysharp) | async contract of the payment/delivery logics |

---

## Architecture

```
[ShopItemPreset] (RecordPreset, SO, Singleton)          [ShopSettings] (SO, outside Database)
   ├── PaymentLogic  [SerializeReference]                   └── FallbackMode: Rollback / Pending / Ready
   ├── BuyCaseLogic  [SerializeReference]
   └── HiddenInShowcase
          │  CopyFrom (DeepCopy clone of logics)
          ▼
[ShopItemRecord : Record]  (Database, GetDataForSave()=>null)

[ShopController] (Singleton, implements IShopController + IShopJournalReader)
   ├── transaction: Buy → Ordered → Pay → Paid → delivery → terminal
   ├── the single event-emission point (Emit)
   ├── runtime index over the journal (O(1) folds)
   └── catalog resolution (empty item on unknown guid)
          │  journal events
          ▼
[ShopStatisticsData : GameModel.IGameData]  (save body, append-only)
   ├── List<ShopTransactionEvent>
   └── long Sequence

Two buses (two domains):
[ShopBus]      — transaction: Buy/BuyForget, ConfirmDelivery, RetryDelivery, CancelWithRefund, GetItem, events
[ShopJournal]  — accounting (read-only): GetOpen/GetReady/GetStuck, GetPurchase, GetPurchaseHistory, GetHistory
```

Transaction and accounting are different domains with different consumers, hence two entry points.

### Purchase states

A state is an operation type recorded in the journal (not a runtime flag), so it survives a restart. Folding by `purchaseGuid` yields the current state (by maximum `Sequence`).

| State | Terminal | Meaning |
|---|---|---|
| `Ordered` | no | Order created, pre-checks passed, payment not attempted |
| `Paid` | no | Payment confirmed |
| `Ready` | no | Paid, delivery awaits player confirmation (no auto-delivery) |
| `Pending` | no | Delivery failed, purchase held for retry |
| `Delivered` | yes | Item granted |
| `Refunded` | yes | Payment refunded |
| `Cancelled` | yes | Payment did not go through, nothing charged |
| `Failed` | yes | Cannot deliver nor refund — the item has no logics |

### Fallback policies (`ShopSettings.FallbackMode`, global)

| Policy | Behavior after `Paid` |
|---|---|
| `Rollback` | Auto-deliver; on failure — refund and close |
| `Pending` | Auto-deliver; on failure — hold in `Pending` for external retry |
| `Ready` | No auto-delivery; move to `Ready`, wait for player confirmation |

`Ready` is held on a failed confirmation regardless of policy; the only exits are a successful delivery or a manual cancel-with-refund.

---

## Contract

### Who changes state

Only the **controller** emits events. Logics do not write state — they report the outcome by calling methods on a scoped context bound to a single purchase. The static controller does not subscribe to logic-instance events.

Flow: the controller creates the purchase (`Ordered`) → hands data to `PaymentLogic` → it calls the context (`Paid`/`Cancelled`) on the answer → the controller hands over to `BuyCaseLogic` → it calls the context (`Delivered`/`Ready`/`Pending`/`Failed`).

### PaymentLogic

```csharp
public abstract class PaymentLogic
{
    ShopRefusal? CanPay(int requestedCount);        // sync pre-check, null = allowed
    UniTask Pay(IPayContext context);               // Paid(payValue) or Cancelled(reason)
    UniTask Refund(IRefundContext context);         // Refunded() or Failed(reason)
}
```

### BuyCaseLogic

```csharp
public abstract class BuyCaseLogic
{
    ShopRefusal? CanDeliver(int requestedCount);    // sync pre-check
    UniTask Deliver(IBuyContext context);           // Delivered/Ready/Pending/Failed
}
```

### Requirements on logics

1. **Stateless.** `CopyFrom` gives each record its own clone of the logic (DeepCopy), `GetDataForSave()=>null` — logic state is not saved. Everything needed to retry delivery after a restart is in the journal event (`RequestedCount`, `PayValue`, `BuyValue` via the context).
2. **Pre-check is filtering, not guaranteeing** — the execution result is authoritative.
3. **Single saved contour (Inv-9).** Data the logic changes must be saved together with the journal. For a logic whose effect is inside the save contour, the `Ordered → Paid/Cancelled` path must be **synchronous**. An async path is only for an effect outside the save contour (server-authoritative payment); its recovery after an interruption is the implementation's responsibility, the engine guarantees a stable `purchaseGuid` as the idempotency key.
4. **An exception leaves no dangling purchase.** The engine wraps every logic await in `try/catch`: a payment exception → the purchase closes as `Cancelled`, a delivery exception → treated as failure (fallback policy), a refund exception → the purchase stays open (Inv-6).

---

## API

### ShopBus (transaction)

| Member | Purpose |
|---|---|
| `Init(ShopSettings)` | Engine initialization. Called by the project's bootstrap |
| `GetItem(itemGuid)` | Item. Unknown guid → empty item (stub without logics), not null |
| `UniTask<ShopResult> Buy(itemGuid, count)` | Purchase. Returns the final outcome; for UI await |
| `BuyForget(itemGuid, count)` | Synchronous fire-and-forget wrapper |
| `ShopRefusal? ConfirmDelivery(purchaseGuid)` | Confirm from `Ready`. `PurchaseBusy` when the purchase is busy |
| `ShopRefusal? RetryDelivery(purchaseGuid)` | Retry from `Pending` |
| `ShopRefusal? CancelWithRefund(purchaseGuid)` | Manual cancel of an open purchase with refund |
| `event OnPurchaseStateChanged(guid, state)` | Purchase state changed |
| `event OnPurchaseClosed(ShopPurchase)` | Purchase closed by a terminal event |

### ShopJournal (accounting, read-only)

| Member | Purpose |
|---|---|
| `GetOpen()` | Open (non-terminal) purchases |
| `GetReady()` | Ready to collect (`Ready`) |
| `GetStuck()` | Stuck (`Failed`) — for support |
| `GetPurchase(guid)` | Fold of a specific purchase |
| `GetPurchaseHistory(guid)` | All events of a specific purchase |
| `GetHistory()` | Full journal history |
| `event OnJournalUpdated` | The journal got a new event |

The `Ready` list is separated from `Failed` deliberately: regular uncollected rewards do not clutter failure triage.

---

## Usage

### Item and settings

`Assets → Create → Vortex → Shop → Shop Item` — item preset: assign `PaymentLogic`/`BuyCaseLogic` (project implementations) and the visibility flag.
`Assets → Create → Vortex → Shop → Shop Settings` — fallback policy.

### Init and purchase

```csharp
ShopBus.Init(shopSettings);   // from the project's bootstrap

// purchase awaiting the outcome (UI)
var result = await ShopBus.Buy(itemGuid, count: 3);
if (!result.Started)
    ShowRefusal(result.Refusal.Value);        // pre-check refusal
else if (result.Purchase.State == PurchaseState.Delivered)
    ShowSuccess();
else if (result.Refusal.HasValue)
    ShowRefusal(result.Refusal.Value);        // runtime cancel (LogicRejected, etc.)

// automation / no await needed
ShopBus.BuyForget(itemGuid, 1);
```

### Daily item (the Ready scenario)

An item with a `PaymentLogic` that auto-charges on a timer, under the global `Ready` policy: `Buy` reaches `Paid` and moves the purchase to `Ready`. The player clicks to collect:

```csharp
foreach (var p in ShopJournal.GetReady())
    ShowClaimable(p);

// on click
var refusal = ShopBus.ConfirmDelivery(purchaseGuid);
if (refusal == ShopRefusal.PurchaseBusy)
    ShowBusy();
```

### Subscribing to the journal

```csharp
private void OnEnable()  => ShopJournal.OnJournalUpdated += RefreshShowcase;
private void OnDisable() => ShopJournal.OnJournalUpdated -= RefreshShowcase;
```

---

## Edge Cases

| Situation | Behavior |
|---|---|
| Unknown `itemGuid` | Empty item (no logics) → `LogicUnavailable` |
| `count <= 0` | `InvalidCount`, journal not written |
| An order exists between `Ordered` and `Paid` | `OrderInProgress` (Inv-7) — no new order starts |
| An operation is already running on the purchase | `PurchaseBusy` (Inv-8) |
| Payment logic threw / stayed silent | Purchase closes as `Cancelled` (reason `LogicRejected`) — does not hang in `Ordered` |
| Delivery logic threw | Treated as failure → fallback policy |
| Refund failed | Purchase stays open, no terminal written (Inv-6) |
| Item "removed" (flag + `BuyCaseLogic` swapped to compensation, `PaymentLogic` removed) | Hidden in showcase; an open purchase resolves to the compensation and completes |
| Item truly gone (no logics) | An open purchase closes as `Failed`, appears in `GetStuck()` |
| Loading an earlier slot | The journal rolls back with the slot — accepted behavior (purchases are slot-local) |
| Saving from an active purchase | The unclosed purchase is in the journal; after load it is available for completion via `GetOpen()`/`RetryDelivery` |
| Uncollected `Ready` accumulate | Normal: nothing burns; served as a separate `GetReady()` list |

using System;
using System.Collections.Generic;
using Vortex.Sdk.Core.GameCore;
using Vortex.Sdk.Shop.Model;

namespace Vortex.Sdk.Shop.Controllers
{
    public partial class ShopController
    {
        private ShopStatisticsData Journal => GameController.Get<ShopStatisticsData>();

        #region Runtime index

        // Рантайм-индекс над журналом — не сериализуется. Свёртки O(1) вместо O(журнал) на каждую
        // покупку (иначе перечни были бы O(n²)). Обновляется инкрементально в Emit; перестраивается,
        // когда сменился список событий (загрузка сейва / новая игра создают новый ShopStatisticsData).

        private readonly struct FoldState
        {
            public FoldState(ShopPurchase purchase, long maxSeq)
            {
                Purchase = purchase;
                MaxSeq = maxSeq;
            }

            public ShopPurchase Purchase { get; }
            public long MaxSeq { get; }
        }

        private readonly Dictionary<string, FoldState> _index = new();
        private readonly Dictionary<string, List<ShopTransactionEvent>> _eventsByGuid = new();
        private readonly List<string> _order = new(); // guid'ы в порядке первого появления

        /// <summary>Список событий, по которому построен индекс. Смена ссылки = журнал заменён (загрузка/NewGame).</summary>
        private List<ShopTransactionEvent> _indexedEvents;

        private void EnsureIndex()
        {
            var events = Journal?.Events;
            if (ReferenceEquals(events, _indexedEvents))
                return; // индекс актуален (та же ссылка; Add в Emit её не меняет)

            _index.Clear();
            _eventsByGuid.Clear();
            _order.Clear();
            _indexedEvents = events;
            if (events == null)
                return;

            foreach (var e in events)
                ApplyToIndex(e);
        }

        internal void ResetIndex()
        {
            _index.Clear();
            _eventsByGuid.Clear();
            _order.Clear();
            _indexedEvents = null;
        }

        /// <summary>
        /// Инкрементально учитывает событие в индексе. Состояние — по МАКСИМАЛЬНОМУ Sequence
        /// (авторитет порядка); payValue/buyValue — последние ненулевые (каждое ставится один раз).
        /// </summary>
        private void ApplyToIndex(ShopTransactionEvent e)
        {
            if (!_eventsByGuid.TryGetValue(e.PurchaseGuid, out var list))
            {
                list = new List<ShopTransactionEvent>();
                _eventsByGuid[e.PurchaseGuid] = list;
                _order.Add(e.PurchaseGuid);
            }

            list.Add(e);

            var exists = _index.TryGetValue(e.PurchaseGuid, out var prev);
            var payValue = e.PayValue != 0 ? e.PayValue : (exists ? prev.Purchase.PayValue : 0);
            var buyValue = e.BuyValue != 0 ? e.BuyValue : (exists ? prev.Purchase.BuyValue : 0);
            var maxSeq = exists ? prev.MaxSeq : long.MinValue;
            var state = exists ? prev.Purchase.State : e.Type;
            if (e.Sequence >= maxSeq)
            {
                maxSeq = e.Sequence;
                state = e.Type;
            }

            _index[e.PurchaseGuid] = new FoldState(
                new ShopPurchase(e.PurchaseGuid, e.ItemGuid, e.RequestedCount, state, payValue, buyValue),
                maxSeq);
        }

        #endregion

        #region Emission

        /// <summary>
        /// Единственная точка эмиссии события (Inv-2: sequence монотонен и уникален в слоте).
        /// Повторно то же состояние не пишется (удержание Ready/Pending при неудачном повторе не
        /// плодит дубли). Оси времени — из GameController; абсолютная метка — Unix-секунды из
        /// DateTimeOffset (не TimeController.Timestamp).
        /// </summary>
        private void Emit(string purchaseGuid, string itemGuid, PurchaseState state,
            int requestedCount, int payValue, int buyValue)
        {
            var journal = Journal;
            if (journal == null)
                return;

            EnsureIndex();
            if (StateOf(purchaseGuid) == state)
                return; // дубль состояния — не пишем

            var evt = new ShopTransactionEvent
            {
                PurchaseGuid = purchaseGuid,
                Sequence = journal.Sequence,
                ItemGuid = itemGuid,
                Type = state,
                RequestedCount = requestedCount,
                PayValue = payValue,
                BuyValue = buyValue,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                PlaySeconds = (long)GameController.PlayTime.TotalSeconds,
                AppSeconds = (long)GameController.AppTime.TotalSeconds
            };
            journal.Events.Add(evt);
            journal.Sequence++;
            ApplyToIndex(evt); // ссылка на список не меняется (Add), индекс остаётся актуальным

            OnPurchaseStateChanged?.Invoke(purchaseGuid, state);
            OnJournalUpdated?.Invoke();

            if (IsTerminal(state))
                OnPurchaseClosed?.Invoke(Fold(purchaseGuid));
        }

        private static bool IsTerminal(PurchaseState state) =>
            state is PurchaseState.Delivered or PurchaseState.Refunded
                or PurchaseState.Cancelled or PurchaseState.Failed;

        #endregion

        #region Fold (O(1) via index)

        /// <summary>Свёртка покупки по purchaseGuid. Пустая (Exists == false), если событий нет.</summary>
        private ShopPurchase Fold(string purchaseGuid)
        {
            if (string.IsNullOrEmpty(purchaseGuid))
                return default;
            EnsureIndex();
            return _index.TryGetValue(purchaseGuid, out var fs) ? fs.Purchase : default;
        }

        /// <summary>
        /// Текущее состояние покупки, либо null если событий нет. Nullable сознательно: иначе
        /// default(ShopPurchase).State == Ordered путал бы «пусто» с реальным Ordered.
        /// </summary>
        private PurchaseState? StateOf(string purchaseGuid)
        {
            if (string.IsNullOrEmpty(purchaseGuid))
                return null;
            EnsureIndex();
            return _index.TryGetValue(purchaseGuid, out var fs) ? fs.Purchase.State : (PurchaseState?)null;
        }

        #endregion
    }
}

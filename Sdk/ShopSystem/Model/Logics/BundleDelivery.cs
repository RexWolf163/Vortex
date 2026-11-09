#if USING_VORTEX_SHOP
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Sdk.ShopSystem.Model.Logics
{
    /// <summary>
    /// Набор: одна покупка выдаёт несколько товаров. Каждый элемент — самостоятельная
    /// <see cref="DeliveryLogic"/> со своими настройками; наборы можно вкладывать друг в друга.
    ///
    /// Проверка до оплаты (<see cref="CanDelivery"/>) — все элементы сразу: если не помещается хоть один,
    /// покупка не начинается. Выдача — по порядку списка. Отката у логик выдачи нет, поэтому при отказе
    /// элемента уже выданные остаются у игрока, а покупка уходит по политике <c>AfterpayMode</c>. Чтобы
    /// повторная выдача (<c>RetryDelivery</c>) не задвоила их, выданные элементы запоминаются по
    /// <see cref="ShopOperation.PurchaseGuid"/> до конца сеанса; после перезапуска память пуста — такой
    /// отказ после успешной предпроверки редок, поэтому её не сохраняем.
    ///
    /// Логика не предметная: <see cref="DeliveryLogic.GetItemGuid"/> — <c>null</c>, состав для UI —
    /// <see cref="Items"/>.
    /// </summary>
    [Serializable, InfoBox("Набор: одна покупка выдаёт все элементы списка по порядку.")]
    public class BundleDelivery : DeliveryLogic
    {
        // НЕ Array.Empty<>() — общий экземпляр при [SerializeReference] даёт алиас между наборами.
        [SerializeReference, Tooltip("Состав набора. Пусто или пустой элемент — товар не продаётся.")]
        private DeliveryLogic[] items = new DeliveryLogic[0];

        /// <summary>Выданные элементы по покупке — защита от задвоения при повторной выдаче. Только сеанс.</summary>
        [NonSerialized] private Dictionary<string, HashSet<int>> _delivered;

        /// <summary>Состав набора — для отображения.</summary>
        public IReadOnlyList<DeliveryLogic> Items => items;

        /// <summary>Набор — одна пачка; количество каждого товара задаёт его собственная логика.</summary>
        public override int GetCount() => 1;

        public override async UniTask<bool> CanDelivery(string guid, int count, CancellationToken ct)
        {
            if (!IsConfigured(guid))
                return false;

            foreach (var item in items)
                if (!await item.CanDelivery(guid, count, ct))
                    return false;

            return true;
        }

        public override async UniTask<bool> MakeDelivery(ShopOperation operation, CancellationToken ct)
        {
            if (!IsConfigured(operation.ItemGuid))
                return false;

            _delivered ??= new Dictionary<string, HashSet<int>>();
            if (!_delivered.TryGetValue(operation.PurchaseGuid, out var done))
                _delivered[operation.PurchaseGuid] = done = new HashSet<int>();

            for (var i = 0; i < items.Length; i++)
            {
                if (done.Contains(i))
                    continue;

                if (!await items[i].MakeDelivery(operation, ct))
                {
                    Debug.LogError($"[BundleDelivery] Покупка {operation.PurchaseGuid}: элемент [{i}] " +
                                   $"{items[i].GetType().Name} не выдан; выдано ранее: {done.Count} из {items.Length}.");
                    return false;
                }

                done.Add(i);
            }

            _delivered.Remove(operation.PurchaseGuid);
            return true;
        }

        private bool IsConfigured(string guid)
        {
            if (items.Length == 0)
            {
                Debug.LogError($"[BundleDelivery] Товар {guid}: набор пуст — продавать нечего.");
                return false;
            }

            for (var i = 0; i < items.Length; i++)
            {
                if (items[i] != null)
                    continue;
                Debug.LogError($"[BundleDelivery] Товар {guid}: элемент [{i}] не задан (или утрачен тип).");
                return false;
            }

            return true;
        }
    }
}
#endif

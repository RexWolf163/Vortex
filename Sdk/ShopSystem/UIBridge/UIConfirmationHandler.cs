#if USING_VORTEX_SHOP
using System;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.ShopSystem.Bus;
using Vortex.Sdk.ShopSystem.Model;

namespace Vortex.Sdk.ShopSystem.UIBridge
{
    /// <summary>
    /// Мост интерфейса подтверждения к <see cref="UIConfirmation"/> и источник его данных. Публичные
    /// <see cref="Approve"/>/<see cref="Decline"/> вешаются на кнопки «Подтвердить»/«Отмена» и передают
    /// вердикт в статику логики. Как <see cref="IDataStorage"/> отдаёт подтверждаемый <see cref="ShopItemModel"/>
    /// (по <see cref="UIConfirmation.CurrentGuid"/> из каталога <see cref="ShopBus"/>) — чтобы вью интерфейса
    /// показали товар. В одной сборке с <see cref="UIConfirmation"/>, поэтому читает её internal-члены напрямую.
    /// </summary>
    public class UIConfirmationHandler : MonoBehaviour, IDataStorage
    {
        public event Action OnUpdateLink;

        /// <summary>Подтверждаемый товар (<see cref="ShopItemModel"/>); прочие типы или нет подтверждения — null.</summary>
        public T GetData<T>() where T : class
        {
            if (typeof(T) != typeof(ShopItemModel))
                return null;

            var guid = UIConfirmation.CurrentGuid;
            if (string.IsNullOrEmpty(guid))
                return null;

            return ShopBus.Instance.ShopItems.TryGetValue(guid, out var model) ? model as T : null;
        }

        // На активации интерфейс уже открыт с выставленным CurrentGuid — оповещаем линкованные вью перечитать.
        private void OnEnable() => OnUpdateLink?.Invoke();

        /// <summary>Кнопка подтверждения: продолжить покупку.</summary>
        public void Approve() => UIConfirmation.Resolve(true);

        /// <summary>Кнопка отмены: покупка не начинается (NotStarted).</summary>
        public void Decline() => UIConfirmation.Resolve(false);
    }
}
#endif

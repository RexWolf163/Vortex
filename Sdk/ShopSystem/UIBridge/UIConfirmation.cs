#if USING_VORTEX_SHOP
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Model.Enums;
using Vortex.Core.UIProviderSystem.Bus;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Sdk.ShopSystem.Model.Logics;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.ShopSystem.UIBridge
{
    /// <summary>
    /// Подтверждение покупки через UI: открывает заданный интерфейс (<see cref="UIProvider"/>) и ждёт
    /// вердикта игрока. Вердикт приходит извне статикой <see cref="Resolve"/>, которую дёргает
    /// <see cref="UIConfirmationHandler"/> с кнопок открытого интерфейса. По ответу интерфейс закрывается.
    ///
    /// Один активный процесс покупки за раз (ShopController.IsBusy) ⇒ ровно одно ожидающее подтверждение,
    /// поэтому слот ожидания единственный и статический. Отмена процесса (<paramref name="ct"/>) закрывает
    /// UI и пробрасывает <see cref="OperationCanceledException"/>. Если UI забыли снабдить хэндлером —
    /// подтверждение висит до отмены процесса (ответственность дизайнера сцены).
    /// </summary>
    [Serializable]
    public class UIConfirmation : ConfirmationLogic
    {
        [SerializeField, DbRecord(typeof(UserInterfaceData), RecordTypes.Singleton),
         Tooltip("Интерфейс подтверждения — открывается на время ожидания вердикта и закрывается по ответу.")]
        private string ui;

        // Единственный ожидающий вердикт: активный процесс покупки всегда один.
        private static UniTaskCompletionSource<bool> _pending;

        /// <summary>
        /// Вердикт по текущему подтверждению: <c>true</c> — покупка продолжается, <c>false</c> — отменяется
        /// (NotStarted). Дёргается <see cref="UIConfirmationHandler"/> с кнопок UI. Нет ожидающего
        /// подтверждения — no-op.
        /// </summary>
        internal static void Resolve(bool confirmed) => _pending?.TrySetResult(confirmed);

        public override async UniTask<bool> Confirm(string guid, int count, CancellationToken ct)
        {
            UIProvider.Open(ui);
            _pending = new UniTaskCompletionSource<bool>();
            try
            {
                return await _pending.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                _pending = null;
                UIProvider.Close(ui);
            }
        }
    }
}
#endif

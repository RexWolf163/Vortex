using UnityEngine;
using Vortex.Sdk.RebindSystem.Bus;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Прерывание ожидания клавиши: <see cref="Cancel"/> вешается на кнопку «Отмена». Прерванный перехват ничего
    /// не меняет — слот остаётся как был.
    /// </summary>
    public class CaptureCancelHandler : MonoBehaviour
    {
        [SerializeField, Tooltip("Прерывать ожидание при выключении объекта — например, при закрытии панели " +
                                 "управления: иначе клапан продолжил бы ловить клавиши в игре.")]
        private bool cancelOnDisable = true;

        public void Cancel() => RebindBus.Controller.CancelSaving();

        private void OnDisable()
        {
            if (cancelOnDisable)
                Cancel();
        }
    }
}

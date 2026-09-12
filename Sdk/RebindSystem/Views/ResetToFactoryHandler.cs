using UnityEngine;
using Vortex.Sdk.RebindSystem.Bus;

namespace Vortex.Sdk.RebindSystem.Views
{
    /// <summary>
    /// Сброс всех изменений до заводских настроек, включая активность групп: <see cref="ResetAll"/> вешается на
    /// кнопку. Открытый перехват прерывается — иначе он дописал бы клавишу поверх сброса. Доступность кнопки —
    /// через <see cref="ChangesStateHandler"/>.
    /// </summary>
    public class ResetToFactoryHandler : MonoBehaviour
    {
        public void ResetAll()
        {
            RebindBus.Controller.CancelSaving();
            RebindBus.Controller.ResetAll();
        }
    }
}

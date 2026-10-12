using UnityEngine;
using UnityEngine.EventSystems;
using Vortex.Unity.UI.VirtualCursorSystem.Bus;
using Vortex.Unity.UI.VirtualCursorSystem.Controllers;

namespace Vortex.Unity.UI.VirtualCursorSystem.Handlers
{
    /// <summary>
    /// UGUI-зона hover: по входу/выходу указателя включает указанный скин для курсора.
    ///
    /// <b>Правило размещения: одна зона — один свой рейкаст.</b> Компонент вешается на объект
    /// со своим <c>Graphic</c> (<c>raycastTarget = true</c>), и ни одна зона не должна быть предком
    /// рейкаст-объекта другой зоны. UGUI шлёт <c>PointerEnter</c> не только цели рейкаста, но и её
    /// предкам по иерархии — зона-предок над чужим рейкастом получит Enter вместе с ним, и порядок
    /// их запросов скина становится неопределённым (некорректное размещение: две зоны на один рейкаст).
    /// Вложенные зоны («матрёшка») раскладываются сиблингами, каждая со своим рейкастом: тогда любой
    /// рейкаст адресует ровно одну зону, вход идёт от внешней границы к внутренней, а при выходе
    /// в соседний рейкаст скин зоны снимается.
    /// </summary>
    public class CursorHoverZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Имя скина, запрашиваемого при наведении. Пусто — зона ничего не меняет.</summary>
        [SerializeField] private string skinName;

        /// <summary>Вход указателя — запросить скин зоны.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(skinName)) return; // пусто = зона ничего не меняет (контракт Tooltip)
            VirtualCursorBus.Data.OnSkin(skinName, this);
        }

        /// <summary>Выход указателя — снять запрос скина зоны.</summary>
        public void OnPointerExit(PointerEventData eventData) => OffSkin();

        /// <summary>
        /// Снять свой запрос скина при выключении/уничтожении зоны под курсором: UGUI не шлёт Exit на
        /// деактивируемый объект, из-за чего скин залипал бы, пока курсор не войдёт в другую зону.
        /// </summary>
        private void OnDisable() => OffSkin();

        /// <summary>Снять запрос скина этой зоны в контроллере.</summary>
        private void OffSkin() => VirtualCursorBus.Data.OffSkin(this);
    }
}
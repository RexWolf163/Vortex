using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Vortex.Unity.UI.CursorSystem
{
    /// <summary>
    /// MonoBehaviour-маркер UI-зоны, при наведении на которую <see cref="CursorController"/>
    /// показывает hover-вариант курсора. Вешается на любой UGUI-объект с <c>RectTransform</c>
    /// и <c>Raycast Target</c>; вешать дополнительные коллайдеры не требуется.
    ///
    /// Поле <see cref="index"/> сериализованно через <c>[ValueDropdown]</c> — в инспекторе
    /// выпадает список спрайтов из активного <see cref="CursorSettings"/>, выбор по имени
    /// спрайта. Пункт «[NONE]» = <c>-1</c> отключает hover-смену для этой зоны.
    ///
    /// При выключении объекта (<see cref="OnDisable"/>) автоматически шлёт <see cref="CursorController.OnUnHover"/>,
    /// чтобы курсор не залип в hover-состоянии после скрытия зоны.
    /// </summary>
    public class MouseHoverListener : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// Индекс варианта курсора из <see cref="CursorPack.CursorOnHover"/>. <c>-1</c> = «без смены».
        /// Индекс общий для всех наборов разрешений — порядок ховеров должен совпадать во всех пакетах.
        /// </summary>
        [SerializeField, ValueDropdown("GetList")]
        private int index = -1;

        /// <summary>Курсор зашёл в зону — сообщаем контроллеру наш индекс.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            CursorController.OnHover(index);
        }

        /// <summary>Курсор покинул зону — снимаем наш индекс (контроллер игнорирует, если перехватил другой listener).</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            CursorController.OnUnHover(index);
        }

        /// <summary>
        /// При выключении компонента форсируем выход из hover-зоны: <c>OnPointerExit</c> от
        /// EventSystem не приходит автоматически, и курсор может застрять в hover-варианте.
        /// </summary>
        private void OnDisable()
        {
            OnPointerExit(null);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: формирует список значений для <c>[ValueDropdown]</c>. Подтягивает
        /// первый <see cref="CursorSettings"/>-ассет из <c>Resources</c> и составляет
        /// dropdown «имя спрайта → индекс» из самого крупного набора (индексы общие
        /// для всех разрешений). Если наборов нет — сбрасывает <see cref="index"/> в <c>-1</c>
        /// и оставляет только «[NONE]».
        /// </summary>
        private ValueDropdownList<int> GetList()
        {
            var result = new ValueDropdownList<int> { new ValueDropdownItem<int>("[NONE]", -1) };
            var settings = Resources.LoadAll<CursorSettings>("");
            if (settings == null || settings.Length == 0)
                return result;

            var packs = settings[0].CursorPacks;
            if (packs == null || packs.Length == 0)
            {
                index = -1;
                return result;
            }

            CursorPack largest = null;
            var largestMax = int.MinValue;
            foreach (var entry in packs)
            {
                if (entry?.Pack == null || entry.MaxScreenHeight <= largestMax)
                    continue;
                largestMax = entry.MaxScreenHeight;
                largest = entry.Pack;
            }

            var list = largest?.CursorOnHover;
            if (list == null || list.Length == 0)
            {
                index = -1;
                return result;
            }

            for (var i = 0; i < list.Length; i++)
            {
                var sprite = list[i];
                result.Add(new ValueDropdownItem<int>(sprite != null ? sprite.name : $"[EMPTY] {i}", i));
            }

            return result;
        }
#endif
    }
}
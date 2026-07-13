using System.Collections.Generic;
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
    /// Поле <see cref="key"/> сериализованно через <c>[ValueDropdown]</c> — в инспекторе
    /// выпадает объединённый список ключей (<see cref="CursorHoverEntry.Name"/>) из активного
    /// <see cref="CursorSettings"/>. Пункт «[NONE]» = пустой ключ отключает hover-смену для зоны.
    ///
    /// При выключении объекта (<see cref="OnDisable"/>) автоматически шлёт <see cref="CursorController.OnUnHover"/>,
    /// чтобы курсор не залип в hover-состоянии после скрытия зоны.
    /// </summary>
    public class MouseHoverListener : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// Ключ hover-варианта (<see cref="CursorHoverEntry.Name"/>). Пусто = «без смены».
        /// Ключ общий для всех наборов разрешений; резолвится по пакетам с фолбэком к более раннему.
        /// </summary>
        [SerializeField, ValueDropdown("GetList")]
        private string key = string.Empty;

        /// <summary>Курсор зашёл в зону — сообщаем контроллеру наш ключ.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            CursorController.OnHover(key);
        }

        /// <summary>Курсор покинул зону — снимаем наш ключ (контроллер игнорирует, если перехватил другой listener).</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            CursorController.OnUnHover(key);
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
        /// Editor-only: формирует список значений для <c>[ValueDropdown]</c>. Подтягивает первый
        /// <see cref="CursorSettings"/>-ассет из <c>Resources</c> и составляет dropdown как
        /// объединение уникальных ключей (<see cref="CursorHoverEntry.Name"/>) по всем наборам —
        /// у каждого пакета может быть свой набор. Если наборов нет — сбрасывает <see cref="key"/>
        /// в пусто и оставляет только «[NONE]».
        /// </summary>
        private ValueDropdownList<string> GetList()
        {
            var result = new ValueDropdownList<string> { new ValueDropdownItem<string>("[NONE]", string.Empty) };
            var settings = Resources.LoadAll<CursorSettings>("");
            if (settings == null || settings.Length == 0)
                return result;

            var packs = settings[0].CursorPacks;
            if (packs == null || packs.Length == 0)
            {
                key = string.Empty;
                return result;
            }

            var seen = new HashSet<string>();
            foreach (var entry in packs)
            {
                var arr = entry?.Pack?.CursorOnHover;
                if (arr == null)
                    continue;

                foreach (var hover in arr)
                {
                    if (hover == null || string.IsNullOrEmpty(hover.Name) || !seen.Add(hover.Name))
                        continue;
                    result.Add(new ValueDropdownItem<string>(hover.Name, hover.Name));
                }
            }

            return result;
        }
#endif
    }
}

using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Sdk.ContentTagsSystem.Bus;
using Vortex.Sdk.ContentTagsSystem.Model;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Sdk.ContentTagsSystem.Handlers
{
    /// <summary>
    /// Переключает <see cref="UIStateSwitcher"/> по тегам текущего издания: условие выполнено —
    /// <see cref="SwitcherState.On"/>, иначе <see cref="SwitcherState.Off"/>.
    ///
    /// Состояние вычисляется в <c>OnEnable</c> и больше не пересчитывается: набор активных тегов за сеанс
    /// не меняется.
    ///
    /// Вешать на один свитчер два управляющих компонента нельзя — победит отработавший последним.
    /// </summary>
    public class ContentTagGateHandler : MonoBehaviour
    {
        [SerializeField, ValueDropdown("@Vortex.Sdk.ContentTagsSystem.Model.ContentTagsCatalog.EditorKeys()")]
        [Tooltip("Теги, по которым решается показ.")]
        private string[] tags = new string[0];

        [SerializeField, Tooltip("Как сопоставлять список с активным набором.")]
        private TagMatchMode mode = TagMatchMode.All;

        [SerializeField, StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher switcher;

        /// <summary>Теги компонента. Для окна ревизии — редактор собирает по ним индекс.</summary>
        internal string[] EditorTags => tags;

        private void OnEnable()
        {
            if (switcher == null)
            {
                Debug.LogError($"[ContentTags] {name}: не назначен UIStateSwitcher.", this);
                return;
            }

            if (tags.Length == 0)
            {
                Debug.LogError($"[ContentTags] {name}: список тегов пуст — объект выключен.", this);
                switcher.Set(SwitcherState.Off);
                return;
            }

            switcher.Set(ContentBus.Matches(tags, mode) ? SwitcherState.On : SwitcherState.Off);
        }
    }
}

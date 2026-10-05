using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.ContentTagsSystem.Bus;
using Vortex.Sdk.ContentTagsSystem.Model;

namespace Vortex.Sdk.ContentTagsSystem.Handlers
{
    /// <summary>
    /// Включает и выключает целевые объекты по тегам текущего издания — там, где состояния свитчера не
    /// нужны и раздела просто не должно быть на сцене.
    ///
    /// Собственный объект в список целей не кладётся: выключив себя, компонент больше не получит
    /// <c>OnEnable</c> и обратно не включится.
    /// </summary>
    public class ContentTagObjectHandler : MonoBehaviour
    {
        [SerializeField, ValueDropdown("@Vortex.Sdk.ContentTagsSystem.Model.ContentTagsCatalog.EditorKeys()")]
        [Tooltip("Теги, по которым решается показ целей.")]
        private string[] tags = new string[0];

        [SerializeField, Tooltip("Как сопоставлять список с активным набором.")]
        private TagMatchMode mode = TagMatchMode.All;

        [SerializeField, Tooltip("Объекты, которыми управляет компонент. Себя сюда класть нельзя.")]
        private GameObject[] targets = new GameObject[0];

        [SerializeField, Tooltip("Показывать цели при невыполненном условии.")]
        private bool invert;

        /// <summary>Теги компонента. Для окна ревизии — редактор собирает по ним индекс.</summary>
        internal string[] EditorTags => tags;

        private void OnEnable()
        {
            if (targets.Length == 0)
            {
                Debug.LogError($"[ContentTags] {name}: список целей пуст.", this);
                return;
            }

            if (tags.Length == 0)
            {
                Debug.LogError($"[ContentTags] {name}: список тегов пуст — цели выключены.", this);
                SetTargets(false);
                return;
            }

            SetTargets(ContentBus.Matches(tags, mode) != invert);
        }

        private void SetTargets(bool active)
        {
            foreach (var target in targets)
            {
                if (target == null)
                    continue;

                if (target == gameObject)
                {
                    Debug.LogError($"[ContentTags] {name}: собственный объект в списке целей — " +
                                   "выключив себя, компонент не включится обратно. Цель пропущена.", this);
                    continue;
                }

                target.SetActive(active);
            }
        }
    }
}

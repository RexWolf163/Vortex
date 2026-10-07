using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.ContentTagsSystem.Model;

namespace Vortex.Sdk.ContentTagsSystem.Settings
{
    /// <summary>
    /// Конфигурация изданий: объявленные теги, бандлы и ключ активного издания. Держите ассет в папке
    /// Resources — шина грузит его оттуда при первом обращении.
    ///
    /// Перед сборкой издания меняется одно поле — <c>activeBundle</c>. Переопределения в редакторе нет:
    /// что записано в ассете, то и соберётся.
    /// </summary>
    [CreateAssetMenu(fileName = "ContentSettings", menuName = "Vortex/Settings/Content Tags")]
    public class ContentSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Объявленные теги проекта.")]
        private List<ContentTag> tags = new();

        [SerializeField, Tooltip("Издания сборки и их состав.")]
        private List<ContentBundle> bundles = new();

        [SerializeField, ValueDropdown("$BundleKeys")]
        [Tooltip("Издание текущей сборки. Пусто или неизвестный ключ — всё размеченное выключено.")]
        private string activeBundle;

        public IReadOnlyList<ContentTag> Tags => tags;

        public IReadOnlyList<ContentBundle> Bundles => bundles;

        public string ActiveBundle => activeBundle;

        /// <summary>
        /// Проблемы конфигурации: пустые и повторяющиеся ключи, теги бандлов без объявления, активное
        /// издание без бандла.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            var declared = new HashSet<string>();

            for (var i = 0; i < tags.Count; i++)
            {
                var tag = tags[i];
                if (tag == null || string.IsNullOrEmpty(tag.Key))
                {
                    problems.Add($"Теги [{i}]: не задан ключ.");
                    continue;
                }

                if (!declared.Add(tag.Key))
                    problems.Add($"Теги [{i}]: ключ «{tag.Key}» уже объявлен.");
            }

            var bundleKeys = new HashSet<string>();
            for (var i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                if (bundle == null || string.IsNullOrEmpty(bundle.Key))
                {
                    problems.Add($"Бандлы [{i}]: не задан ключ.");
                    continue;
                }

                if (!bundleKeys.Add(bundle.Key))
                    problems.Add($"Бандлы [{i}]: ключ «{bundle.Key}» уже занят.");

                foreach (var tag in bundle.Tags)
                    if (!declared.Contains(tag))
                        problems.Add($"Бандл «{bundle.Key}»: тег «{tag}» не объявлен в списке тегов.");
            }

            if (string.IsNullOrEmpty(activeBundle))
                problems.Add("Активное издание не выбрано — всё размеченное будет выключено.");
            else if (!bundleKeys.Contains(activeBundle))
                problems.Add($"Активное издание «{activeBundle}» не соответствует ни одному бандлу.");

            return problems;
        }

        private void OnValidate()
        {
            foreach (var problem in Validate())
                Debug.LogWarning($"[ContentTags] {problem}", this);
        }

#if UNITY_EDITOR
        // Источник выпадашки активного издания. Метод существует только в редакторе: в билде инспектора нет.
        private IEnumerable<string> BundleKeys()
        {
            foreach (var bundle in bundles)
                if (bundle != null && !string.IsNullOrEmpty(bundle.Key))
                    yield return bundle.Key;
        }
#endif
    }
}

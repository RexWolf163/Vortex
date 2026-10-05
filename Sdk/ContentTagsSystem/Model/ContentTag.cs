using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.ContentTagsSystem.Model
{
    /// <summary>
    /// Объявление тега: метка контента, существующая в проекте. Сам по себе тег ничего не включает —
    /// включённым его делает бандл активного издания.
    /// </summary>
    [Serializable, ClassLabel("$key")]
    public class ContentTag
    {
        [SerializeField, Tooltip("Ключ тега. По нему идут все сопоставления.")]
        private string key;

        [SerializeField, Multiline, Tooltip("Для кого и зачем тег заведён. В логике не участвует.")]
        private string description;

        public string Key => key;

        /// <summary>Пояснение. Наружу сборки не выходит: поле справочное, решений по нему не принимают.</summary>
        internal string Description => description;
    }
}

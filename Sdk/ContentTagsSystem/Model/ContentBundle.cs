using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.ContentTagsSystem.Model
{
    /// <summary>
    /// Издание сборки: белый список тегов, включённых в нём. Теги, не перечисленные здесь, в этом
    /// издании выключены; контент без тегов виден всегда.
    /// </summary>
    [Serializable, ClassLabel("$key")]
    public class ContentBundle
    {
        [SerializeField, Tooltip("Имя издания: steam, gog, publisher_x, demo.")]
        private string key;

        [SerializeField, ValueDropdown("@Vortex.Sdk.ContentTagsSystem.Model.ContentTagsCatalog.EditorKeys()")]
        [Tooltip("Ключи включённых тегов.")]
        private List<string> tags = new();

        public string Key => key;

        public IReadOnlyList<string> Tags => tags;
    }
}

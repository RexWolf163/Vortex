using System;
using UnityEngine;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.GallerySystem.View
{
    /// <summary>
    /// Точечный оверрайд превью-иконки на конкретной сцене галлереи —
    /// например, секретная/сезонная карточка получает другое превью
    /// без правки базовой модели.
    /// </summary>
    [Serializable]
    public class IconOverride
    {
        [SerializeField, DbRecord] private string guid;
        [SerializeField] private Sprite sprite;

        public string Guid => guid;
        public Sprite Sprite => sprite;
    }
}

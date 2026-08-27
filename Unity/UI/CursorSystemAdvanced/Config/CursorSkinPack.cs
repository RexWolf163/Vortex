using System;
using UnityEngine;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// Набор скинов одного тира разрешения: базовый скин (вне hover) + hover-варианты по строковому ключу.
    /// </summary>
    [Serializable]
    public class CursorSkinPack
    {
        [SerializeField, Tooltip("Базовый скин вне hover-зон.")]
        private CursorSkin baseSkin = new();

        [SerializeField, Tooltip("Hover-варианты по ключу (CursorSkin.Name).")]
        private CursorSkin[] hoverSkins = Array.Empty<CursorSkin>();

        public CursorSkin Base => baseSkin;

        /// <summary>Hover-скин по ключу; null, если ключ пуст или не найден.</summary>
        public CursorSkin FindHover(string key)
        {
            if (string.IsNullOrEmpty(key) || hoverSkins == null)
                return null;
            foreach (var s in hoverSkins)
                if (s != null && s.Name == key)
                    return s;
            return null;
        }
    }
}

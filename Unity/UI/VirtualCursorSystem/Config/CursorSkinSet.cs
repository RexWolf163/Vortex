using System;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.VirtualCursorSystem
{
    /// <summary>
    /// Сменный набор (тема) курсора: ключ выбора + паки по тирам разрешения. Индекс пака совпадает
    /// с индексом в <see cref="CursorSkinSettings.ResolutionTiers"/> (выравнивание стережёт OnValidate).
    /// </summary>
    [Serializable, ClassLabel("$Label")]
    public class CursorSkinSet
    {
        [SerializeField, Tooltip("Ключ темы (для выбора/покупки).")]
        private string key;

        [SerializeField, Tooltip("Паки по тирам разрешения (по одному на ResolutionTiers[i]).")]
        private CursorSkinPack[] tiers = Array.Empty<CursorSkinPack>();

        public string Key => key;
        public CursorSkinPack[] Tiers => tiers;

        /// <summary>Пак под индекс тира с клампом к диапазону массива; null, если паков нет.</summary>
        public CursorSkinPack GetTier(int tierIndex)
        {
            if (tiers == null || tiers.Length == 0)
                return null;
            var i = Mathf.Clamp(tierIndex, 0, tiers.Length - 1);
            return tiers[i];
        }

#if UNITY_EDITOR
        private string Label() => string.IsNullOrEmpty(key) ? "[No Key]" : key;
#endif
    }
}

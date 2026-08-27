using System;
using UnityEngine;

namespace Vortex.Unity.UI.CursorSystemAdvanced
{
    /// <summary>
    /// SO-каталог тем курсора + ГЛОБАЛЬНЫЕ тиры разрешения. Тиры — единый источник брейкпоинтов
    /// (по возрастанию), каждая тема даёт по одному паку на тир. Выбор темы в рантайме —
    /// <see cref="CursorSkinSelector"/>. Загружается бутстрапом пакета и передаётся в
    /// <see cref="VirtualCursorController.Init"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "CursorSkinSettings", menuName = "Vortex/UI/Cursor Skin Settings")]
    public class CursorSkinSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Брейкпоинты по Screen.height (по возрастанию). Единый источник для всех тем.")]
        private int[] resolutionTiers = { 1080, int.MaxValue };

        [SerializeField, Tooltip("Ключ темы по умолчанию (если выбранная не задана/не найдена).")]
        private string defaultSetKey;

        [SerializeField, Tooltip("Каталог тем курсора.")]
        private CursorSkinSet[] sets = Array.Empty<CursorSkinSet>();

        public string DefaultSetKey => defaultSetKey;
        public int[] ResolutionTiers => resolutionTiers;
        public CursorSkinSet[] Sets => sets;

        /// <summary>Тема по ключу; дефолтная, если ключ пуст/не найден; первая — если и дефолт не найден.</summary>
        public CursorSkinSet GetSet(string key)
        {
            CursorSkinSet first = null;
            CursorSkinSet def = null;
            foreach (var s in sets)
            {
                if (s == null) continue;
                first ??= s;
                if (!string.IsNullOrEmpty(key) && s.Key == key)
                    return s;
                if (s.Key == defaultSetKey)
                    def = s;
            }
            return def ?? first;
        }

        /// <summary>Индекс тира под текущее разрешение: минимальный порог >= height, иначе крупнейший.</summary>
        public int SelectTierIndex(int screenHeight)
        {
            if (resolutionTiers == null || resolutionTiers.Length == 0)
                return 0;

            var bestIndex = -1;
            var bestMax = int.MaxValue;
            var largestIndex = 0;
            var largestMax = int.MinValue;

            for (var i = 0; i < resolutionTiers.Length; i++)
            {
                var t = resolutionTiers[i];
                if (t >= screenHeight && t < bestMax) { bestMax = t; bestIndex = i; }
                if (t > largestMax) { largestMax = t; largestIndex = i; }
            }

            return bestIndex >= 0 ? bestIndex : largestIndex;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var tierCount = resolutionTiers?.Length ?? 0;
            if (tierCount == 0)
                return;

            for (var i = 1; i < tierCount; i++)
                if (resolutionTiers[i] < resolutionTiers[i - 1])
                {
                    Debug.LogWarning("[CursorSkinSettings] resolutionTiers должны идти по возрастанию.", this);
                    break;
                }

            if (sets == null)
                return;
            foreach (var set in sets)
            {
                if (set?.Tiers == null) continue;
                if (set.Tiers.Length != tierCount)
                    Debug.LogWarning(
                        $"[CursorSkinSettings] Тема '{set.Key}': паков {set.Tiers.Length}, тиров {tierCount} — выровняй по тирам.",
                        this);
            }
        }
#endif
    }
}

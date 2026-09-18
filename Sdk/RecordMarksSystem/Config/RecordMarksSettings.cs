using UnityEngine;
using Vortex.Unity.CoreAssetsSystem;

namespace Vortex.Sdk.RecordMarksSystem.Config
{
    /// <summary>
    /// Декларативный список известных меток пакета <c>RecordMarksSystem</c> и их
    /// принадлежности к каналу сохранения. Единственная точка правды для валидации и bootstrap'а bus'а.
    ///
    /// Метки в <see cref="slotMarks"/> сохраняются вместе со слотом (<c>SaveController</c>) и
    /// сбрасываются при новой игре. Метки в <see cref="globalMarks"/> живут в
    /// <c>GlobalSaveController</c> — переживают слоты и рестарты.
    ///
    /// Списки не пересекаются, дубликатов не должно быть, пустых имён быть не должно (см. <c>OnValidate</c>).
    ///
    /// Реализует <see cref="ICoreAsset"/> — единственный экземпляр создаётся автоматически в
    /// <c>Assets/Resources/Settings/</c> контроллером Core Assets (<c>Tools → Vortex → Debug →
    /// Check Core Assets</c>, либо при включённом авто-режиме — на перезагрузке домена), поэтому
    /// bootstrap bus'а всегда находит ассет. Ручное создание через
    /// <c>Create → Vortex → Settings → RecordMarks</c> тоже доступно.
    /// </summary>
    [CreateAssetMenu(fileName = "RecordMarksSettings", menuName = "Vortex/Settings/RecordMarks")]
    public class RecordMarksSettings : ScriptableObject, ICoreAsset
    {
        /// <summary>
        /// Идентификаторы меток, сохраняемых per-slot. Рекомендуемая форма имени —
        /// дот-неймспейс: <c>"gallery.unlocked"</c>, <c>"codex.viewed"</c>.
        /// </summary>
        [SerializeField, Tooltip("Метки, сохраняемые в слоте через SaveController.")]
        private string[] slotMarks = new string[0];

        /// <summary>
        /// Идентификаторы меток, сохраняемых в глобальном хранилище
        /// (<c>GlobalSaveController</c>) — переживают слоты.
        /// </summary>
        [SerializeField, Tooltip("Метки, сохраняемые глобально через GlobalSaveController.")]
        private string[] globalMarks = new string[0];

        /// <summary>Имена меток per-slot. Read-only проекция.</summary>
        public string[] SlotMarks => slotMarks;

        /// <summary>Имена меток per-account. Read-only проекция.</summary>
        public string[] GlobalMarks => globalMarks;

#if UNITY_EDITOR
        private void OnValidate()
        {
            WarnEmptyOrDuplicate(slotMarks, nameof(slotMarks));
            WarnEmptyOrDuplicate(globalMarks, nameof(globalMarks));
            WarnCrossOverlap();
        }

        private void WarnEmptyOrDuplicate(string[] list, string listName)
        {
            if (list == null) return;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var mark in list)
            {
                if (string.IsNullOrWhiteSpace(mark))
                {
                    Debug.LogWarning($"[RecordMarksSettings] Пустое имя метки в '{listName}'.", this);
                    continue;
                }
                if (!seen.Add(mark))
                    Debug.LogWarning($"[RecordMarksSettings] Дубликат метки '{mark}' в '{listName}'.", this);
            }
        }

        private void WarnCrossOverlap()
        {
            if (slotMarks == null || globalMarks == null) return;
            var slot = new System.Collections.Generic.HashSet<string>(slotMarks);
            foreach (var mark in globalMarks)
                if (!string.IsNullOrWhiteSpace(mark) && slot.Contains(mark))
                    Debug.LogWarning(
                        $"[RecordMarksSettings] Метка '{mark}' присутствует и в slot, и в global. " +
                        "Одна метка может жить только в одном хранилище.", this);
        }
#endif
    }
}

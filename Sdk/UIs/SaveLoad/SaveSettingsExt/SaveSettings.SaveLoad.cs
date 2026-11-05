#if USING_VORTEX_SAVE_LOAD_WRAPPER
using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.SaveSystem.Presets
{
    /// <summary>
    /// Переключатели метаблоков пакета SaveLoad: превью (<c>SavePreviewController</c>) и тип сейва
    /// (<c>SaveKindController</c>).
    /// </summary>
    public partial class SaveSettings
    {
        /// <summary>Ключ метаблока превью сейва</summary>
        public const string PreviewMetaKey = "preview";

        /// <summary>Ключ метаблока типа сейва</summary>
        public const string KindMetaKey = "kind";

        [BoxGroup(MetaGroup, order: MetaGroupOrder)]
        [SerializeField, SaveMetaBlock(PreviewMetaKey)]
        [Tooltip("Превью — снимок экрана на начале сохранения.")]
        private bool previewMeta = true;

        [BoxGroup(MetaGroup, order: MetaGroupOrder)]
        [SerializeField, SaveMetaBlock(KindMetaKey)]
        [Tooltip("Тип сейва — автосейв / ручной.")]
        private bool kindMeta = true;
    }
}
#endif

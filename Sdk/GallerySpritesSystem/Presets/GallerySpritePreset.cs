using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Sdk.GallerySpritesSystem.Models;
using Vortex.Unity.DatabaseSystem.Presets;

namespace Vortex.Sdk.GallerySpritesSystem.Presets
{
    /// <summary>
    /// Пресет художественной карточки-спрайта галлереи. Наследует базовые поля
    /// <c>RecordPreset&lt;T&gt;</c>: Guid, Name, Description, Icon (превью).
    ///
    /// Собственное поле — полиморфный <see cref="AssetHandle{T}"/> для fullscreen-ассета.
    /// Дизайнер выбирает конкретную реализацию (`DirectAssetHandle` или
    /// `AddressableAssetHandle`) через Odin `[SerializeReference]`-picker.
    /// </summary>
    [CreateAssetMenu(fileName = "GallerySpritePreset",
                     menuName = "Database/Gallery/Sprite")]
    public class GallerySpritePreset : RecordPreset<GallerySpriteModel>
    {
        [SerializeReference] private AssetHandle<Sprite> fullscreen;

        /// <summary>
        /// Read-only accessor для <see cref="GallerySpriteModel.CopyFrom"/>: модель делает
        /// deep-clone этого handle-шаблона, чтобы runtime-инстанс имел своё состояние загрузки
        /// (<c>_cached</c>) и не разделял его с пресетом.
        /// </summary>
        public AssetHandle<Sprite> FullscreenTemplate => fullscreen;
    }
}

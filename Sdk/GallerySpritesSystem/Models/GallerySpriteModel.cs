using System.Threading;
using UnityEngine;
using Vortex.Core.AssetHandleSystem;
using Vortex.Core.DatabaseSystem.Model;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Sdk.GallerySpritesSystem.Bus;
using Vortex.Sdk.GallerySpritesSystem.Presets;
using Vortex.Sdk.GallerySystem;
using Vortex.Unity.Extensions.Abstractions;

namespace Vortex.Sdk.GallerySpritesSystem.Models
{
    /// <summary>
    /// Модель художественной карточки-спрайта галлереи. Реализация <see cref="IGalleryEntry"/>
    /// для GallerySystem — попадает в галлерейный пул через <c>GalleryView</c> и стандартный
    /// <c>Database.GetRecords(typeof(IGalleryEntry))</c>.
    ///
    /// Fullscreen-ассет обёрнут в <see cref="AssetHandle{T}"/>: дизайнер per-preset выбирает
    /// <c>DirectAssetHandle&lt;Sprite&gt;</c> или <c>AddressableAssetHandle&lt;Sprite&gt;</c>.
    /// Handle-инстанс модели — независимый deep-clone от инстанса пресета (см. <see cref="CopyFrom"/>).
    ///
    /// Публичный доступ к handle — только через extension-контроллер
    /// <c>GallerySpriteController</c> (в той же сборке, видит internal). Внешние потребители
    /// работают с моделью как с обычным IGalleryEntry.
    /// </summary>
    public class GallerySpriteModel : Record, IGalleryEntry
    {
        /// <summary>Иконка-превью в галлерее (заполняется из <see cref="Presets.GallerySpritePreset"/> базовым CopyFrom).</summary>
        public Sprite Icon { get; protected set; }

        /// <summary>
        /// Fullscreen-ассет карточки. Owner-lock: internal — доступ только внутри сборки пакета.
        /// Deep-clone-инстанс от пресета, полученный в <see cref="CopyFrom"/>. Мутируется только
        /// зарегистрированным viewer'ом через <c>GallerySpriteController</c>.
        /// </summary>
        internal AssetHandle<Sprite> Fullscreen { get; set; }

        /// <summary>
        /// Кастомный <c>CopyFrom</c>: базовое <see cref="ObjectExtCopy.CopyFrom"/> копирует public
        /// property (Guid, Name, Description, Icon) через reflection. <see cref="Fullscreen"/>
        /// — internal, public reflection его не видит; поэтому deep-clone делаем явно из
        /// <see cref="GallerySpritePreset.FullscreenTemplate"/>. Instance-метод перекрывает
        /// одноимённый extension по правилу разрешения C# при вызове <c>record.CopyFrom(preset)</c>
        /// из <c>RecordPreset&lt;T&gt;.GetData()</c>.
        /// </summary>
        public bool CopyFrom(SoData source)
        {
            var ok = ObjectExtCopy.CopyFrom(this, source);
            if (source is GallerySpritePreset preset)
                Fullscreen = preset.FullscreenTemplate.DeepCopy();
            return ok;
        }

        /// <summary>Каталог статичен, в тело сейва не входит. Статус «разблокирована» живёт в RecordMarksSystem.</summary>
        public override string GetDataForSave() => null;

        /// <summary>Каталог статичен — восстанавливать нечего.</summary>
        public override void LoadFromSaveData(string data)
        {
        }

        /// <summary>IGalleryEntry.Show — синхронный. Внутри — только зов bus.</summary>
        public void Show() => Show(CancellationToken.None);

        /// <summary>Overload с CancellationToken — для потребителей со своим токеном отмены.</summary>
        public void Show(CancellationToken ct) => GallerySpritesBus.RequestShow(this, ct);

        /// <summary>IGalleryEntry.Hide — синхронный. Внешний триггер закрытия fullscreen'а.</summary>
        public void Hide() => GallerySpritesBus.RequestHide(this);
    }
}

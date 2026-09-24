using UnityEngine;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.GallerySystem.View
{
    /// <summary>
    /// Виджет карточки в пуле <c>GalleryView</c>. Читает из связанного <see cref="IDataStorage"/>
    /// (обычно <c>PoolItem</c>) пять компонентов, разложенных <c>GalleryView</c>: превью-Sprite,
    /// <see cref="BoolData"/> isLocked, общий <see cref="StringData"/> selectedGuid,
    /// <see cref="IGalleryEntry"/> entry и <see cref="GalleryPoolCallbacks"/> callbacks.
    ///
    /// Управляет двумя <see cref="TweenerHub"/>-ами:
    /// <list type="bullet">
    ///   <item><c>lockedTweener</c> — Forward при <c>isLocked=true</c>, Back при false.</item>
    ///   <item><c>selectedTweener</c> — Forward когда <c>selectedGuid.Value == entry.GuidPreset</c>,
    ///   Back — когда не совпадает.</item>
    /// </list>
    ///
    /// Публичный <see cref="Show"/> — точка привязки для кнопки «Смотреть» на префабе:
    /// зовёт <c>entry.Show()</c> и уведомляет обрамляющий UI через <c>callbacks.OnShow</c>.
    /// </summary>
    public class GalleryCardView : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour storage;

        [SerializeField, AutoLink] private UIComponent preview;

        [Header("Tweeners")] [SerializeField, AutoLink]
        private TweenerHub lockedTweener;

        [SerializeField, AutoLink] private TweenerHub selectedTweener;

        private IDataStorage _source;
        private BoolData _isLocked;
        private StringData _selectedGuid;
        private IGalleryEntry _entry;
        private GalleryPoolCallbacks _callbacks;

        private void OnEnable()
        {
            _source ??= storage as IDataStorage;
            if (_source == null)
            {
                Debug.LogError($"[GalleryCardView] '{name}': storage is not IDataStorage.", this);
                return;
            }

            _source.OnUpdateLink += Relink;
            Relink();
        }

        private void OnDisable()
        {
            if (_source != null)
                _source.OnUpdateLink -= Relink;
            Unsubscribe();
        }

        private void Relink()
        {
            // Полный ре-подписочный цикл: старые ссылки могут указывать на данные предыдущего entry
            // (тот же PoolItem переиспользуется под другую карточку) — отписываемся и переподписываемся.
            Unsubscribe();

            var sprite = _source.GetData<Sprite>();
            _isLocked = _source.GetData<BoolData>();
            _selectedGuid = _source.GetData<StringData>();
            _entry = _source.GetData<IGalleryEntry>();
            _callbacks = _source.GetData<GalleryPoolCallbacks>();

            if (preview != null)
                preview.SetSprite(sprite);

            if (_isLocked != null)
            {
                _isLocked.OnUpdateData += ApplyLocked;
                ApplyLocked();
            }

            if (_selectedGuid != null)
            {
                _selectedGuid.OnUpdateData += ApplySelected;
                ApplySelected();
            }
        }

        private void Unsubscribe()
        {
            if (_isLocked != null)
            {
                _isLocked.OnUpdateData -= ApplyLocked;
                _isLocked = null;
            }

            if (_selectedGuid != null)
            {
                _selectedGuid.OnUpdateData -= ApplySelected;
                _selectedGuid = null;
            }
        }

        private void ApplyLocked()
        {
            if (lockedTweener == null || _isLocked == null) return;
            if (_isLocked.Value) lockedTweener.Forward();
            else lockedTweener.Back();
        }

        private void ApplySelected()
        {
            if (selectedTweener == null || _selectedGuid == null || _entry == null) return;
            if (_selectedGuid.Value == _entry.GuidPreset) selectedTweener.Forward();
            else selectedTweener.Back();
        }

        /// <summary>
        /// Показать содержимое карточки. Синхронно вызывает <c>entry.Show()</c> и
        /// уведомляет <c>GalleryView</c> через <c>callbacks.OnShow</c>. Подвешивается
        /// на UI-обработчик кнопки «Смотреть» в префабе.
        /// </summary>
        public void Show()
        {
            _entry?.Show();
            _callbacks?.OnShow?.Invoke();
        }

        /// <summary>
        /// Сигнал фокуса от префаба. Уведомляет <c>GalleryView</c>
        /// через <c>callbacks.OnFocus</c>: тот обновит общий <c>selectedGuid</c> и статик
        /// <c>LastViewedGuid</c>, реактив триггернет <see cref="ApplySelected"/> на этой
        /// и остальных карточках. 
        /// </summary>
        public void SetFocus()
        {
            _callbacks?.OnFocus?.Invoke();
        }
    }
}
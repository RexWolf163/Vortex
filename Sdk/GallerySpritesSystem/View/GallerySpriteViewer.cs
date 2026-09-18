using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.System.Abstractions;
using Vortex.Sdk.GallerySpritesSystem.Bus;
using Vortex.Sdk.GallerySpritesSystem.Controllers;
using Vortex.Sdk.GallerySpritesSystem.Models;
using Vortex.Unity.UI.TweenerSystem;

namespace Vortex.Sdk.GallerySpritesSystem.View
{
    /// <summary>
    /// Готовый viewer спрайтовых карточек. Регистрируется в <see cref="GallerySpritesBus"/> на <c>Awake</c>
    /// (по дизайну — один viewer на проект; снимает регистрацию на <c>OnDestroy</c>) и обслуживает Show/Hide:
    /// грузит fullscreen-ассет модели через <see cref="GallerySpriteController"/>, кладёт спрайт в
    /// <see cref="fullscreenImage"/> и запускает показ твинером <see cref="showTweener"/> (Forward — открыть,
    /// Back — закрыть).
    ///
    /// Реализует <see cref="IDataStorage"/>: отдаёт текущую показываемую модель по типу
    /// (<c>GallerySpriteModel</c> / <c>IGalleryEntry</c>) — дочерние UI-виджеты панели fullscreen'а
    /// читают её через <c>GetData&lt;T&gt;()</c> и переподцепляются по <see cref="OnUpdateLink"/>.
    ///
    /// Смена показываемой карточки отменяет незавершённую загрузку прошлой и отпускает её ассет; при
    /// уничтожении viewer'а загрузка отменяется, текущий ассет отпускается.
    /// </summary>
    public class GallerySpriteViewer : MonoBehaviour, IDataStorage
    {
        [SerializeField, Tooltip("Image, куда кладётся fullscreen-спрайт показываемой карточки.")]
        private Image fullscreenImage;

        [SerializeField, Tooltip("Твинер показа: Forward — открыть панель (после загрузки спрайта), Back — закрыть.")]
        private TweenerHub showTweener;

        private const string LoggerCategory = "GallerySpriteViewer";

        private GallerySpriteModel _current;
        private CancellationTokenSource _cts;

        /// <summary>
        /// IDataSource: served-модель сменилась — потребителю перезапросить <see cref="GetData{T}"/>
        /// и, если нужно, переподписаться на её события.
        /// </summary>
        public event Action OnUpdateLink;

        private void Awake() => GallerySpritesBus.Register(HandleShow, HandleHide);

        private void OnDestroy()
        {
            GallerySpritesBus.Unregister();
            CancelLoad();
            _current?.ReleaseFullscreen();
        }

        /// <summary>
        /// IDataStorage: текущая показываемая модель по типу. До первого Show и после Hide — <c>null</c>.
        /// </summary>
        public T GetData<T>() where T : class => _current as T;

        // ── Обработчики шины ──────────────────────────────────────────────────

        private void HandleShow(GallerySpriteModel model, CancellationToken external)
        {
            // Смена показываемой карточки: отменяем незавершённую загрузку и отпускаем прошлый ассет.
            CancelLoad();
            if (_current != null && _current != model)
                _current.ReleaseFullscreen();

            _current = model;
            OnUpdateLink?.Invoke();
            ShowAsync(external).Forget();
        }

        private async UniTaskVoid ShowAsync(CancellationToken external)
        {
            var model = _current;
            // Свой CTS: отмена при смене карточки, Hide или уничтожении viewer'а.
            _cts = CancellationTokenSource.CreateLinkedTokenSource(external, this.GetCancellationTokenOnDestroy());
            try
            {
                var sprite = await model.LoadFullscreenAsync(_cts.Token);
                if (fullscreenImage != null)
                    fullscreenImage.sprite = sprite;
                if (showTweener != null)
                    showTweener.Forward();
            }
            catch (OperationCanceledException)
            {
                // Смена карточки / Hide / destroy до конца загрузки — штатно, показ не начинаем.
            }
            catch (Exception e)
            {
                // Провал загрузки ассета (нет бандла/адреса, битый reference): панель не открываем,
                // логируем с контекстом карточки, а не отдаём сбой в unobserved-exception без адресата.
                Log.Print(LogLevel.Error,
                    $"Fullscreen load failed for '{model?.Name}': {e.Message}", LoggerCategory);
            }
        }

        private void HandleHide(GallerySpriteModel model)
        {
            CancelLoad();
            if (showTweener != null)
                showTweener.Back();

            // Ассет отпускаем сразу. Direct — no-op; для Addressable панель уже уходит по Back. Если критично
            // не гасить спрайт до конца анимации закрытия — отпуск можно отложить в OnComplete твинера.
            model?.ReleaseFullscreen();

            if (_current == model)
            {
                _current = null;
                OnUpdateLink?.Invoke();
            }
        }

        private void CancelLoad()
        {
            if (_cts == null)
                return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}

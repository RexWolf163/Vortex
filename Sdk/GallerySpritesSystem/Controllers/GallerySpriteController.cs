using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vortex.Sdk.GallerySpritesSystem.Models;

namespace Vortex.Sdk.GallerySpritesSystem.Controllers
{
    /// <summary>
    /// Extension-контроллер публичного API для viewer'а. Живёт в той же сборке, что и
    /// <see cref="GallerySpriteModel"/> — видит internal <c>Fullscreen</c>. Инкапсулирует
    /// handle-lifecycle: viewer работает через методы модели (`await model.LoadFullscreenAsync()`,
    /// `model.ReleaseFullscreen()`), сам handle-инстанс не покидает сборку пакета — single-holder
    /// гарантирован на уровне видимости типа.
    /// </summary>
    public static class GallerySpriteController
    {
        /// <summary>
        /// Асинхронно загрузить fullscreen-ассет. Единственная публичная точка Load для viewer'а.
        /// </summary>
        public static UniTask<Sprite> LoadFullscreenAsync(this GallerySpriteModel model, CancellationToken ct)
            => model.Fullscreen.LoadAsync(ct);

        /// <summary>Перегрузка без CancellationToken.</summary>
        public static UniTask<Sprite> LoadFullscreenAsync(this GallerySpriteModel model)
            => model.Fullscreen.LoadAsync();

        /// <summary>Освободить fullscreen-ассет. Единственная публичная точка Release для viewer'а.</summary>
        public static void ReleaseFullscreen(this GallerySpriteModel model)
            => model.Fullscreen.Release();
    }
}

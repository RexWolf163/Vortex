using System;
using System.Threading;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Sdk.GallerySpritesSystem.Models;

namespace Vortex.Sdk.GallerySpritesSystem.Bus
{
    /// <summary>
    /// Статическая шина показа/скрытия спрайтовых галлерейных карточек. Работает как
    /// registry-паттерн: **один** активный viewer на проект. Регистрация — через
    /// <see cref="Register"/> из <c>Awake</c> реализации viewer'а, снятие — через
    /// <see cref="Unregister"/> в <c>OnDestroy</c>.
    ///
    /// Модель ничего не знает про viewer — только вызывает <see cref="RequestShow"/>/
    /// <see cref="RequestHide"/>. Bus доставляет команды зарегистрированному handler'у.
    ///
    /// Отсутствие зарегистрированного viewer'а не является ошибкой (например, сцена
    /// галлереи ещё не открыта): команда логгируется как warning и теряется.
    /// </summary>
    public static class GallerySpritesBus
    {
        private const string LoggerCategory = "GallerySpritesBus";

        private static Action<GallerySpriteModel, CancellationToken> _showHandler;
        private static Action<GallerySpriteModel> _hideHandler;

        /// <summary>Есть ли активный зарегистрированный viewer.</summary>
        public static bool HasViewer => _showHandler != null;

        /// <summary>
        /// Зарегистрировать пару обработчиков viewer'а (по одному вызову на весь проект).
        /// Повторная регистрация при живом viewer'e — LogError + отказ (первый остаётся активным).
        /// </summary>
        public static void Register(
            Action<GallerySpriteModel, CancellationToken> showHandler,
            Action<GallerySpriteModel> hideHandler)
        {
            if (_showHandler != null)
            {
                Log.Print(LogLevel.Error,
                    "Register: viewer already registered. Second registration ignored.",
                    LoggerCategory);
                return;
            }

            _showHandler = showHandler;
            _hideHandler = hideHandler;
        }

        /// <summary>Снять регистрацию (из <c>OnDestroy</c> viewer'а). Идемпотентно.</summary>
        public static void Unregister()
        {
            _showHandler = null;
            _hideHandler = null;
        }

        /// <summary>Запросить показ карточки. Обычно вызывается моделью через <c>Show()</c>.</summary>
        public static void RequestShow(GallerySpriteModel model, CancellationToken ct)
        {
            if (_showHandler == null)
            {
                Log.Print(LogLevel.Warning,
                    $"RequestShow('{model?.Name}'): no viewer registered. Command lost.",
                    LoggerCategory);
                return;
            }

            _showHandler(model, ct);
        }

        /// <summary>Запросить скрытие карточки (внешний триггер закрытия fullscreen'а).</summary>
        public static void RequestHide(GallerySpriteModel model)
        {
            if (_hideHandler == null)
            {
                Log.Print(LogLevel.Warning,
                    $"RequestHide('{model?.Name}'): no viewer registered.",
                    LoggerCategory);
                return;
            }

            _hideHandler(model);
        }
    }
}

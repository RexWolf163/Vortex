using System.Collections.Generic;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.VideoSystem
{
    /// <summary>
    /// Контроллер управления разрешением и параметрами экрана
    /// 
    /// </summary>
    public class VideoController : SystemController<VideoController, IVideoDriver>
    {
        /// <summary>
        /// Список доступных разрешений
        /// </summary>
        private static readonly List<string> AvailableResolutions = new();

        /// <summary>
        /// Список доступных режимов отображения для устройства
        /// </summary>
        private static readonly List<string> AvailableScreenModes = new();

        /// <summary>
        /// Выводит полный перечень доступных разрешений для устройства
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyList<string> GetResolutionsList() => AvailableResolutions;

        /// <summary>
        /// Выводит полный перечень доступных режимов для устройства
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyList<string> GetScreenModes() => AvailableScreenModes;

        /// <summary>
        /// Возвращает текущую настройку разрешения
        /// </summary>
        /// <returns></returns>
        public static string GetResolution() => Driver.GetResolution();

        /// <summary>
        /// Устанавливает новое разрешение для устройства
        /// </summary>
        /// <param name="resolution"></param>
        public static void SetResolution(string resolution) => Driver.SetResolution(resolution);

        /// <summary>
        /// Задает режим экрана устройства
        /// </summary>
        /// <param name="screenMode"></param>
        public static void SetScreenMode(string screenMode) => Driver.SetScreenMode(screenMode);

        /// <summary>
        /// Возвращает режим экрана устройства
        /// </summary>
        /// <returns></returns>
        public static string GetScreenMode() => Driver.GetScreenMode();

        protected override void OnDriverConnect() => Driver.SetLinks(AvailableResolutions, AvailableScreenModes);

        protected override void OnDriverDisconnect()
        {
        }
    }
}
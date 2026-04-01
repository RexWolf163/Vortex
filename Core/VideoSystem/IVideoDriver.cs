using System.Collections.Generic;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.VideoSystem
{
    /// <summary>
    /// Интерфейс драйверов для видео системы
    /// </summary>
    public interface IVideoDriver : ISystemDriver
    {
        /// <summary>
        /// Передача линка на реестр режимов
        /// </summary>
        /// <param name="resolutions"></param>
        /// <param name="screenModes"></param>
        public void SetLinks(List<string> resolutions, List<string> screenModes);

        /// <summary>
        /// Выставление режима экрана
        /// (полноэкранный, оконный и т.п.)
        /// </summary>
        /// <param name="screenMode"></param>
        public void SetScreenMode(string screenMode);

        /// <summary>
        /// Возвращает режим экрана
        /// (полноэкранный, оконный и т.п.)
        /// </summary>
        /// <returns></returns>
        public string GetScreenMode();

        /// <summary>
        /// Возвращает текущее разрешение монитора
        /// </summary>
        /// <returns></returns>
        public string GetResolution();

        /// <summary>
        /// Задает новое разрешение для видеосистемы устройства
        /// </summary>
        /// <param name="resolution"></param>
        public void SetResolution(string resolution);
    }
}
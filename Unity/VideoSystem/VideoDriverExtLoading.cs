using System;
using UnityEngine;
using Vortex.Core.VideoSystem;
using Vortex.Core.VideoSystem.Model;

namespace Vortex.Unity.VideoSystem
{
    /// <summary>
    /// Расширение драйвера.
    /// Функционал инициализации, загрузки и хранения параметров
    ///
    /// Параметры хранятся отдельно от прочих данных в PlayerPrefs
    /// </summary>
    public partial class VideoDriver
    {
        /// <summary>
        /// Ключ для сохранения данных
        /// </summary>
        private const string SaveKey = "VideoSettings";

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            if (VideoController.SetDriver(Instance))
                return;
            Dispose();
        }

        /// <summary>
        /// Сохранение настроек видеорежима
        /// </summary>
        private void SaveSettings()
        {
            var save = $"{_currentResolution};{(byte)_screenMode}";
            PlayerPrefs.SetString(SaveKey, save);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Загрузка настроек видеорежима
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var save = PlayerPrefs.GetString(SaveKey, string.Empty);
                if (string.IsNullOrEmpty(save))
                {
                    DefaultSettings();
                    SaveSettings();
                    return;
                }

                var ar = save.Split(';');
                _screenMode = Enum.Parse<ScreenMode>(ar[1]);
                _currentResolution = ar[0];
            }
            catch (Exception e)
            {
                DefaultSettings();
                SaveSettings();

                Debug.LogException(e);
                Debug.LogError("[VideoDriver] Video settings was resets.");
            }
        }

        private void DefaultSettings()
        {
            _currentResolution = $"{Screen.currentResolution.width}x{Screen.currentResolution.height}";
            _screenMode = ScreenMode.ExclusiveFullScreen;
            switch (Screen.fullScreenMode)
            {
                case FullScreenMode.FullScreenWindow:
                    _screenMode = ScreenMode.FullScreenWindow;
                    break;
                case FullScreenMode.MaximizedWindow:
                    _screenMode = ScreenMode.MaximizedWindow;
                    break;
                case FullScreenMode.Windowed:
                    _screenMode = ScreenMode.Windowed;
                    break;
            }
        }
    }
}
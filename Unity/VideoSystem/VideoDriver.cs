using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.System.Abstractions;
using Vortex.Core.VideoSystem;
using Vortex.Core.VideoSystem.Model;

namespace Vortex.Unity.VideoSystem
{
    /// <summary>
    /// Драйвер управления режимами монитора 
    /// </summary>
    public partial class VideoDriver : Singleton<VideoDriver>, IVideoDriver
    {
        private readonly Dictionary<string, Resolution> _indexModes = new();
        private List<string> _listResolutions;
        private List<string> _listModes;


        private string _currentResolution;
        private ScreenMode _screenMode;

        public event Action OnInit;

        public void Init()
        {
            _indexModes.Clear();

            var list = Screen.resolutions
                .Select(r => new Resolution { width = r.width, height = r.height })
                .GroupBy(r => new { r.width, r.height })
                .Select(g => g.First())
                .ToArray();

            foreach (var resolution in list)
                _indexModes.AddNew($"{resolution.width}x{resolution.height}", resolution);

            _listResolutions.Clear();
            _listResolutions.AddRange(_indexModes.Keys);

            _listModes.Clear();
            var ar = Enum.GetValues(typeof(ScreenMode));
            foreach (ScreenMode enumValue in ar)
            {
                if (!IsAvailableScreenMode(enumValue))
                    continue;
                _listModes.Add(enumValue.ToString());
            }

            LoadSettings();
            OnInit?.Invoke();
        }

        public void Destroy()
        {
        }

        public void SetLinks(List<string> resolutions, List<string> screenModes)
        {
            _listResolutions = resolutions;
            _listModes = screenModes;
        }

        /// <summary>
        /// Выставление режима экрана
        /// (полноэкранный, оконный и т.п.)
        /// </summary>
        /// <param name="screenMode"></param>
        public void SetScreenMode(string screenMode)
        {
            var res = _indexModes[_currentResolution];
            if (!Enum.TryParse(screenMode, out _screenMode))
                _screenMode = ScreenMode.ExclusiveFullScreen;
            var mode = GetFullScreenMode();

            Screen.SetResolution(res.width, res.height, mode);
            SaveSettings();
        }

        /// <summary>
        /// Возвращает режим экрана
        /// (полноэкранный, оконный и т.п.)
        /// </summary>
        /// <returns></returns>
        public string GetScreenMode() => _screenMode.ToString();

        /// <summary>
        /// Возвращает текущее разрешение монитора
        /// </summary>
        /// <returns></returns>
        public string GetResolution() => _currentResolution;

        /// <summary>
        /// Задает новое разрешение для видеосистемы устройства
        /// </summary>
        /// <param name="resolutionKey"></param>
        public void SetResolution(string resolutionKey)
        {
            if (!_indexModes.TryGetValue(resolutionKey, out var resolution))
            {
                Debug.LogError($"[VideoController] Resolution Key {resolutionKey} not found");
                return;
            }

            _currentResolution = resolutionKey;
            var mode = GetFullScreenMode();
            Screen.SetResolution(resolution.width, resolution.height, mode);
            SaveSettings();
        }

        /// <summary>
        /// Возвращает режим монитора согласно текущим установкам с учетом типа платформы
        /// </summary>
        /// <returns></returns>
        private FullScreenMode GetFullScreenMode()
        {
            switch (_screenMode)
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                case ScreenMode.Windowed:
                    return FullScreenMode.Windowed;
#endif

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                case ScreenMode.MaximizedWindow:
                    return FullScreenMode.MaximizedWindow;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
                case ScreenMode.ExclusiveFullScreen:
                    return FullScreenMode.ExclusiveFullScreen;
#endif
                default:
                    return FullScreenMode.FullScreenWindow;
            }
        }

        /// <summary>
        /// Проверка доступности режима на платформе
        /// </summary>
        /// <param name="screenMode"></param>
        /// <returns></returns>
        private bool IsAvailableScreenMode(ScreenMode screenMode)
        {
            switch (screenMode)
            {
                case ScreenMode.FullScreenWindow:
                    return true;
#if UNITY_EDITOR || UNITY_STANDALONE
                case ScreenMode.Windowed:
                    return true;
#endif

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                case ScreenMode.MaximizedWindow:
                    return true;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
                case ScreenMode.ExclusiveFullScreen:
                    return true;
#endif
                default:
                    return false;
            }
        }
    }
}
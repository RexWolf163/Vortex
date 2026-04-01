using System;
using Vortex.Steam.SteamConnectionSystem.Models;

namespace Vortex.Steam.SteamConnectionSystem
{
    public static class SteamBus
    {
        public static event Action OnCallServices;
        public static event Action OnLoaded;

#if USING_STEAM
        public const bool SteamEnabled = true;
#else
        public const bool SteamEnabled = false;
#endif

        private static bool _isInitialized = false;

        public static bool IsInitialized
        {
            get => _isInitialized;
            internal set =>
#if USING_STEAM
                _isInitialized = value;
#else
                _isInitialized = false;
#endif
        }

        public static bool IsLoaded { get; internal set; }

        /*
        /// <summary>
        /// Модель данных системы коннектора к стиму
        /// </summary>
        public static SteamData Data = new SteamData();

        */
        /// <summary>
        /// Модель данных Пользователя в стиме
        /// </summary>
        public static SteamUserData User { get; internal set; } = new SteamUserData();

        internal static void LoadServices() => OnCallServices?.Invoke();
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Vortex.Core.NetworkSystem;
using Vortex.Core.System.Abstractions;

namespace Vortex.Unity.NetworkSystem.Drivers.Uwr
{
    /// <summary>
    /// Драйвер сети на UnityWebRequest — прокладка без логики: переводит подготовленную попытку в запрос Unity и
    /// объявляет возможности текущей платформы. Регистрируется сам; ядро принимает его, только если он выбран в DriverConfig.
    /// </summary>
    public class UwrNetworkDriver : Singleton<UwrNetworkDriver>, INetworkDriver
    {
        /// <summary>Заголовки, ненадёжные на всех платформах (документация Unity 6, SetRequestHeader).</summary>
        private static readonly string[] CommonUnsettable =
        {
            "Accept-Charset", "Accept-Encoding", "Access-Control-Request-Headers", "Access-Control-Request-Method",
            "Connection", "Content-Length", "Date", "Dnt", "Expect", "Host", "Keep-Alive", "Origin", "Referer", "Te",
            "Trailer", "Transfer-Encoding", "Upgrade", "Via", "X-Unity-Version"
        };

        /// <summary>Заголовки, которые на Web заполняет браузер.</summary>
        private static readonly string[] WebUnsettable = { "Cookie", "Cookie2", "User-Agent" };

        private const string TempFolderName = "VortexNetwork";

        private UwrNetworkSettings _settings;
        private HashSet<string> _unsettable;
        private string _tempFolder;

        public event Action OnInit;

        public INetworkSettings Settings => _settings;

        public bool IsReachable => Application.internetReachability != NetworkReachability.NotReachable;

#if UNITY_WEBGL && !UNITY_EDITOR
        public bool SupportsDownloadToFile => false;
        public bool SupportsFileBodySegments => false;
        public bool SupportsRedirectLimit => false;
#else
        public bool SupportsDownloadToFile => true;
        public bool SupportsFileBodySegments => true;
        public bool SupportsRedirectLimit => true;
#endif
        public bool SupportsUploadProgress => true;
        public bool SupportsDownloadProgress => true;

        public IReadOnlyCollection<string> UnsettableHeaders => _unsettable;

        [RuntimeInitializeOnLoadMethod]
        private static void Run()
        {
            if (NetworkController.SetDriver(Instance))
                return;
            Dispose();
        }

        public void Init()
        {
            var assets = Resources.LoadAll<UwrNetworkSettings>("");
            if (assets.Length != 1)
                throw new InvalidOperationException(
                    $"[Network] UnityWebRequest driver expects exactly one UwrNetworkSettings asset in Resources, found {assets.Length}.");
            _settings = assets[0];

            _unsettable = new HashSet<string>(CommonUnsettable, StringComparer.OrdinalIgnoreCase);
#if UNITY_WEBGL && !UNITY_EDITOR
            _unsettable.UnionWith(WebUnsettable);
#endif

            _tempFolder = Path.Combine(Application.temporaryCachePath, TempFolderName);
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            if (Directory.Exists(_tempFolder))
                Directory.Delete(_tempFolder, true); // «хвосты» прошлых запусков
#endif

            OnInit?.Invoke();
        }

        public void Destroy()
        {
        }

        public INetworkAttempt CreateAttempt(PreparedAttempt attempt) =>
            new UwrAttempt(attempt, SupportsRedirectLimit ? _settings.RedirectLimit : -1, _tempFolder);
    }
}

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
            _settings = NetworkSettingsAsset.LoadSingle<UwrNetworkSettings>();

            _unsettable = new HashSet<string>(CommonUnsettable, StringComparer.OrdinalIgnoreCase);
#if UNITY_WEBGL && !UNITY_EDITOR
            _unsettable.UnionWith(WebUnsettable);
#endif

            // temporaryCachePath общий у редактора, сборок и клонов проекта: у каждого запуска своя подпапка,
            // чужие удаляются, только если давно не менялись.
            var root = Path.Combine(Application.temporaryCachePath, TempFolderName);
            _tempFolder = Path.Combine(root, Guid.NewGuid().ToString("N"));
#if !(UNITY_WEBGL && !UNITY_EDITOR)
            CleanStaleTempEntries(root, TimeSpan.FromHours(_settings.StaleTempFolderHours));
#endif

            OnInit?.Invoke();
        }

        public void Destroy() => TryDelete(_tempFolder);

        /// <summary>«Хвосты» прошлых запусков: записи в общей папке старше срока. Занятые другим процессом пропускаются.</summary>
        private static void CleanStaleTempEntries(string root, TimeSpan maxAge)
        {
            if (!Directory.Exists(root))
                return;
            var cutoff = DateTime.UtcNow - maxAge;
            foreach (var directory in Directory.GetDirectories(root))
                if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                    TryDelete(directory);
            foreach (var file in Directory.GetFiles(root))
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    TryDelete(file);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                else if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Занято другим процессом — удалит следующий запуск.
            }
        }

        public INetworkAttempt CreateAttempt(PreparedAttempt attempt) =>
            new UwrAttempt(attempt, SupportsRedirectLimit ? _settings.RedirectLimit : -1, _tempFolder);
    }
}

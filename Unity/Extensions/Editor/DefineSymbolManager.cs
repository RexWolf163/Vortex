#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Vortex.Unity.Extensions.Editor
{
    /// <summary>
    /// Контроллер добавления define строк по наличию пакетов
    /// Опрашивает через рефлексию всех кто реализует интерфейс INeedPackage
    /// AI-вайб скрипт как основа
    /// </summary>
    internal static class DefineSymbolManager
    {
        /*
        private const string ADDRESSABLES_PACKAGE_NAME = "com.unity.addressables";
        private const string DEFINE_SYMBOL = "ENABLE_ADDRESSABLES";
        */
        internal static bool _isProcessing = false;

        [InitializeOnLoadMethod]
        private static void Run()
        {
            //EditorApplication.delayCall += UpdateDefineSymbols;
            Events.registeringPackages -= OnPackagesChanged;
            Events.registeringPackages += OnPackagesChanged;
            EditorUserBuildSettings.activeBuildTargetChanged -= OnActiveBuildTargetChanged;
            EditorUserBuildSettings.activeBuildTargetChanged += OnActiveBuildTargetChanged;
        }

        private static void OnPackagesChanged(PackageRegistrationEventArgs packageRegistrationEventArgs) =>
            UpdateDefineSymbols();

        private static void OnActiveBuildTargetChanged() => UpdateDefineSymbols();

        internal static void UpdateDefineSymbols()
        {
            if (_isProcessing) return;
            _isProcessing = true;

            var request = Client.List(true);
            EditorApplication.update += WaitForRequest;

            void WaitForRequest()
            {
                if (!request.IsCompleted) return;

                EditorApplication.update -= WaitForRequest;
                _isProcessing = false;

                if (request.Status == StatusCode.Success)
                {
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    var types = assemblies.SelectMany(assembly =>
                        assembly.GetTypes().Where(t =>
                            !t.IsAbstract && !t.IsInterface && typeof(INeedPackage).IsAssignableFrom(t)));

                    foreach (var type in types)
                    {
                        var instance = Activator.CreateInstance(type) as INeedPackage;
                        if (instance == null)
                            continue;
                        var hasPackage = request.Result.Any(pkg => pkg.name == instance.GetPackageName());
                        ApplyDefineSymbolToRelevantPlatforms(hasPackage, instance.GetDefineString());
                    }
                }

                else if (request.Status >= StatusCode.Failure)
                {
                    Debug.LogWarning(
                        $"AddressablesDefineSymbol: ошибка при проверке пакетов — {request.Error?.message}");
                }
            }
        }

        private static void ApplyDefineSymbolToRelevantPlatforms(bool shouldEnable, string defineString)
        {
            var targetGroups = new HashSet<BuildTargetGroup>
            {
                EditorUserBuildSettings.selectedBuildTargetGroup,
                BuildTargetGroup.Standalone
            };

            int modifiedCount = 0;

            foreach (var group in targetGroups)
            {
                if (group == BuildTargetGroup.Unknown) continue;

                try
                {
                    var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
                    string currentDefines = PlayerSettings.GetScriptingDefineSymbols(namedTarget);

                    // Безопасное разбиение и очистка для .NET Standard 2.0
                    var definesSet = new HashSet<string>(
                        currentDefines
                            .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                    );

                    bool wasChanged = false;
                    if (shouldEnable && !definesSet.Contains(defineString))
                    {
                        definesSet.Add(defineString);
                        wasChanged = true;
                    }
                    else if (!shouldEnable && definesSet.Contains(defineString))
                    {
                        definesSet.Remove(defineString);
                        wasChanged = true;
                    }

                    if (wasChanged)
                    {
                        PlayerSettings.SetScriptingDefineSymbols(namedTarget,
                            string.Join(";", definesSet.OrderBy(x => x)));
                        modifiedCount++;
                        Debug.Log(
                            $"AddressablesDefineSymbol: {(shouldEnable ? "Добавлен" : "Удалён")} {defineString} для {group}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"AddressablesDefineSymbol: ошибка при обработке {group} — {ex.Message}");
                }
            }

            if (modifiedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded() => UpdateDefineSymbols();
    }

    public class AddressablesPreBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            DefineSymbolManager.UpdateDefineSymbols();

            var startTime = DateTime.Now;
            while (DefineSymbolManager._isProcessing &&
                   (DateTime.Now - startTime).TotalSeconds < 2.0f)
            {
                System.Threading.Thread.Sleep(10);
            }
        }
    }

    public interface INeedPackage
    {
        /// <summary>
        /// Возвращает название нужного пакета (например: com.unity.addressables)
        /// </summary>
        /// <returns></returns>
        public string GetPackageName();

        /// <summary>
        /// Возвращает ключ который нужно вставить/удалить в настройки проекта
        /// </summary>
        /// <returns></returns>
        public string GetDefineString();
    }
}
#endif
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.CoreAssetsSystem.Editor;
using Vortex.Unity.FileSystem.Bus;

namespace Vortex.Unity.CoreAssetsSystem
{
    public static class CoreAssetsController
    {
        // Каноничное место Vortex-конфигов — Resources/Settings/. Сюда же SettingsDriver
        // кладёт SettingsPreset-наследники, так что все ассеты конфигурации лежат рядом.
        private const string Path = "Resources/Settings";

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            var autoMode = CoreAssetsPreferences.GetCoreAssetAutoCreationMode();
            if (!autoMode)
                return;
            EditorRegister();
        }

        [MenuItem("Tools/Vortex/Debug/Check Core Assets")]
        private static void EditorRegister()
        {
            FileBus.CreateFolders($"{Application.dataPath}/{Path}");

            //Создание ассетов настроек
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var typeList = new List<Type>();
            foreach (var assembly in assemblies)
                try
                {
                    typeList.AddRange(assembly.GetTypes().Where(t =>
                        t.IsSubclassOf(typeof(ScriptableObject))
                        && t.GetInterfaces().Contains(typeof(ICoreAsset))));
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

            var resources = Resources.LoadAll("")?.Select(x => x.GetType()).ToArray() ??
                            Type.EmptyTypes;
            foreach (var type in typeList)
            {
                if (resources.Contains(type))
                    continue;
                var so = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(so, $"Assets/{Path}/{type.Name}.asset");
                Debug.Log($"Create new settings preset {Path}/{type.Name}");
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
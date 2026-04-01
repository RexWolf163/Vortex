using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.FileSystem.Bus;

#if UNITY_EDITOR

namespace Vortex.Unity.CoreAssetsSystem
{
    public static class CoreAssetsController
    {
        private const string Path = "Resources";

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            var autoMode = CoreAssetsPreferences.GetCoreAssetAutoCreationMode();
            if (!autoMode)
                return;
            EditorRegister();
        }

        [MenuItem("Vortex/Debug/Check Core Assets")]
        private static void EditorRegister()
        {
            File.CreateFolders($"{Application.dataPath}/{Path}");

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
            }
        }
    }
}
#endif
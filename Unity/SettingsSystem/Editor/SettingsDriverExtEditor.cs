#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vortex.Unity.FileSystem.Bus;
using Vortex.Unity.SettingsSystem.Presets;

namespace Vortex.Unity.SettingsSystem
{
    public partial class SettingsDriver
    {
        [InitializeOnLoadMethod]
        private static void EditorRegister()
        {
            File.CreateFolders($"{Application.dataPath}/Resources/{Path}");
            //Создание ассетов настроек
            var assetType = typeof(SettingsPreset);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var typeList = new List<Type>();
            foreach (var assembly in assemblies)
                typeList.AddRange(assembly.GetTypes());
            var resources = Resources.LoadAll<SettingsPreset>(Path)?.Select(x => x.GetType()).ToArray() ??
                            Type.EmptyTypes;
            foreach (var type in typeList)
            {
                if (!type.IsSubclassOf(assetType) || resources.Contains(type))
                    continue;
                var so = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(so, $"Assets/Resources/{Path}/{type.Name}.asset");
                Debug.Log($"Create new settings preset {type.Name}");
            }
        }
    }
}
#endif
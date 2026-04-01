#if UNITY_EDITOR
using System;
using System.Linq;
using Sirenix.OdinInspector;

namespace Vortex.Unity.Extensions.Editor.Misc
{
    public class DropDawnHandler
    {
        /// <summary>
        /// Вывод списка название типов реализующих тип или интерфейс
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ValueDropdownList<string> GetTypesNameList<T>()
        {
            var result = new ValueDropdownList<string>();
            var currentDomain = AppDomain.CurrentDomain;
            var assems = currentDomain.GetAssemblies();
            foreach (var assembly in assems)
            {
                var types = assembly.GetTypes();

                switch (typeof(T).IsInterface)
                {
                    case true:
                        foreach (var type in types)
                        {
                            var interfaces = type.GetInterfaces();
                            if (interfaces.Contains(typeof(T)))
                                result.Add(new ValueDropdownItem<string>(type.Name, type.AssemblyQualifiedName));
                        }

                        break;
                    default:
                        foreach (var type in types)
                            if ((type.IsSubclassOf(typeof(T)) || (type == typeof(T))) && !type.IsAbstract)
                                result.Add(new ValueDropdownItem<string>(type.Name, type.AssemblyQualifiedName));
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Вывод существующих в проекте сцен (указанных в настройках сборки)
        /// </summary>
        /// <returns></returns>
        public static ValueDropdownList<string> GetScenes()
        {
            var result = new ValueDropdownList<string>();

            var scenes = UnityEditor.EditorBuildSettings.scenes;

            foreach (var scene in scenes)
            {
                var scenePath = scene.path.Split("Scenes/")[^1].Split('.')[0];
                var sceneName = scenePath.Split("/")[^1].Split('.')[0];
                result.Add(scenePath, sceneName);
            }

            return result;
        }
    }
}
#endif
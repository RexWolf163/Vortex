using System;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
#endif

namespace Vortex.Unity.DriverManagerSystem.Base
{
    /// <summary>
    /// Запись в таблице конфигуратора
    /// </summary>
    [Serializable]
    internal class DriverRecord
    {
        [field: SerializeField, HideInInspector]
        public string SystemType { get; internal set; }

        [field: SerializeField, LabelText("$GetSystemLabel"), ValueDropdown("GetDrivers")]
        public string DriverType { get; internal set; }

#if UNITY_EDITOR

        private string GetSystemLabel()
        {
            var systemType = Type.GetType(SystemType);
            return systemType != null ? systemType.Name : "[Switched Off]";
        }

        public DriverRecord(string systemType)
        {
            SystemType = systemType;
            var list = GetDrivers();
            DriverType = list.Count > 1 ? list.ToArray()[1].Value : "";
        }

        private ValueDropdownList<string> GetDrivers()
        {
            var res = new ValueDropdownList<string> { { "[Switched Off]", "[off]" } };
            var systemType = Type.GetType(SystemType);
            if (systemType == null)
                return res;

            var driverType = GetDriverInterface();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName.StartsWith("ru.vortex"));

            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface && driverType.IsAssignableFrom(t));
                foreach (var type in types)
                    res.Add(type.FullName, type.AssemblyQualifiedName);
            }

            return res;
        }

        private Type GetDriverInterface()
        {
            var t = Type.GetType(SystemType);
            if (t == null)
                throw new InvalidOperationException($"[DriverSettings] Тип не найден: {SystemType}");

            var method = GetStaticMethodInInheritanceChain(t, "GetDriverType");

            if (method == null)
                throw new InvalidOperationException(
                    $"[DriverSettings] Метод GetDriverType не найден в типе {t.FullName}");

            var driver = method.Invoke(null, null) as Type;

            if (driver == null)
                throw new InvalidOperationException(
                    "[DriverSettings] Метод GetDriverType вернул null или некорректное значение");

            return driver;
        }

        public static MethodInfo GetStaticMethodInInheritanceChain(Type type, string methodName)
        {
            while (type != null)
            {
                var method = type.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (method != null)
                    return method;
                type = type.BaseType;
            }

            return null;
        }
#endif
    }
}
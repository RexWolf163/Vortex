using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Vortex.Core.StateAxisSystem.Abstractions
{
    /// <summary>
    /// Lazy-кеш <c>FullName → Type</c> для всех не-абстрактных наследников <see cref="StateAxis"/>.
    /// Заполняется при первом обращении путём скана <c>AppDomain.CurrentDomain.GetAssemblies()</c>.
    /// Используется в <see cref="StateAxis.Deserialize(string)"/> для резолвинга типа по namespace+name.
    /// </summary>
    internal static class StateAxisTypeCache
    {
        private static Dictionary<string, Type> _byFullName;

        public static Type Resolve(string fullName)
        {
            if (_byFullName == null) Build();
            return _byFullName.TryGetValue(fullName, out var t) ? t : null;
        }

        private static void Build()
        {
            _byFullName = new Dictionary<string, Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    if (!typeof(StateAxis).IsAssignableFrom(t)) continue;
                    _byFullName[t.FullName] = t;
                }
            }
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Vortex.Unity.Extensions.Abstractions
{
    public static class SoDataController
    {
        public static PropertyInfo[] GetPropertiesList(this SoData source) =>
            source.GetType()
                .GetProperties(BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance)
                .Where(x => !x.CanWrite).ToArray();

#if UNITY_EDITOR
        internal static void PrintFields(this SoData source)
        {
            var properties = source.GetPropertiesList();
            var list = properties.Select(x => x.Name).ToArray();
            Debug.Log(
                $"<color=#ffa500ff><b>[DEBUG]</b></color> Following {list.Length} fields are available for automatic processing:\n<b>" +
                String.Join("\n", list) + "</b>\n");
        }

#endif
    }
}
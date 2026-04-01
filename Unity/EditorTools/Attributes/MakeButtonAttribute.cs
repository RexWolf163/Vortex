using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    /// <summary>
    /// Атрибут делающий из метода кнопку
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class MakeButtonAttribute : PropertyAttribute
    {
        public readonly string MethodName;

        public MakeButtonAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}
using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ReadOnlyFieldAttribute : PropertyAttribute
    {
    }
}
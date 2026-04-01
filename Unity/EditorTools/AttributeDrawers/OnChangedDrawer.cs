#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Vortex.Unity.EditorTools.Abstraction;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.AttributeDrawers
{
    [CustomPropertyDrawer(typeof(OnChangedAttribute), true)]
    public class OnChangedDrawer : MultiDrawer
    {
    }
}
#endif
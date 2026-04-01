using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HideAttribute : PropertyAttribute
    {
        public string Condition { get; }
        public HideAttribute(string condition = null) { Condition = condition; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HideInPlayAttribute : HideAttribute
    {
        public HideInPlayAttribute(string condition = null) : base(condition) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class HideInEditorAttribute : HideAttribute
    {
        public HideInEditorAttribute(string condition = null) : base(condition) { }
    }
}

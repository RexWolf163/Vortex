using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ShowAttribute : PropertyAttribute
    {
        public string Condition { get; }
        public ShowAttribute(string condition = null) { Condition = condition; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ShowInPlayAttribute : ShowAttribute
    {
        public ShowInPlayAttribute(string condition = null) : base(condition) { }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ShowInEditorAttribute : ShowAttribute
    {
        public ShowInEditorAttribute(string condition = null) : base(condition) { }
    }
}

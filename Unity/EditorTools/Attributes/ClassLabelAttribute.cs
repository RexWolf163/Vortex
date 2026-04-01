using System;

namespace Vortex.Unity.EditorTools.Attributes
{
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property)]
    public class ClassLabelAttribute : Attribute
    {
        public readonly string GroupName;

        public ClassLabelAttribute(string _groupName = "$ToString")
        {
            GroupName = _groupName;
        }
    }
}
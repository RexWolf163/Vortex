using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    public class ClassLabelAttributeProcessor : OdinAttributeProcessor
    {
        private ClassLabelAttribute attribute;

        public override bool CanProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member)
        {
            var index = -1;
            for (var i = parentProperty.Attributes.Count - 1; i >= 0; i--)
            {
                var propertyAttribute = parentProperty.Attributes[i];
                if (propertyAttribute is ClassLabelAttribute)
                    index = i;
            }

            attribute = index >= 0 ? (ClassLabelAttribute)parentProperty.Attributes[index] : null;

            return attribute != null;
        }

        public override void ProcessChildMemberAttributes(InspectorProperty _parentProperty, MemberInfo _member,
            List<Attribute> _attributes)
        {
            if (attribute != null)
            {
                _attributes.Add(new FoldoutGroupAttribute(attribute.GroupName));
                foreach (Attribute attr in _attributes)
                {
                    if (attr is PropertyGroupAttribute grp)
                    {
                        if (!grp.GroupID.StartsWith(attribute.GroupName))
                            grp.GroupID = attribute.GroupName + "/" + grp.GroupID;
                    }
                }
            }
        }
    }
}

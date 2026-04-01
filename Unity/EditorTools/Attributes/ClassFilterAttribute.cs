using System;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Attributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ClassFilterAttribute : PropertyAttribute
    {
        public Type[] RequiredTypes { get; }

        public ClassFilterAttribute(params Type[] requiredTypes)
        {
            foreach (var requiredType in requiredTypes)
            {
                if (requiredType == null)
                    throw new ArgumentNullException(nameof(requiredType));

                if (!requiredType.IsClass && !requiredType.IsInterface)
                    throw new ArgumentException("Тип должен быть классом или интерфейсом.", nameof(requiredType));
            }

            RequiredTypes = requiredTypes;
        }
    }
}
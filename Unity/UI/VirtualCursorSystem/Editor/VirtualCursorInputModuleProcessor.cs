#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using UnityEngine.EventSystems;
using Vortex.Unity.UI.VirtualCursorSystem.UiModule;

namespace Vortex.Unity.UI.VirtualCursorSystem.Editor
{
    /// <summary>
    /// Скрывает в инспекторе <see cref="VirtualCursorInputModule"/> поля, унаследованные от
    /// <see cref="StandaloneInputModule"/>: оси, кнопки и автоповтор навигации Selectable старого Input Manager.
    /// Модуль их не использует — <see cref="VirtualCursorBaseInput"/> отдаёт по ним 0/false.
    /// Поля <see cref="BaseInputModule"/> (Send Pointer Hover To Parent) остаются.
    /// </summary>
    public class VirtualCursorInputModuleProcessor : OdinAttributeProcessor<VirtualCursorInputModule>
    {
        /// <summary>Повесить <see cref="HideInInspector"/> на члены, объявленные в <see cref="StandaloneInputModule"/>.</summary>
        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member,
            List<Attribute> attributes)
        {
            if (member.DeclaringType == typeof(StandaloneInputModule))
                attributes.Add(new HideInInspector());
        }
    }
}
#endif

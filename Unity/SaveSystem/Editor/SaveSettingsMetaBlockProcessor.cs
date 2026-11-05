#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.SaveSystem.Presets;

namespace Vortex.Unity.SaveSystem.Editor
{
    /// <summary>
    /// Рисует переключатели метаблоков <see cref="SaveSettings"/> (поля с <see cref="SaveMetaBlockAttribute"/>)
    /// одиночной кнопкой On/Off. Атрибут добавляется здесь, а не на поле: <see cref="SaveSettings"/> компилируется
    /// в сборку настроек, а та не может ссылаться на EditorTools — цикл через CoreAssetsSystem.
    /// </summary>
    public class SaveSettingsMetaBlockProcessor : OdinAttributeProcessor<SaveSettings>
    {
        public override void ProcessChildMemberAttributes(InspectorProperty parentProperty, MemberInfo member,
            List<Attribute> attributes)
        {
            if (member.GetCustomAttribute<SaveMetaBlockAttribute>() != null)
                attributes.Add(new ToggleButtonAttribute(isSingleButton: true));
        }
    }
}
#endif

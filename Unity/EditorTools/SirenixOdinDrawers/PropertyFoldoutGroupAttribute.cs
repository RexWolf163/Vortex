using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.EditorTools.SirenixOdinDrawers
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = true)]
    public class PropertyFoldoutGroupAttribute : FoldoutGroupAttribute
    {
        public string PropertyName;

        public string Title;

        /// <summary>Adds the property to the specified foldout group.</summary>
        /// <param name="_groupName">Name of the foldout group.</param>
        /// <param name="_order">The order of the group in the inspector.</param>
        public PropertyFoldoutGroupAttribute(string _groupName, int _order = 0) : base(_groupName, _order)
        {
        }

        /// <summary>Adds the property to the specified foldout group.</summary>
        /// <param name="_groupName">Name of the foldout group.</param>
        /// <param name="_expanded">Whether or not the foldout should be expanded by default.</param>
        /// <param name="_order">The order of the group in the inspector.</param>
        public PropertyFoldoutGroupAttribute(string _groupName, bool _expanded, int _order = 0) : base(_groupName,
            _expanded, _order)
        {
        }
    }


#if UNITY_EDITOR

    public class PropertyFoldoutGroupAttributeDrawer : OdinGroupDrawer<PropertyFoldoutGroupAttribute>
    {
#if !ODIN_INSPECTOR_3
        public StringMemberHelper TitleHelper;
#else
        public Sirenix.OdinInspector.Editor.ValueResolvers.ValueResolver<string> TitleHelper;
#endif
        public LocalPersistentContext<bool> IsVisible;
        public string ErrorMessage;
        public object FoldoutKey;
        public InspectorProperty HeaderProperty;

        protected override void Initialize()
        {
            base.Initialize();

            IsVisible = Property.Context.GetPersistent<bool>(this, "IsVisible", false);
            string propName = Attribute.PropertyName ?? Attribute.GroupName;
            HeaderProperty = Property.Children.Get(propName);
            if (HeaderProperty == null)
                ErrorMessage = "No property or field named " + propName +
                               " found. Make sure the property is part of the inspector and the group.";
            else
            {
                TitleHelper = null;
                InspectorProperty helperProperty = HeaderProperty;
                while (helperProperty.Info.PropertyType == PropertyType.Group)
                {
                    helperProperty = helperProperty.Parent;
                    if (helperProperty == null)
                    {
                        helperProperty = HeaderProperty;
                        break;
                    }
                }

#if !ODIN_INSPECTOR_3
                if (Attribute.Title != null)
                {
                    TitleHelper = new StringMemberHelper(Property, Attribute.Title, ref ErrorMessage);
                }
                else
                if (helperProperty.GetAttribute<HideLabelAttribute>() == null)
                {
                    LabelTextAttribute customLabel = helperProperty.GetAttribute<LabelTextAttribute>();
                    if (customLabel == null)
                        TitleHelper = new StringMemberHelper(Property, helperProperty.NiceName, ref ErrorMessage);
                    else
                        TitleHelper = new StringMemberHelper(Property, customLabel.Text, ref ErrorMessage);
                }
#else
                if (Attribute.Title != null)
                {
                    TitleHelper =
                        Sirenix.OdinInspector.Editor.ValueResolvers.ValueResolver.Get<string>(Property,
                            Attribute.Title);
                }
                else if (helperProperty.GetAttribute<HideLabelAttribute>() == null)
                {
                    LabelTextAttribute customLabel = helperProperty.GetAttribute<LabelTextAttribute>();
                    if (customLabel == null)
                        TitleHelper = Sirenix.OdinInspector.Editor.ValueResolvers.ValueResolver.Get(Property,
                            helperProperty.NiceName, helperProperty.NiceName);
                    else
                        TitleHelper =
                            Sirenix.OdinInspector.Editor.ValueResolvers.ValueResolver.Get(Property,
                                customLabel.Text, customLabel.Text);
                }
#endif
            }

            FoldoutKey = UniqueDrawerKey.Create(Property, this);
        }

        /// <summary>Draws the property.</summary>
        protected override void DrawPropertyLayout(GUIContent _label)
        {
            if (ErrorMessage != null)
            {
                SirenixEditorGUI.ErrorMessageBox(ErrorMessage, true);
            }
            else
            {
                _label = null;
                if (TitleHelper != null)
                {
#if !ODIN_INSPECTOR_3
                    string title = TitleHelper.GetString(Property) ?? Attribute.GroupName;
#else
                    string title = TitleHelper.GetValue() ?? Attribute.GroupName;
#endif
                    _label = string.IsNullOrEmpty(title) ? null : new GUIContent(title);
                }

                SirenixEditorGUI.BeginBox();
                SirenixEditorGUI.BeginBoxHeader();

                var foldoutRect = EditorGUILayout.GetControlRect(false,
                    GUILayout.Width(_label != null ? EditorGUIUtility.labelWidth : 10f));
                IsVisible.Value = SirenixEditorGUI.Foldout(foldoutRect, IsVisible.Value, _label ?? GUIContent.none);
                HeaderProperty.Draw(GUIContent.none);

                SirenixEditorGUI.EndBoxHeader();

                if (SirenixEditorGUI.BeginFadeGroup(FoldoutKey, IsVisible.Value))
                {
                    for (int index = 0; index < Property.Children.Count; ++index)
                    {
                        InspectorProperty child = Property.Children[index];
                        if (child != HeaderProperty)
                            child.Draw(child.Label);
                    }
                }

                SirenixEditorGUI.EndFadeGroup();

                SirenixEditorGUI.EndBox();
            }
        }
    }

#endif
}
#if UNITY_EDITOR && ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.UI.TweenerSystem.Editor
{
    /// <summary>
    /// Odin-drawer поля <see cref="TweenerHub"/>: под полем — кнопки Back и Forward, чтобы проверить анимацию
    /// прямо из инспектора владельца. Вне Play Mode хаб переключается мгновенно, в Play Mode — с анимацией.
    /// Поле не назначено — кнопки недоступны.
    /// </summary>
    public sealed class TweenerHubDrawer : OdinValueDrawer<TweenerHub>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            CallNextDrawer(label);

            var hub = ValueEntry.SmartValue;
            using (new EditorGUI.DisabledScope(hub == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Back", EditorStyles.miniButtonLeft))
                    hub.Back();
                if (GUILayout.Button("Forward", EditorStyles.miniButtonRight))
                    hub.Forward();
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Elements
{
    /// <summary>
    /// Сброс параметров RectTransform выбранного объекта к значениям префаба
    /// Хоткей: Alt+Shift+T
    /// </summary>
    public static class RectTransformResetter
    {
        [Shortcut("Vortex/Revert RectTransform", KeyCode.T, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        private static void RevertSelectedRectTransform()
        {
            var go = Selection.activeGameObject;
            if (go == null)
                return;

            var rect = go.GetComponent<RectTransform>();
            if (rect == null)
                return;

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return;

            var modifications = PrefabUtility.GetPropertyModifications(go);
            if (modifications == null)
                return;

            Undo.RecordObject(rect, "Revert RectTransform");

            PrefabUtility.RevertObjectOverride(rect, InteractionMode.UserAction);

            Debug.Log($"[RectTransformResetter] Reverted RectTransform on «{go.name}»");
        }
    }
}
#endif

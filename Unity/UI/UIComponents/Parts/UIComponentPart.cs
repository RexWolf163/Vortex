using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vortex.Unity.UI.UIComponents.Parts
{
    public abstract class UIComponentPart : MonoBehaviour
    {
        [InfoBox(
            "Editor-параметр. Если включить, инспектор перестанет менять настройки rectTransform под заполнение контейнера")]
        [SerializeField]
        private bool onlyNativeSize = false;
#if UNITY_EDITOR
        [OnInspectorInit]
        protected void OnInspector()
        {
            if (onlyNativeSize)
                return;
            var rect = transform.GetComponent<RectTransform>();
            if (rect == null)
                return;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchorMin = Vector2.zero;
        }

        /// <summary>
        /// Editor: пометить целевые объекты части грязными и записать их текущее состояние как модификацию
        /// инстанса префаба. Дёргается снаружи (напр. из <c>SetSpriteComponent</c> через <c>UIComponent</c>)
        /// после PutData в edit-mode — иначе правка графики/текста не сохраняется: <c>SetDirty</c> на
        /// GameObject не создаёт prefab-оверрайд для реально изменённого компонента (Image/Text/…).
        /// </summary>
        public abstract void SetDirty();

        /// <summary>Грязнит объект и фиксирует его текущее состояние как оверрайд инстанса префаба. Null — no-op.</summary>
        protected static void Dirty(Object target)
        {
            if (target == null)
                return;
            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
#endif
    }
}
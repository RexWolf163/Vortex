using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Хэндлер для автоматического выравнивания по указанным параметрам RectTransform.
    /// Применяет настройки в Awake (runtime) и OnValidate (editor).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class AutoRectSetter : MonoBehaviour
    {
        [SerializeField, AutoLink, OnChanged("Apply")]
        private RectTransform rect;

        [Header("Borders (Left / Top / Right / Bottom / PosZ)")]
        [SerializeField, ToggleButton(isSingleButton: true), OnChanged("Apply")]
        private bool setBorders = true;

        [SerializeField, OnChanged("Apply")]
        private float left;

        [SerializeField, OnChanged("Apply")]
        private float top;

        [SerializeField, OnChanged("Apply")]
        private float right;

        [SerializeField, OnChanged("Apply")]
        private float bottom;

        [SerializeField, OnChanged("Apply")]
        private float posZ;

        [Header("Anchors")] [SerializeField, ToggleButton(isSingleButton: true)]
        private bool setAnchors = true;

        [SerializeField, OnChanged("Apply")]
        private Vector2 anchorMin = Vector2.zero;

        [SerializeField, OnChanged("Apply")]
        private Vector2 anchorMax = Vector2.one;

        [Header("Pivot")] [SerializeField, ToggleButton(isSingleButton: true)]
        private bool setPivot = true;

        [SerializeField, OnChanged("Apply")]
        private Vector2 pivot = new(0.5f, 0.5f);

        [Header("Rotation")] [SerializeField, ToggleButton(isSingleButton: true)]
        private bool setRotation = true;

        [SerializeField, OnChanged("Apply")]
        private Vector3 rotation = Vector3.zero;


        private void Awake()
        {
            Apply();
        }

        /// <summary>
        /// Применяет все активные параметры к RectTransform
        /// </summary>
        [Sirenix.OdinInspector.Button("Применить значения")]
        public void Apply()
        {
            var rt = rect;
            if (rt == null)
                return;

            if (setAnchors)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
            }

            if (setPivot)
            {
                rt.pivot = pivot;
            }

            if (setBorders)
            {
                rt.offsetMin = new Vector2(left, bottom); // (left, bottom)
                rt.offsetMax = new Vector2(-right, -top); // (-right, -top)

                var pos = rt.anchoredPosition3D;
                pos.z = posZ;
                rt.anchoredPosition3D = pos;
            }

            if (setRotation)
            {
                rt.localEulerAngles = rotation;
            }
        }
#if UNITY_EDITOR

        private bool _wasInit = false;

        private void Update()
        {
            if (_wasInit)
                return;
            _wasInit = true;
            Apply();
        }

        /// <summary>
        /// Заполняет поля текущими значениями RectTransform
        /// </summary>
        [Sirenix.OdinInspector.Button("Считать текущие значения")]
        private void ReadFromCurrent()
        {
            var rt = rect;
            if (rt == null)
                return;

            UnityEditor.Undo.RecordObject(this, "Read RectTransform Values");

            anchorMin = rt.anchorMin;
            anchorMax = rt.anchorMax;
            pivot = rt.pivot;

            left = rt.offsetMin.x;
            bottom = rt.offsetMin.y;
            right = -rt.offsetMax.x;
            top = -rt.offsetMax.y;
            posZ = rt.anchoredPosition3D.z;

            rotation = rt.localEulerAngles;

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
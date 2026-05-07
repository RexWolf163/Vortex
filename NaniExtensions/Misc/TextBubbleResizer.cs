using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.NaniExtensions.Misc
{
#if UNITY_EDITOR
    [ExecuteAlways]
#endif
    public class TextBubbleResizer : MonoBehaviour
    {
        [SerializeField, OnValueChanged("SourceChanged")]
        private Vector2 defaultSize;

        [SerializeField, OnValueChanged("SourceChanged")]
        private AnimationCurve curve;

        [SerializeField, HorizontalGroup("size"), OnValueChanged("SourceChanged")]
        private int min;

        [SerializeField, HorizontalGroup("size"), OnValueChanged("SourceChanged")]
        private int max;

        [SerializeField, OnValueChanged("SourceChanged")]
        private TextMeshProUGUI source;

        [SerializeField, AutoLink] private RectTransform rectTransform;

        [InfoBox("Включить для тестирования вне рантайма"),
         OnValueChanged("SourceChanged")]
        [SerializeField]
        private bool testMode;

        private bool needRefresh;

        private float size;

        private void OnEnable()
        {
            source.OnPreRenderText += OnPreRenderText;
            Refresh();
        }

        private void OnDisable()
        {
            source.OnPreRenderText -= OnPreRenderText;
        }

        private void OnPreRenderText(TMP_TextInfo tmpTextInfo)
        {
            size = 0;

            var charCount = tmpTextInfo.characterCount;
            if (charCount == 0)
            {
                size = 0;
                needRefresh = true;
                return;
            }

            var chars = tmpTextInfo.characterInfo;
            var totalAdvance = 0f;

            for (int i = 0; i < charCount; i++)
                totalAdvance += chars[i].xAdvance - chars[i].origin;

            size = totalAdvance / 60f;
            needRefresh = true;
        }

        private void Update()
        {
            if (!needRefresh)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying && !testMode)
                return;
#endif
            Refresh();
        }

        [Button]
        private void Refresh()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && !testMode)
                return;
#endif
            needRefresh = false;
            if (max <= 0) return;

            var delta = max - min;
            var fontScale = Mathf.Sqrt(source.fontSize / 30f);
            var scale = curve.Evaluate((size - min) / delta * fontScale);
            rectTransform.sizeDelta = defaultSize * scale;
        }

#if UNITY_EDITOR

        [OnInspectorInit]
        private void OnAdd()
        {
            if (defaultSize != Vector2.zero) return;
            var rect = GetComponent<RectTransform>();
            defaultSize = new Vector2(rect.rect.width, rect.rect.height);
        }

        private bool wasSubscribe = false;

        private void OnValidate()
        {
            if (!testMode || source == null || wasSubscribe)
                return;
            source.OnPreRenderText -= OnPreRenderText;
            source.OnPreRenderText += OnPreRenderText;
            wasSubscribe = true;
        }

        private void SourceChanged()
        {
            if (!testMode)
            {
                if (source != null)
                    source.OnPreRenderText -= OnPreRenderText;
                return;
            }

            if (source != null)
            {
                source.OnPreRenderText -= OnPreRenderText;
                source.OnPreRenderText += OnPreRenderText;
            }

            wasSubscribe = false;
        }
#endif
    }
}
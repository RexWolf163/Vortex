using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.TweenerSystem.UniTaskTweener;

namespace Vortex.Sdk.UIs.RoofTransparentSystem
{
    public class RoofTransparentHandler : MonoBehaviour
    {
        [SerializeField, InfoBox("Центры для триггера расстояния")]
        private TriggerZone[] triggersCenter = new TriggerZone[0];

        /// <summary>
        /// Длительность перехода
        /// </summary>
        [SerializeField, InfoBox("Длительность перехода")]
        public float fadeTime = 0.1f;

        /// <summary>
        /// Порог снижения прозрачности
        /// </summary>
        [SerializeField, InfoBox("Порог прозрачности"), Range(0.01f, 1f)]
        public float minAlpha = 0.35f;

        [SerializeField, AutoLink] private SpriteRenderer sprite;

        private readonly AsyncTween _tween = new();

        private void OnEnable()
        {
            if (triggersCenter.Length == 0)
            {
                Debug.LogWarning($"[RoofTransparentHandler] No trigger centers on {gameObject.name}");
                return;
            }

            RoofTransparentBus.OnUpdatePositions += OnMoveTargets;
        }

        private void OnDisable()
        {
            _tween.Kill();
            RoofTransparentBus.OnUpdatePositions -= OnMoveTargets;
        }

        private void OnMoveTargets()
        {
            var index = RoofTransparentBus.GetIndex();
            var isTransparent = false;
            foreach (var (position, size) in index)
            {
                foreach (var triggerCenter in triggersCenter)
                {
                    if (!((position.Value - triggerCenter.Position).magnitude < triggerCenter.Radius + size))
                        continue;
                    isTransparent = true;
                    break;
                }

                if (isTransparent)
                    break;
            }

            var k = (sprite.color.a - minAlpha) / (1 - minAlpha);
            if (isTransparent)
            {
                if (k > 0)
                {
                    _tween.Set(() => sprite.color.a, (f) => sprite.color = new Color(1, 1, 1, f), minAlpha,
                            fadeTime / k)
                        .SetEase(EaseType.Linear)
                        .Run();
                }
            }
            else if (k < 1f)
                _tween.Set(() => sprite.color.a, (f) => sprite.color = new Color(1, 1, 1, f), 1f, fadeTime * (1f - k))
                    .SetEase(EaseType.Linear)
                    .Run();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (triggersCenter == null) triggersCenter = new TriggerZone[0];
            if (triggersCenter.Length == 0)
                triggersCenter = GetComponentsInChildren<TriggerZone>(true);
        }
#endif
    }
}
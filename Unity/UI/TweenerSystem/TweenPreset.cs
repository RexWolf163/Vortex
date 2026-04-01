using UnityEngine;

namespace Vortex.Unity.UI.TweenerSystem
{
    [CreateAssetMenu(fileName = "TweenPreset", menuName = "Tween Data Preset")]
    public class TweenPreset : ScriptableObject
    {
        public AnimationCurve curve;
        [Range(0f, 5f)] public float duration;
        public bool offOnStartPoint;
        public bool offOnEndPoint;
    }
}
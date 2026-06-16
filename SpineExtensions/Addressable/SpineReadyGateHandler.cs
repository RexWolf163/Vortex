using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.SpineExtensions.Addressable
{
    /// <summary>
    /// Хэндлер для гейтинга слоев по готовности спайна.
    /// Не проверяет на потерю спайном данных!
    ///
    /// Активируется на OnEnable
    ///
    /// Управляемые слои рекомендуется держать по дефолту выключенными
    /// </summary>
    public class SpineReadyGateHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private SkeletonAnimation skeletonAnimation;
        [SerializeField, AutoLink] private SkeletonGraphic skeletonGraphic;

        [SerializeField] private GameObject[] targets;

        private void Awake()
        {
            foreach (var target in targets)
                target.SetActive(false);
        }

        private void OnEnable() => TimeController.Accumulate(Check, this);

        private void OnDisable()
        {
            TimeController.RemoveCall(this);
            foreach (var target in targets)
                target.SetActive(false);
        }

        private void Check()
        {
            if ((skeletonGraphic != null && skeletonGraphic.skeletonDataAsset == null)
                || (skeletonAnimation != null && skeletonAnimation.skeletonDataAsset == null))
            {
                TimeController.Accumulate(Check, this);
                return;
            }

            foreach (var target in targets)
                target.SetActive(true);
        }

#if UNITY_EDITOR
        [Button]
        private void PreparingForStart()
        {
            foreach (var target in targets)
                target.SetActive(false);
        }
#endif
    }
}
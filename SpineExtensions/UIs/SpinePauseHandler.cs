using Sirenix.OdinInspector;
using Spine.Unity;
using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.SpineExtensions.UIs
{
    /// <summary>
    /// Хэндлер "заморозки" спайна.
    /// Фиксирует его при переходе игры в режим паузы
    /// </summary>
    public class SpinePauseHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private SkeletonGraphic spine;
        [SerializeField, AutoLink] private SkeletonAnimation spineAnimation;

        private float _oldTimeScale = 1f;

        private void OnEnable()
        {
            GameController.OnGameStateChanged += OnStateChanged;
            OnStateChanged();
        }

        private void OnDisable()
        {
            GameController.OnGameStateChanged -= OnStateChanged;
            UnPause();
        }

        private void OnStateChanged()
        {
            var state = GameController.GetState();
            switch (state)
            {
                case GameStates.Off:
                case GameStates.Play:
                case GameStates.Win:
                case GameStates.Fail:
                    UnPause();
                    break;
                case GameStates.Loading:
                case GameStates.Paused:
                    Pause();
                    break;
            }
        }

        [Button, HorizontalGroup("Pause")]
        private void Pause()
        {
            if (spine != null)
                spine.freeze = true;
            if (spineAnimation != null)
                spineAnimation.enabled = false;
        }

        [Button, HorizontalGroup("Pause")]
        private void UnPause()
        {
            if (spine != null)
                spine.freeze = false;
            if (spineAnimation != null)
                spineAnimation.enabled = true;
        }
    }
}
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
        }

        private void OnDisable()
        {
            GameController.OnGameStateChanged -= OnStateChanged;
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
                    if (spine != null)
                        spine.freeze = false;
                    if (spineAnimation != null)
                        spineAnimation.timeScale = _oldTimeScale;
                    break;
                case GameStates.Loading:
                case GameStates.Paused:
                    if (spine != null)
                        spine.freeze = true;
                    if (spineAnimation != null)
                    {
                        if (spineAnimation.timeScale != 0)
                            _oldTimeScale = spineAnimation.timeScale;
                        spineAnimation.timeScale = 0;
                    }
                    break;
            }
        }
    }
}
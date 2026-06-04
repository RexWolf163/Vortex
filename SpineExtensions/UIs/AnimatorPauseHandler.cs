using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.SpineExtensions.UIs
{
    /// <summary>
    /// Хэндлер "заморозки" спайна.
    /// Фиксирует его при переходе игры в режим паузы
    /// </summary>
    public class AnimatorPauseHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private Animator animator;

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
            _oldTimeScale = animator.speed;
            animator.speed = 0;
        }

        [Button, HorizontalGroup("Pause")]
        private void UnPause()
        {
            animator.speed = _oldTimeScale;
        }
    }
}
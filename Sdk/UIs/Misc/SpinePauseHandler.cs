using Spine.Unity;
using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.UIs.Misc
{
    /// <summary>
    /// Хэндлер "заморозки" спайна.
    /// Фиксирует его при переходе игры в режим паузы
    /// </summary>
    public class SpinePauseHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private SkeletonGraphic spine;

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
                    spine.freeze = false;
                    break;
                case GameStates.Loading:
                case GameStates.Paused:
                    spine.freeze = true;
                    break;
            }
        }
    }
}
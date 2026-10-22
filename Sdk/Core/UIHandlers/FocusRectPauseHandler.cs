using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.FocusRectSystem;

namespace Vortex.Sdk.Core.UIHandlers
{
    /// <summary>
    /// Заморозка <see cref="FocusRectView"/> на паузе игры: <see cref="GameStates.Paused"/> — плавный переход View
    /// стоит, остальные состояния — идёт. Пока хэндлер выключен, View не заморожен
    /// </summary>
    public class FocusRectPauseHandler : MonoBehaviour
    {
        [SerializeField, AutoLink] private FocusRectView view;

        private void OnEnable()
        {
            GameController.OnGameStateChanged += OnStateChanged;
            OnStateChanged();
        }

        private void OnDisable()
        {
            GameController.OnGameStateChanged -= OnStateChanged;
            if (view != null)
                view.SetPause(false);
        }

        private void OnStateChanged()
        {
            if (view != null)
                view.SetPause(GameController.GetState() == GameStates.Paused);
        }
    }
}

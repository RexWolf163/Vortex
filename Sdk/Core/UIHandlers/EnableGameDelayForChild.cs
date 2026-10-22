using UnityEngine;
using Vortex.Sdk.Core.GameCore;
using Timer = Vortex.Unity.AppSystem.System.TimeSystem.Timer;

namespace Vortex.Sdk.Core.UIHandlers
{
    /// <summary>
    /// Отложенная активация дочерних слоёв по игровому времени — аналог <c>EnableDelayForChild</c> на
    /// <see cref="Timer"/>: на паузе игры (<see cref="GameStates.Paused"/>) отсчёт заморожен, после её снятия
    /// досчитывается остаток.
    /// <list type="bullet">
    /// <item>Дети выключаются при создании; включение объекта запускает отсчёт <see cref="delay"/>, по его концу
    /// дети включаются.</item>
    /// <item>Выключение объекта выключает детей и снимает отсчёт.</item>
    /// <item>Вне игры (редактор) дети включаются сразу.</item>
    /// </list>
    /// </summary>
    [ExecuteInEditMode]
    public class EnableGameDelayForChild : MonoBehaviour
    {
        [SerializeField, Range(0, 10f)] private float delay = 1f;

        private Transform[] _childs;

        /// <summary>
        /// Отсчёт до включения детей; не идёт — <c>null</c>
        /// </summary>
        private Timer _timer;

        private void Awake()
        {
            var count = transform.childCount;
            _childs = new Transform[count];
            for (var i = 0; i < count; i++)
            {
                _childs[i] = transform.GetChild(i);
                _childs[i].gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                SwitchChild(true);
                return;
            }
#endif
            StopTimer();
            _timer = new Timer(delay, OnDelayPassed);
            GameController.OnGameStateChanged += OnGameStateChanged;
            // Таймер запускается сразу — на паузе игры замораживается тут же
            OnGameStateChanged();
        }

        private void OnDisable()
        {
            StopTimer();
            SwitchChild(false);
        }

        private void OnDelayPassed()
        {
            StopTimer();
            SwitchChild(true);
        }

        /// <summary>
        /// Пауза игры — отсчёт заморожен; игра снова идёт — остаток досчитывается
        /// </summary>
        private void OnGameStateChanged()
        {
            if (_timer == null)
                return;
            if (GameController.GetState() == GameStates.Paused)
                _timer.SetPause();
            else
                _timer.Resume();
        }

        private void StopTimer()
        {
            GameController.OnGameStateChanged -= OnGameStateChanged;
            _timer?.Dispose();
            _timer = null;
        }

        private void SwitchChild(bool b)
        {
            foreach (var child in _childs)
                child.gameObject.SetActive(b);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            Awake();
            SwitchChild(false);
        }
#endif
    }
}

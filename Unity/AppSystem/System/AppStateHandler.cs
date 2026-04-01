using System.Collections;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Enums;

namespace Vortex.Unity.AppSystem.System
{
    /// <summary>
    /// обработка перехода в фон и обратно
    /// Фиксирует "анфокус" только если состояние Run
    /// </summary>
    public class AppStateHandler : MonoBehaviour
    {
        private bool _pauseState;

        private AppStates _oldState;

#if UNITY_EDITOR
        /// <summary>
        /// Чекбокс для запуска из редактора
        /// </summary>
        private bool _started;
#endif

        private void Awake()
        {
            _oldState = App.GetState();
            App.OnStateChanged += OnStateChanged;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
#if UNITY_EDITOR
            if (Settings.Data().IgnorePauseInEditor)
                return;
#endif
            _pauseState = !hasFocus;
            SetPauseState();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
#if UNITY_EDITOR
            if (Settings.Data().IgnorePauseInEditor)
                return;
#endif
            _pauseState = pauseStatus;
            SetPauseState();
        }

        private void SetPauseState()
        {
            App.SetState(_pauseState ? AppStates.Unfocused : _oldState);
        }

        private void OnStateChanged(AppStates newState)
        {
            if (newState == AppStates.Unfocused)
                return;
            if (newState == AppStates.Stopping)
            {
#if UNITY_EDITOR
                Debug.LogError("[AppStateHandler] App state is stopping.");
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }

            _oldState = newState;
        }

        /// <summary>
        /// Считается что разрушение этого компонента идет только при выходе из приложения
        /// </summary>
        private void OnDestroy()
        {
#if UNITY_EDITOR
            //Прерывание из-за проблем при запуске с активной сценой
            if (!_started)
                return;
            StopAllCoroutines();
#endif
            App.OnStateChanged -= OnStateChanged;
            App.Exit();
        }

#if UNITY_EDITOR
        private IEnumerator Start()
        {
            //Защита от процессов при запуске из редактора
            yield return new WaitForSeconds(1f);
            _started = true;
        }
#endif
    }
}
using System;
using System.Collections;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.LoaderSystem.Bus;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.LocalizationSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Unity.LocalizationSystem;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.Components.LoaderSystem
{
    /// <summary>
    /// Компонент индикации процесса загрузки системы
    /// Отображает текущий загружаемый модуль, процент выполнения для его процесса, номер модуля
    /// в общем списке загружаемого и размер этого списка 
    /// </summary>
    public class LoaderView : MonoBehaviour
    {
        private enum States
        {
            Waiting,
            Loading,
            Completed,
        }

        [SerializeField, StateSwitcher(typeof(States))]
        private UIStateSwitcher switcher;

        [SerializeField] private UIComponent uiComponent;

        [SerializeField] private string loadingTextPattern = "{0} ({1}) → {2}: {3}%";

        private AppStates _state;

        private void Awake()
        {
            App.OnStateChanged += OnStateChange;
            OnStateChange(App.GetState());
        }

        private void OnDestroy()
        {
            App.OnStateChanged -= OnStateChange;
            StopAllCoroutines();
        }

        private IEnumerator View()
        {
            while (_state == AppStates.Starting)
            {
                Refresh();
                yield return new WaitForSeconds(.3f);
            }
        }

        private void OnStateChange(AppStates state)
        {
            _state = state;
            switch (state)
            {
                case AppStates.Starting:
                    StartCoroutine(View());
                    if (switcher != null)
                        switcher.Set(States.Loading);
                    return;
                case AppStates.Running:
                    StopAllCoroutines();
                    Refresh();
                    if (switcher != null)
                        switcher.Set(States.Completed);
                    App.OnStateChanged -= OnStateChange;
                    return;
                default:
                    if (switcher != null)
                        switcher.Set(States.Waiting);
                    break;
            }
        }

        private void Refresh()
        {
            var loadingData = Loader.GetCurrentLoadingData();
            var step = Loader.GetProgress();
            var size = Loader.GetSize();
            if (loadingData != null)
            {
                var progress = loadingData.Size == 0
                    ? 0
                    : Mathf.Floor(100f * loadingData.Progress / loadingData.Size);
                uiComponent.SetText(String.Format(loadingTextPattern, step, size,
                    loadingData.Name.TryTranslate(),
                    progress));
            }
        }
    }
}
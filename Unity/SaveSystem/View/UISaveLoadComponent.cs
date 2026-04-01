using System;
using System.Collections;
using UnityEngine;
using Vortex.Core.LocalizationSystem;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Core.SaveSystem.Model;
using Vortex.Unity.LocalizationSystem;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Unity.SaveSystem.View
{
    /// <summary>
    /// Компонент индикации процесса загрузки-сохранения
    /// </summary>
    public class UISaveLoadComponent : MonoBehaviour
    {
        private static SaveProcessData _processData;

        [SerializeField] private UIComponent title;

        [SerializeField] private UIComponent progress;

        [SerializeField, LocalizationKey] private string loadingText;
        [SerializeField, LocalizationKey] private string savingText;

        [SerializeField, LocalizationKey] private string progressTextPattern;

        private bool _process;

        private void OnEnable()
        {
            StartCoroutine(Run());
        }

        private void OnDisable()
        {
            _process = false;
            StopAllCoroutines();
        }

        private IEnumerator Run()
        {
            _processData = SaveController.GetProcessData();
            _process = true;
            title.SetText(SaveController.State == SaveControllerStates.Loading ? loadingText : savingText);
            yield return null;
            while (_process)
            {
                var progressValue = _processData.Global.Size == 0
                    ? 100f
                    : Math.Floor(100f * _processData.Module.Progress / _processData.Module.Size);
                progress.SetText(string.Format(progressTextPattern.Translate(),
                    _processData.Global.Progress,
                    _processData.Global.Size,
                    _processData.Module.Name,
                    progressValue));
                yield return null;
            }

            yield return null;
        }
    }
}
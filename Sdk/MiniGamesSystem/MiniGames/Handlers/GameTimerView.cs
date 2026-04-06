using System;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Handlers
{
    public class GameTimerView : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage))]
        private MonoBehaviour source;

        [SerializeField] private bool reverse;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        [SerializeField] private Slider slider;

        private MiniGameData _data;
        private IGameModelWithTimer _dataTimer;

        [SerializeField] private UIStateSwitcher stageSwitcher;

        private float _step;
        private int _currentStage;
        private int _stages;

        private void OnEnable()
        {
            slider.maxValue = 1f;
            slider.minValue = 0f;
            _stages = stageSwitcher?.States.Length - 1 ?? 0;
            _currentStage = 0;
            _step = 0;
            if (_stages > 0)
            {
                _step = 1f / _stages;
                stageSwitcher?.Set(0);
            }

            Storage.OnUpdateLink += UpdateLink;
            Init();
        }

        private void OnDisable()
        {
            DeInit();
            Storage.OnUpdateLink -= UpdateLink;
        }

        private void Init()
        {
            _data = Storage.GetData<MiniGameData>();
            if (_data is not IGameModelWithTimer timer)
            {
                _data = null;
                return;
            }

            _dataTimer = timer;
        }

        private void DeInit()
        {
            _data = null;
            _dataTimer = null;
        }

        private void CheckStage()
        {
            if (_stages == 0)
                return;
            var current = (int)Math.Floor(slider.value / _step);
            if (current.Equals(_currentStage)) return;
            _currentStage = current;
            stageSwitcher.Set(_currentStage);
        }

        private void UpdateLink()
        {
            DeInit();
            Init();
        }

        private void Update()
        {
            if (_data == null) return;
            if (_data.State != MiniGameStates.Play) return;
            var passed = (float)_dataTimer.Timer.GetTimePassed().TotalSeconds;
            var duration = (float)_dataTimer.Timer.Duration.TotalSeconds;
            if (duration == 0)
                slider.value = reverse ? 0f : 1f;
            else
                slider.value = Mathf.Clamp01(reverse ? 1 - passed / duration : passed / duration);
            CheckStage();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (slider != null) return;

            slider = GetComponentInChildren<Slider>();
        }

#endif
    }
}
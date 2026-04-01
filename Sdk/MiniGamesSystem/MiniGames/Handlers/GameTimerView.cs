using Vortex.Sdk.MiniGamesSystem.MiniGames.Abstractions;
using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;
using UnityEngine;
using UnityEngine.UI;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Handlers
{
    public class GameTimerView : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage))]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        [SerializeField] private Slider slider;

        private MiniGameData _data;
        private IGameModelWithTimer _dataTimer;

        private void OnEnable()
        {
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
                slider.value = 1f;
            else
                slider.value = passed / duration;
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
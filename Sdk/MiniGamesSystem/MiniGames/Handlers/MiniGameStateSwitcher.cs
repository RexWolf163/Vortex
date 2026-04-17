using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;
using UnityEngine;
using Vortex.Core.SettingsSystem.Bus;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Handlers
{
    /// <summary>
    /// Хэндлер для отображения состояния миниигры через UIStateSwitcher 
    /// </summary>
    [RequireComponent(typeof(UIStateSwitcher))]
    public class MiniGameStateSwitcher : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage))]
        private MonoBehaviour dataStorage;

        [SerializeField, StateSwitcher(typeof(MiniGameStates)), AutoLink]
        private UIStateSwitcher switcher;

        private IDataStorage _source;
        private IDataStorage Source => _source ??= dataStorage as IDataStorage;

        private MiniGameData _data;
        private MiniGameData Data => _data ??= Source?.GetData<MiniGameData>();

        private void OnEnable()
        {
            switcher.ResetStates();
            Source.OnUpdateLink += OnUpdateLink;
            if (Data == null)
                return;
            Data.OnGameStateChanged += Refresh;
            Refresh();
            //Дублирование отложенным вызовом на случай косяков юнити
            TimeController.Accumulate(Refresh, this);
        }

        private void OnDisable()
        {
            TimeController.RemoveCall(this);
            Source.OnUpdateLink -= OnUpdateLink;
            if (Data == null)
                return;
            Data.OnGameStateChanged -= Refresh;
            _data = null;
        }

        private void OnUpdateLink()
        {
            if (_data != null)
                _data.OnGameStateChanged -= Refresh;
            _data = null;
            if (Data == null)
                return;
            Data.OnGameStateChanged += Refresh;
            Refresh();
        }

        private void Refresh(MiniGameStates state) => Refresh();

        private void Refresh()
        {
            if (this == null) return;
            if (Settings.Data().MiniGameDebugMode)
                Debug.Log($"[MiniGameStateSwitcher:{name}] switch state at «{Data.State}»");
            switcher.Set(Data.State);
        }
    }
}
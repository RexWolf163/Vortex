using Vortex.Sdk.MiniGamesSystem.MiniGames.Model;
using UnityEngine;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.Sdk.MiniGamesSystem.MiniGames.Handlers
{
    public class MockUpStateSwitcher : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour source;

        private IDataStorage _storage;
        private IDataStorage Storage => _storage ??= source as IDataStorage;

        [SerializeField, AutoLink] private UIComponent button;

        [SerializeField] private MiniGameStates state = MiniGameStates.Off;
        private MiniGameData _data;

        private void OnEnable()
        {
            _data = Storage?.GetData<MiniGameData>();
            button.SetAction(SetState);
        }

        private void OnDisable()
        {
            button.SetAction(null);
        }

        private void SetState()
        {
            if (_data == null) return;
            _data.State = state;
            _data.CallOnStateUpdated();
            _data.CallOnUpdated();
        }
    }
}
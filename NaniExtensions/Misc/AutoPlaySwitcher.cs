using Naninovel;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.NaniExtensions.Core;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.NaniExtensions.Misc
{
    /// <summary>
    /// Хэндлер управления "авто-ражимом"
    /// </summary>
    public class AutoPlaySwitcher : MonoBehaviour
    {
        [SerializeField, AutoLink] private UIComponent switcher;

        private IScriptPlayer _player;

        private bool _cached;

        private IScriptPlayer Player => _player ??= NaniWrapper.ScriptPlayer;

        private void Awake() => switcher.SetAction(Toggle);

        private void OnDestroy() => switcher.SetAction(null);

        private void OnEnable()
        {
            Refresh();
            Player.OnAutoPlay += Refresh;
        }

        private void OnDisable()
        {
            Player.OnAutoPlay -= Refresh;
        }

        private void Refresh(bool obj) => Refresh();

        private void Refresh()
        {
            switcher.SetSwitcher(Player.AutoPlayActive ? SwitcherState.On : SwitcherState.Off);
            _cached = Player.AutoPlayActive;
        }

        private void Toggle()
        {
            Player.SetAutoPlayEnabled(!_cached);
            Refresh();
        }
    }
}
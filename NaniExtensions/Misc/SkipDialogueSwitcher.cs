using Naninovel;
using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.NaniExtensions.Core;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;
using Vortex.Unity.UI.UIComponents;

namespace Vortex.NaniExtensions.Misc
{
    /// <summary>
    /// Хэндлер для проматывания диалогов
    /// </summary>
    public class SkipDialogueSwitcher : MonoBehaviour
    {
        [SerializeField, AutoLink] private UIComponent switcher;

        [SerializeField, StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher enableSwitcher;

        private IScriptPlayer _player;

        private IScriptPlayer Player => _player ??= NaniWrapper.ScriptPlayer;

        private void Awake() => switcher.SetAction(Toggle);

        private void OnDestroy() => switcher.SetAction(null);

        private void OnEnable()
        {
            Refresh();
            Player.OnSkip += Refresh;
            Player.OnStop += OnStop;
            Player.OnPlay += CheckView;
            CheckView();
        }

        private void OnDisable()
        {
            Player.OnSkip -= Refresh;
            Player.OnStop -= OnStop;
            Player.OnPlay -= CheckView;
        }

        private void OnStop(Script obj)
        {
            Player.SetSkipEnabled(false);
            Refresh();
        }

        private void CheckView(Script obj) => CheckView();

        private void CheckView() =>
            enableSwitcher.Set(GetSkipAllowed() ? SwitcherState.On : SwitcherState.Off);

        private bool GetSkipAllowed()
        {
            if (Player.SkipMode == PlayerSkipMode.Everything) return true;
            if (Player.PlayedScript is null) return false;
            return Player.HasPlayed(Player.PlayedScript.Path, Player.PlayedIndex + 1);
        }


        private void Refresh(bool obj) => Refresh();

        private void Refresh() => switcher.SetSwitcher(Player.SkipActive ? SwitcherState.On : SwitcherState.Off);

        private void Toggle()
        {
            Player.SetSkipEnabled(!Player.SkipActive);
            Refresh();
        }
    }
}
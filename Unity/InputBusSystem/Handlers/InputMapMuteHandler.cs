using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Unity.InputBusSystem.Handlers
{
    /// <summary>
    /// Заглушка карты ввода: пока включён, подписан пустыми колбэками на все экшены карты, кроме исключений, и по
    /// LIFO-маршрутизации забирает их сигналы у подписанных раньше. Быстрый mute карты без её выключения.
    ///
    /// Перекрывает только подписчиков шины, подписанных до включения заглушки: подписавшийся позже встаёт сверху —
    /// порядок задаётся вёрсткой. Не глушит подписанных на <c>InputAction</c> в обход шины (Naninovel) и чтение
    /// значения через <see cref="InputController.GetAction"/>
    /// </summary>
    public class InputMapMuteHandler : MonoBehaviour
    {
        [SerializeField, ValueSelector("GetInputMaps")]
        private string inputMap;

        [SerializeField, ValueSelector("GetMapActions")]
        [Tooltip("Экшены карты, которые продолжают работать.")]
        private string[] exceptions = new string[0];

        /// <summary>Экшены, на которые подписана заглушка (id «Карта/Экшен»)</summary>
        private readonly List<string> _muted = new();

        private void OnEnable()
        {
            if (App.GetState() < AppStates.Running)
            {
                App.OnStateChanged += OnAppInit;
                return;
            }

            var prefix = $"{inputMap}/";
            _muted.AddRange(InputController.GetActions()
                .Where(action => action.StartsWith(prefix) && !exceptions.Contains(action)));
            foreach (var action in _muted)
                InputController.AddActionUser(action, this, null, null);
        }

        private void OnDisable()
        {
            App.OnStateChanged -= OnAppInit;
            foreach (var action in _muted)
                InputController.RemoveActionUser(action, this);
            _muted.Clear();
        }

        private void OnDestroy() => OnDisable();

        private void OnAppInit(AppStates states)
        {
            if (states < AppStates.Running) return;
            App.OnStateChanged -= OnAppInit;
            OnEnable();
        }

#if UNITY_EDITOR
        private string[] GetInputMaps() => InputController.GetMaps();

        /// <summary>Экшены выбранной карты — варианты исключений</summary>
        private string[] GetMapActions() =>
            InputController.GetActions().Where(action => action.StartsWith($"{inputMap}/")).ToArray();
#endif
    }
}

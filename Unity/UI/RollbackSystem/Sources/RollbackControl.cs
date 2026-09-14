using UnityEngine;
using UnityEngine.UI;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Misc.DropDown;

namespace Vortex.Unity.UI.RollbackSystem.Sources
{
    /// <summary>
    /// Маркер контрола под откат — аналог <c>RollbackSlaveObserver</c> для новой системы. Вешается на объект с
    /// выпадающим списком или слайдером и регистрирует его в <see cref="UIControlsRollback"/> ближайшего
    /// <see cref="RollbackHandler"/>. Лежит на самом контроле, поэтому переезжает вместе с ним при копировании.
    ///
    /// Регистрация — в конце кадра включения: к этому моменту контрол успевает получить значение. Снимается
    /// регистрация только при уничтожении: порядок выключения родителя и детей не гарантирован, и снятие в
    /// OnDisable могло бы опередить откат хэндлера при закрытии окна.
    /// </summary>
    public class RollbackControl : MonoBehaviour
    {
        [SerializeField, Tooltip("Хэндлер отката. Подставляется ближайший у родителей.")]
        private RollbackHandler handler;

        [SerializeField, AutoLink] private DropDownComponent dropdown;
        [SerializeField, AutoLink] private Slider slider;

        private UIControlsRollback _source;

        private void OnEnable() => TimeController.Call(Link, this);

        private void OnDestroy()
        {
            TimeController.RemoveCall(this);
            if (_source == null)
                return;
            _source.Unlink(dropdown);
            _source.Unlink(slider);
        }

        private void Link()
        {
            _source ??= handler.GetSource<UIControlsRollback>();
            if (_source == null)
            {
                Debug.LogError("[RollbackControl] У хэндлера нет источника UIControlsRollback.", this);
                return;
            }

            if (dropdown != null)
                _source.Link(dropdown);
            if (slider != null)
                _source.Link(slider);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (handler == null)
                handler = GetComponentInParent<RollbackHandler>(true);
        }
#endif
    }
}

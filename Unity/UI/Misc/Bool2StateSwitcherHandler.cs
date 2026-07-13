using UnityEngine;
using Vortex.Core.Extensions.DefaultEnums;
using Vortex.Core.Extensions.ReactiveValues;
using Vortex.Core.System.Abstractions;
using Vortex.Unity.EditorTools.Attributes;
using Vortex.Unity.UI.Attributes;
using Vortex.Unity.UI.StateSwitcher;

namespace Vortex.Unity.UI.Misc
{
    /// <summary>
    /// Гонит <see cref="UIStateSwitcher"/> по значению <see cref="BoolData"/> из связанного
    /// <see cref="IDataStorage"/>: <c>true</c> → <see cref="SwitcherState.On"/>, <c>false</c> → Off.
    /// Потребитель — переподключается по <c>OnUpdateLink</c>, реагирует на <c>OnUpdateData</c>.
    /// </summary>
    public class Bool2StateSwitcherHandler : MonoBehaviour
    {
        [SerializeField, ClassFilter(typeof(IDataStorage)), AutoLink]
        private MonoBehaviour storage;

        [SerializeField, StateSwitcher(typeof(SwitcherState))]
        private UIStateSwitcher switcher;

        private IDataStorage _source;
        private BoolData _data;

        private void OnEnable()
        {
            _source ??= storage as IDataStorage;
            if (_source == null)
            {
                Debug.LogError($"[Bool2StateSwitcherHandler] No IDataStorage selected for {name}");
                return;
            }

            if (switcher == null)
            {
                Debug.LogError($"[Bool2StateSwitcherHandler] No UIStateSwitcher selected for {name}");
                return;
            }

            _source.OnUpdateLink += Relink;
            Relink();
        }

        private void OnDisable()
        {
            if (_source != null)
                _source.OnUpdateLink -= Relink;
            Unsubscribe();
        }

        private void Relink()
        {
            var data = _source.GetData<BoolData>();
            if (data == _data) return;

            Unsubscribe();
            _data = data;
            if (_data == null) return;

            _data.OnUpdateData += Apply;
            Apply();
        }

        private void Unsubscribe()
        {
            if (_data == null) return;
            _data.OnUpdateData -= Apply;
            _data = null;
        }

        private void Apply() => switcher.Set(_data.Value ? SwitcherState.On : SwitcherState.Off);
    }
}

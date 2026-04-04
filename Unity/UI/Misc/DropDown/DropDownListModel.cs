using System;
using System.Collections.Generic;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Unity.UI.Misc.DropDown
{
    public class DropDownListModel : IReactiveData, IDisposable
    {
        public DropDownListModel(Action<int> selectCallback, Action closeCallback, IReadOnlyList<string> texts,
            bool closeOnSelected, int current = 0)
        {
            SelectCallback = selectCallback;
            CloseCallback = closeCallback;
            Texts = texts;
            CloseOnSelected = closeOnSelected;
            Current = current;
        }

        public int Current { get; set; }
        public Action<int> SelectCallback { get; }
        public Action CloseCallback { get; }

        public bool CloseOnSelected { get; }
        public int ScrollSensitivity { get; set; }

        public IReadOnlyList<string> Texts { get; }

        public event Action OnUpdateData;

        public void CallOnUpdate() => OnUpdateData?.Invoke();

        public void Dispose() => OnUpdateData = null;
    }
}
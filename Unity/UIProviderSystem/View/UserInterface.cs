using System;
using UnityEngine;
using Vortex.Core.AppSystem.Bus;
using Vortex.Core.System.Enums;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Unity.AppSystem.System.TimeSystem;
using Vortex.Unity.DatabaseSystem.Attributes;
using Vortex.Unity.UI.TweenerSystem;
using UIProvider = Vortex.Core.UIProviderSystem.Bus.UIProvider;

namespace Vortex.Unity.UIProviderSystem.View
{
    /// <summary>
    /// Класс представление-контроллер интерфейса
    /// </summary>
    [Serializable]
    public sealed partial class UserInterface : MonoBehaviour
    {
        #region Params

        [SerializeField, DbRecord(typeof(UserInterfaceData))]
        private string preset;

        private UserInterfaceData _data;

        /// <summary>
        /// Окно-Контейнер
        /// </summary>
        [SerializeField] private RectTransform wndContainer;

        /// <summary>
        /// Твиннеры открытия/закрытия
        /// </summary>
        [SerializeField] private TweenerHub[] tweeners;

        /// <summary>
        /// Флаг состояния представления
        /// </summary>
        private bool _isOpen;

        private Vector2 Offset
        {
            get => new(_data.Offset.x, _data.Offset.y);
            set => _data.Offset = ((int)value.x, (int)value.y);
        }

        private bool _isRegistred = false;

        #endregion

        #region Private

        private void OnEnable()
        {
            /*
            foreach (var tweener in tweeners)
                tweener.Back(true);
            */
            _isOpen = false;
            _isRegistred = false;
            if (App.GetState() >= AppStates.Running)
                TimeController.Call(Registrate, this);
            else
                App.OnStart += Registrate;
        }

        private void OnDisable()
        {
            TimeController.RemoveCall(this);
            App.OnStart -= Registrate;
            if (!_isRegistred)
                return;

            foreach (var condition in _data.Conditions)
                condition.DeInit();

            UIProvider.Unregister(preset);
            _data.DeInit();
            if (dragZone != null)
                dragZone.OnDrag -= CalcPosition;
            _data.OnOpen -= Check;
            _data.OnClose -= Check;
            _data = null;
            _isOpen = false;
            foreach (var tweener in tweeners)
                tweener.Back(true);
        }

        /// <summary>
        /// Выставление значения окна в нужное положение согласно данным модели
        /// </summary>
        private void Check()
        {
            if (_data.IsOpen)
                Open();
            else
                Close();
        }

        /// <summary>
        /// Проиграть анимацию открытия интерфейса
        /// </summary>
        private void Open()
        {
            if (_isOpen)
                return;
            SetPosition();
            foreach (var tweener in tweeners)
            {
                tweener.Back(true);
                tweener.Forward();
            }

            _isOpen = true;
        }

        /// <summary>
        /// Проиграть анимацию закрытия интерфейса
        /// </summary>
        private void Close()
        {
            if (!_isOpen)
                return;
            foreach (var tweener in tweeners)
                tweener.Back();
            _isOpen = false;
        }

        private void Registrate()
        {
            _data = UIProvider.Register(preset);
            if (_data == null)
                return;
            _data.OnOpen += Check;
            _data.OnClose += Check;
            if (dragZone != null)
                dragZone.OnDrag += CalcPosition;
            _data.Init();
            _isRegistred = true;
            Check();
        }

        #endregion

        #region Public

        /// <summary>
        /// Возвращает id интерфейса
        /// </summary>
        /// <returns></returns>
        public string GetId() => preset;

        #endregion
    }
}
using System;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;

namespace Vortex.Core.Extensions.LogicExtensions.Actions
{
    /// <summary>
    /// Точка подписок на инициализацию компонента (Шлюз инициализации)
    /// Работает как Action, собирая подписки снаружи и откладывая их до момента вызова InitValve.
    /// Дальнейшие подписки будут сразу вызываться через Invoke  
    /// </summary>
    public sealed class InitValve : IDisposable
    {
        private readonly object _lock = new();

        private Action _action;
        private bool _isOpened;
        private bool _isDead;

        /// <summary>
        /// Создание клапана
        /// </summary>
        /// <param name="openValve"></param>
        /// <returns></returns>
        public static InitValve Create(out Action openValve)
        {
            var valve = new InitValve();
            openValve = valve.OpenValve;
            return valve;
        }

        private InitValve()
        {
            _action = null;
            _isOpened = false;
            _isDead = false;
        }

        /// <summary>
        /// Переключение режима работы на активацию отложенных подписок
        /// </summary>
        private void OpenValve()
        {
            Action action;
            lock (_lock)
            {
                if (_isDead)
                {
                    Log.Print(LogLevel.Error, "InitValve is already dead", this);
                    return;
                }

                if (_isOpened)
                {
                    Log.Print(LogLevel.Warning, "InitValve is already opened", this);
                    return;
                }

                _isOpened = true;
                action = _action;
                _action = null;
            }

            action?.Invoke();
        }

        private void Subscribe(Action value)
        {
            var needInvoke = false;
            lock (_lock)
            {
                if (_isDead)
                {
                    Log.Print(LogLevel.Error, "InitValve is already dead", this);
                    return;
                }

                if (!_isOpened)
                    _action += value;
                else
                    needInvoke = true;
            }

            if (needInvoke)
                value.Invoke();
        }

        private void Unsubscribe(Action value)
        {
            lock (_lock)
            {
                if (_isDead)
                    return;
                if (value != null && _action != null)
                    _action -= value;
            }
        }

        public static InitValve operator +(InitValve sa, Action value)
        {
            sa.Subscribe(value);
            return sa;
        }

        public static InitValve operator -(InitValve sa, Action value)
        {
            sa.Unsubscribe(value);
            return sa;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _isDead = true;
                _action = null;
            }
        }
    }
}
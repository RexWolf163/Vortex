using System;
using System.Collections.Generic;
using Vortex.Core.System.Abstractions;

namespace Vortex.Core.System.Models
{
    /// <summary>
    /// Класс-враппер для отслеживания блока задержек типа INeedDelay
    /// Когда каждая из них отпишется о завершении - будет вызван переданный при создании колбэк _callbackGood.
    /// Если было событие ошибки - будет сразу же вызван _callbackOnError.
    /// В зависимости от выбранного в конструкторе флага, после ошибки колбэк _callbackGood может быть вызван или нет.
    ///
    /// Замечания:
    /// - Если все наблюдаемые уже завершены на момент создания, _callbackGood вызывается
    ///   синхронно прямо из конструктора (до возврата из new). Колбэк не должен полагаться
    ///   на то, что ссылка на этот DelayedObserver уже присвоена вызывающей стороне.
    /// - Не потокобезопасен: рассчитан на вызовы из одного потока (Unity main-thread).
    /// </summary>
    public class DelayedObserver : IDisposable
    {
        private readonly HashSet<INeedDelay> _observables = new();

        private readonly Action _callbackGood;
        private readonly Action<INeedDelay> _callbackOnError;

        private readonly bool _callOnError = false;
        private bool _hasError;

        public DelayedObserver(INeedDelay[] observables, Action callbackGood, Action<INeedDelay> callbackOnError = null,
            bool callOnError = false)
        {
            _callbackGood = callbackGood;
            _callbackOnError = callbackOnError;
            _callOnError = callOnError;
            foreach (var observable in observables)
            {
                if (observable.IsCompleted)
                {
                    if (observable.IsFailed)
                    {
                        _hasError = true;
                        callbackOnError?.Invoke(observable);
                    }

                    continue;
                }

                if (!_observables.Add(observable))
                    continue;
                observable.OnReady += Check;
                observable.OnError += OnError;
            }

            if (_observables.Count == 0 && (_callOnError || !_hasError))
                _callbackGood?.Invoke();
        }

        private void OnError(INeedDelay observable)
        {
            _hasError = true;
            observable.OnReady -= Check;
            observable.OnError -= OnError;
            _observables.Remove(observable);
            _callbackOnError?.Invoke(observable);
            if (_observables.Count == 0 && _callOnError)
                _callbackGood?.Invoke();
        }

        private void Check(INeedDelay observable)
        {
            observable.OnReady -= Check;
            observable.OnError -= OnError;
            _observables.Remove(observable);
            if (_observables.Count == 0 && (_callOnError || !_hasError))
                _callbackGood?.Invoke();
        }

        public void Dispose()
        {
            foreach (var observable in _observables)
            {
                observable.OnReady -= Check;
                observable.OnError -= OnError;
            }

            _observables.Clear();
        }
    }
}
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Vortex.Core.NetworkSystem;

namespace Vortex.Unity.NetworkSystem.Mock
{
    /// <summary>
    /// Попытка мока: ждёт задержку, затем за длительность передачи плавно ведёт прогресс и завершается.
    /// «Не отвечать» не завершается никогда — попытку прерывает таймаут ядра.
    /// </summary>
    internal sealed class MockAttempt : INetworkAttempt
    {
        private readonly PreparedAttempt _attempt;
        private readonly MockReactionKind _kind;
        private readonly int _statusCode;
        private readonly IReadOnlyList<NetworkHeader> _headers;
        private readonly byte[] _body;
        private readonly double _delay;
        private readonly double _transfer;
        private readonly string _error;
        private Stopwatch _clock;
        private bool _aborted;

        public MockAttempt(PreparedAttempt attempt, MockReactionKind kind, int statusCode, IReadOnlyList<NetworkHeader> headers,
            byte[] body, float delay, float transfer, string error)
        {
            _attempt = attempt;
            _kind = kind;
            _statusCode = statusCode;
            _headers = headers;
            _body = body;
            _delay = delay;
            _transfer = transfer;
            _error = error;
        }

        public void Start() => _clock = Stopwatch.StartNew();

        public bool IsDone => !_aborted && _clock != null && _kind != MockReactionKind.NoResponse && Elapsed >= _delay + _transfer;

        public float UploadProgress => Progress;

        public float DownloadProgress => Progress;

        public long DownloadedBytes => _body == null ? 0 : (long)(_body.LongLength * Progress);

        public AttemptResult GetResult()
        {
            if (_kind == MockReactionKind.TransportError)
                return new AttemptResult(0, null, null, TransportError.Network, _error);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_headers != null)
                foreach (var header in _headers)
                    if (header != null && !string.IsNullOrWhiteSpace(header.name))
                        headers[header.name] = header.value ?? string.Empty;

            if (_attempt.DownloadPath == null)
                return new AttemptResult(_statusCode, headers, _body, TransportError.None, null);

            File.WriteAllBytes(_attempt.DownloadPath, _body ?? Array.Empty<byte>());
            return new AttemptResult(_statusCode, headers, null, TransportError.None, null);
        }

        public void Abort() => _aborted = true;

        public void Dispose()
        {
        }

        private double Elapsed => _clock?.Elapsed.TotalSeconds ?? 0.0;

        private float Progress
        {
            get
            {
                if (_kind == MockReactionKind.NoResponse || _clock == null || Elapsed < _delay)
                    return 0f;
                if (_transfer <= 0.0)
                    return IsDone ? 1f : 0f;
                return (float)Math.Min(1.0, (Elapsed - _delay) / _transfer);
            }
        }
    }
}
#endif

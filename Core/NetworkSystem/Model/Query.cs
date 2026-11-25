using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.Extensions.ReactiveValues;

namespace Vortex.Core.NetworkSystem
{
    /// <summary>
    /// Модель запроса: собирается цепочкой до запуска (<see cref="Send"/>), после запуска заперта — любая запись в
    /// параметры даёт исключение. Запрос одноразовый: для повтора — <see cref="CloneDraft"/>.
    ///
    /// Наблюдение: реактивные <see cref="State"/>, <see cref="Outcome"/>, <see cref="UploadProgress"/>,
    /// <see cref="DownloadProgress"/> (пишет только контроллер), обработчики цепочки (вызываются в тех же точках, сразу
    /// после изменения поля) и ожидание <see cref="Completion"/>.
    ///
    /// Освобождение: <see cref="GetResult"/> (после завершения; исключает из реестра, данные остаются у владельца),
    /// флаг <see cref="SetAutoRelease"/> или <see cref="Release"/>. Тело читается по требованию — <see cref="TryRead{T}"/>.
    /// </summary>
    public sealed class Query
    {
        private readonly UniTaskCompletionSource _completion = new();

        private HttpMethod? _method;
        private UrlData _url;
        private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
        private RequestBody _body;
        private float? _timeout;
        private bool? _retryAllowed;
        private Action<RetryPolicy> _retryEdit;
        private bool _autoRelease;
        private string _downloadPath;
        private CancellationToken _externalToken;

        private Action<QueryState> _onState;
        private Action<QueryOutcome> _onOutcome;
        private Action<float> _onUploadProgress;
        private Action<float> _onDownloadProgress;

        /// <summary>Номер в рамках сессии. Присваивается при запуске; до запуска 0.</summary>
        public long Id { get; internal set; }

        /// <summary>Запрос запущен: параметры заперты.</summary>
        public bool IsLocked { get; private set; }

        public EnumData<QueryState> State { get; } = new(QueryState.Draft, NetworkController.OwnerKey);
        public EnumData<QueryOutcome> Outcome { get; } = new(QueryOutcome.None, NetworkController.OwnerKey);
        public FloatData UploadProgress { get; } = new(0f, NetworkController.OwnerKey);
        public FloatData DownloadProgress { get; } = new(0f, NetworkController.OwnerKey);

        /// <summary>Номер текущей попытки (с учётом повтора после обновления токена).</summary>
        public int Attempt { get; internal set; }

        /// <summary>Итоговый адрес (после запуска, без маскирования).</summary>
        public string FinalUrl { get; internal set; }

        /// <summary>Ответ. Заполняется при завершении.</summary>
        public QueryResult Result { get; internal set; }

        /// <summary>Завершается, когда присвоен итог.</summary>
        public UniTask Completion => _completion.Task;

        #region Параметры (читает контроллер)

        internal HttpMethod? MethodOrNull => _method;
        internal UrlData Url => _url;
        internal IReadOnlyDictionary<string, string> Headers => _headers;
        internal RequestBody Body => _body;
        internal float? Timeout => _timeout;
        internal bool? RetryAllowed => _retryAllowed;
        internal Action<RetryPolicy> RetryEdit => _retryEdit;
        internal bool AutoRelease => _autoRelease;
        internal string DownloadPath => _downloadPath;
        internal CancellationToken ExternalToken => _externalToken;

        /// <summary>Провайдер тела, выбранный при запуске (для чтения тела).</summary>
        internal IBodyProvider BodyProvider { get; set; }

        /// <summary>Внутренний источник отмены (освобождение, выход).</summary>
        internal CancellationTokenSource Cancellation { get; set; }

        /// <summary>Запрошено освобождение до завершения: освободить сразу после итога.</summary>
        internal bool ReleaseRequested { get; set; }

        #endregion

        #region Цепочка

        public Query SetMethod(HttpMethod method)
        {
            ThrowIfLocked();
            _method = method;
            return this;
        }

        /// <summary>Адрес: полный URL (неявно из строки) или <see cref="UrlData.FromHost"/>.</summary>
        public Query SetUrl(UrlData url)
        {
            ThrowIfLocked();
            _url = url ?? throw new ArgumentNullException(nameof(url));
            return this;
        }

        /// <summary>Адрес из именованного хоста настроек и пути.</summary>
        public Query SetHost(string hostName, string path = null) => SetUrl(UrlData.FromHost(hostName, path));

        /// <summary>Параметр адреса. Значение форматируется сразу; неподдерживаемый тип — исключение.</summary>
        public Query AddParam(string key, object value)
        {
            ThrowIfLocked();
            RequireUrl();
            _url.SetParameter(key, value);
            return this;
        }

        public Query AddParams(IEnumerable<KeyValuePair<string, object>> parameters)
        {
            ThrowIfLocked();
            RequireUrl();
            foreach (var parameter in parameters)
                _url.SetParameter(parameter.Key, parameter.Value);
            return this;
        }

        public Query AddHeader(string name, string value)
        {
            ThrowIfLocked();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("[Network] Header name is empty.", nameof(name));
            _headers[name] = value ?? string.Empty;
            return this;
        }

        /// <summary>Тело: <see cref="RequestBody"/> или любой объект (сериализуется провайдером по маске URL).</summary>
        public Query SetBody(object body)
        {
            ThrowIfLocked();
            _body = body as RequestBody ?? new ObjectBody(body);
            return this;
        }

        /// <summary>Таймаут одной попытки, секунды.</summary>
        public Query SetTimeout(float seconds)
        {
            ThrowIfLocked();
            if (seconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "[Network] Timeout must be positive.");
            _timeout = seconds;
            return this;
        }

        /// <summary>Разрешить или запретить повторы. По умолчанию: GET — да, иные методы — нет.</summary>
        public Query SetRetry(bool allowed)
        {
            ThrowIfLocked();
            _retryAllowed = allowed;
            return this;
        }

        /// <summary>Правка копии политики повторов по умолчанию. Применяется при запуске.</summary>
        public Query ConfigureRetry(Action<RetryPolicy> edit)
        {
            ThrowIfLocked();
            _retryEdit = edit ?? throw new ArgumentNullException(nameof(edit));
            return this;
        }

        /// <summary>Освободить автоматически по завершении. <see cref="GetResult"/> тогда недоступен.</summary>
        public Query SetAutoRelease(bool autoRelease = true)
        {
            ThrowIfLocked();
            _autoRelease = autoRelease;
            return this;
        }

        /// <summary>Скачать ответ в файл (запись через временный файл, переименование только при успехе).</summary>
        public Query DownloadToFile(string path)
        {
            ThrowIfLocked();
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("[Network] Download path is empty.", nameof(path));
            _downloadPath = path;
            return this;
        }

        public Query SetCancellation(CancellationToken token)
        {
            ThrowIfLocked();
            _externalToken = token;
            return this;
        }

        public Query OnState(Action<QueryState> handler)
        {
            ThrowIfLocked();
            _onState = handler;
            return this;
        }

        public Query OnOutcome(Action<QueryOutcome> handler)
        {
            ThrowIfLocked();
            _onOutcome = handler;
            return this;
        }

        public Query OnUploadProgress(Action<float> handler)
        {
            ThrowIfLocked();
            _onUploadProgress = handler;
            return this;
        }

        public Query OnDownloadProgress(Action<float> handler)
        {
            ThrowIfLocked();
            _onDownloadProgress = handler;
            return this;
        }

        /// <summary>Запустить. После вызова параметры заперты.</summary>
        public Query Send()
        {
            ThrowIfLocked();
            IsLocked = true;
            NetworkController.Launch(this);
            return this;
        }

        /// <summary>Новый несобранный запрос с теми же параметрами и обработчиками.</summary>
        public Query CloneDraft()
        {
            var copy = new Query
            {
                _method = _method,
                _url = _url?.Clone(),
                _body = _body,
                _timeout = _timeout,
                _retryAllowed = _retryAllowed,
                _retryEdit = _retryEdit,
                _autoRelease = _autoRelease,
                _downloadPath = _downloadPath,
                _externalToken = _externalToken,
                _onState = _onState,
                _onOutcome = _onOutcome,
                _onUploadProgress = _onUploadProgress,
                _onDownloadProgress = _onDownloadProgress
            };
            foreach (var header in _headers)
                copy._headers[header.Key] = header.Value;
            return copy;
        }

        #endregion

        #region Результат

        /// <summary>
        /// Забрать запрос: исключает его из реестра и возвращает его же. Только после завершения, один раз,
        /// не при автоосвобождении — иначе исключение.
        /// </summary>
        public Query GetResult()
        {
            NetworkController.TakeResult(this);
            return this;
        }

        /// <summary>Освободить без получения результата. Если запрос выполняется — он отменяется.</summary>
        public void Release() => NetworkController.Release(this);

        /// <summary>
        /// Прочитать тело в класс <typeparamref name="T"/> провайдером, выбранным по маске URL при запуске.
        /// До завершения — исключение. Нет тела (304, пустой ответ) — <c>false</c> «нет данных».
        /// </summary>
        public bool TryRead<T>(out T value, out string error)
        {
            value = default;
            if (State.Value != QueryState.Completed && State.Value != QueryState.Released)
                throw new InvalidOperationException($"[Network] Query #{Id} is not completed; body cannot be read yet.");

            if (Result == null || !Result.HasBody)
            {
                error = "Нет данных.";
                return false;
            }

            byte[] data;
            try
            {
                data = Result.Body ?? File.ReadAllBytes(Result.FilePath);
            }
            catch (Exception e)
            {
                error = "Не удалось прочитать файл ответа: " + e.Message;
                return false;
            }

            // Сырые форматы не требуют провайдера.
            if (typeof(T) == typeof(byte[]))
            {
                value = (T)(object)data;
                error = null;
                return true;
            }

            if (typeof(T) == typeof(string))
            {
                value = (T)(object)Encoding.UTF8.GetString(data);
                error = null;
                return true;
            }

            if (!BodyProvider.TryDeserialize(data, typeof(T), out var raw, out error))
                return false;

            value = (T)raw;
            return true;
        }

        #endregion

        #region Изменения состояния (только контроллер)

        internal void SetState(QueryState state)
        {
            if (State.Value == state)
                return;
            State.Set(state, NetworkController.OwnerKey);
            _onState?.Invoke(state);
        }

        internal void SetOutcome(QueryOutcome outcome)
        {
            Outcome.Set(outcome, NetworkController.OwnerKey);
            _onOutcome?.Invoke(outcome);
        }

        internal void SetUploadProgress(float value)
        {
            if (UploadProgress.Value.Equals(value))
                return;
            UploadProgress.Set(value, NetworkController.OwnerKey);
            _onUploadProgress?.Invoke(value);
        }

        internal void SetDownloadProgress(float value)
        {
            if (DownloadProgress.Value.Equals(value))
                return;
            DownloadProgress.Set(value, NetworkController.OwnerKey);
            _onDownloadProgress?.Invoke(value);
        }

        internal void CompleteAwaiters() => _completion.TrySetResult();

        #endregion

        private void ThrowIfLocked()
        {
            if (IsLocked)
                throw new InvalidOperationException($"[Network] Query #{Id} is already sent; parameters are locked.");
        }

        private void RequireUrl()
        {
            if (_url == null)
                throw new InvalidOperationException("[Network] Set the URL (SetUrl/SetHost) before adding parameters.");
        }
    }
}

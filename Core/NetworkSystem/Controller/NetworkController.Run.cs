using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Vortex.Core.LoggerSystem.Bus;
using Vortex.Core.LoggerSystem.Model;
using Vortex.Core.SettingsSystem.Bus;

namespace Vortex.Core.NetworkSystem
{
    public partial class NetworkController
    {
        private const string LogSource = "Network";

        /// <summary>Итог одной попытки.</summary>
        private readonly struct AttemptOutcome
        {
            public readonly QueryOutcome Outcome;
            public readonly AttemptResult Result;
            public readonly string Message;

            public AttemptOutcome(QueryOutcome outcome, AttemptResult result, string message)
            {
                Outcome = outcome;
                Result = result;
                Message = message;
            }

            public long StatusCode => Result?.StatusCode ?? 0;
        }

        #region Попытки и повторы

        private static async UniTaskVoid RunAsync(Query query, Prepared prepared)
        {
            var token = query.Cancellation.Token;
            var clock = Stopwatch.StartNew();
            var limit = _settings.MaxConcurrentRequests;
            var policyAttempts = 0;
            var authRefreshed = false;
            var isAuthRetry = false;
            string authToken = null;
            var last = default(AttemptOutcome);

            try
            {
                while (true)
                {
                    // Токен — до слота: источник токена может сам ходить в сеть через этот контроллер,
                    // и ожидание токена под занятым слотом при заполненном лимите даёт дедлок.
                    if (prepared.Auth != null && authToken == null)
                    {
                        authToken = await prepared.TokenSource(false, token);
                        // Источник мог не учесть отмену: запрос уже завершён или освобождён — дальше не идём.
                        token.ThrowIfCancellationRequested();
                    }

                    await Registry.AcquireSlotAsync(limit, token);
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        query.Attempt++;
                        if (!isAuthRetry)
                            policyAttempts++;
                        isAuthRetry = false;
                        query.SetState(QueryState.Running);

                        var headers = new Dictionary<string, string>(prepared.Headers, StringComparer.OrdinalIgnoreCase);
                        if (prepared.Auth != null && !string.IsNullOrEmpty(authToken))
                        {
                            prepared.Auth.ApplyHeaders(authToken, headers);
                            ThrowIfUnsettable(query.Id, headers.Keys); // провайдер мог дописать запрещённый заголовок
                        }

                        last = await ExecuteAttemptAsync(query, prepared, headers, token);
                    }
                    finally
                    {
                        Registry.ReleaseSlot(limit);
                    }

                    LogInfo($"#{query.Id} attempt {query.Attempt}: {last.Outcome} ({last.StatusCode}).");

                    // 401 → новый токен → один повтор вне счёта политики.
                    if (last.StatusCode == 401 && prepared.Auth != null && !authRefreshed)
                    {
                        authRefreshed = true;
                        authToken = await prepared.TokenSource(true, token);
                        token.ThrowIfCancellationRequested();
                        if (string.IsNullOrEmpty(authToken))
                            break;
                        isAuthRetry = true;
                        query.SetState(QueryState.Queued);
                        continue;
                    }

                    if (!ShouldRetry(prepared, last, policyAttempts, out var delay))
                        break;

                    query.SetState(QueryState.RetryWait);
                    LogInfo($"#{query.Id} retry in {delay.ToString("0.##", CultureInfo.InvariantCulture)} s.");
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.Realtime, PlayerLoopTiming.Update, token);
                    query.SetState(QueryState.Queued);
                }

                if (!TryFinishDownload(prepared, last.Outcome, out var fileError))
                {
                    Complete(query, QueryOutcome.BodyError, last.Result, clock.Elapsed, query.Attempt, fileError, null);
                    return;
                }

                if (last.Outcome is QueryOutcome.Success or QueryOutcome.NotModified)
                {
                    // 1 — только по итогу: между попытками прогресс начинается заново, ложной «готовности» нет.
                    query.SetUploadProgress(1f);
                    query.SetDownloadProgress(1f);
                }

                Complete(query, last.Outcome, last.Result, clock.Elapsed, query.Attempt, last.Message,
                    last.Outcome == QueryOutcome.Success && prepared.DownloadPath != null ? prepared.DownloadPath : null);
            }
            catch (OperationCanceledException)
            {
                TryFinishDownload(prepared, QueryOutcome.Cancelled, out _);
                Complete(query, QueryOutcome.Cancelled, last.Result, clock.Elapsed, query.Attempt, "Cancelled.", null);
            }
            catch (Exception e)
            {
                // Исключение источника токена, провайдера или обработчика — ошибка программиста: завершаем запрос и пробрасываем.
                TryFinishDownload(prepared, QueryOutcome.Cancelled, out _);
                Complete(query, QueryOutcome.Cancelled, last.Result, clock.Elapsed, query.Attempt, e.Message, null);
                throw;
            }
        }

        /// <summary>
        /// Итог известен при запуске (сеть выключена, тело больше лимита, нет файла части), но выдаётся на следующем
        /// кадре — после возврата из Send: обработчики и ожидающие видят запрос уже у вызывающего кода, а повторная
        /// отправка из обработчика не растит стек.
        /// </summary>
        private static async UniTaskVoid CompleteImmediateAsync(Query query, QueryOutcome outcome, string message)
        {
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (query.Outcome.Value != QueryOutcome.None)
                return; // уже завершён выходом из приложения
            if (query.Cancellation.IsCancellationRequested)
                Complete(query, QueryOutcome.Cancelled, null, TimeSpan.Zero, 0, "Cancelled.", null);
            else
                Complete(query, outcome, null, TimeSpan.Zero, 0, message, null);
        }

        private static bool ShouldRetry(Prepared prepared, AttemptOutcome last, int policyAttempts, out float delay)
        {
            delay = 0f;
            var policy = prepared.RetryPolicy;
            if (!prepared.RetryAllowed
                || last.Outcome is QueryOutcome.Success or QueryOutcome.NotModified
                || !policy.IsRetryable(last.Outcome, last.StatusCode)
                || policyAttempts >= policy.maxAttempts)
                return false;

            delay = policy.GetDelay(policyAttempts, Jitter);
            if (!policy.respectRetryAfter || !TryGetRetryAfter(last.Result, out var retryAfter))
                return true;

            if (retryAfter > policy.maxDelay)
                return false; // сервер просит ждать дольше, чем мы готовы — итог последней попытки
            delay = retryAfter;
            return true;
        }

        private static async UniTask<AttemptOutcome> ExecuteAttemptAsync(Query query, Prepared prepared,
            Dictionary<string, string> headers, CancellationToken token)
        {
            // Исключение при создании попытки — забота драйвера: он освобождает уже выделенное сам (см. INetworkDriver.CreateAttempt).
            var attempt = Driver.CreateAttempt(new PreparedAttempt(query.Id, query.Attempt, prepared.Url, prepared.Method, headers,
                prepared.ContentType, prepared.Body ?? (IReadOnlyList<BodySegment>)Array.Empty<BodySegment>(),
                prepared.TempDownloadPath));
            try
            {
                query.SetUploadProgress(0f);
                query.SetDownloadProgress(0f);
                attempt.Start();
                var clock = Stopwatch.StartNew();

                while (!attempt.IsDone)
                {
                    if (token.IsCancellationRequested)
                    {
                        attempt.Abort();
                        throw new OperationCanceledException(token);
                    }

                    if (clock.Elapsed.TotalSeconds > prepared.Timeout)
                    {
                        attempt.Abort();
                        return new AttemptOutcome(QueryOutcome.Timeout, null,
                            $"Attempt exceeded the timeout of {prepared.Timeout} s.");
                    }

                    if (attempt.DownloadedBytes > _settings.MaxResponseBytes)
                    {
                        attempt.Abort();
                        return TooLargeResponse(attempt.DownloadedBytes);
                    }

                    if (Driver.SupportsUploadProgress)
                        query.SetUploadProgress(attempt.UploadProgress);
                    if (Driver.SupportsDownloadProgress)
                        query.SetDownloadProgress(attempt.DownloadProgress);

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                var result = attempt.GetResult();

                // Последний кусок мог прийти в том же кадре, что и завершение: лимит проверяется и здесь — в том числе
                // для скачивания в файл, где тела в памяти нет.
                var size = result.Body?.LongLength ?? attempt.DownloadedBytes;
                if (size > _settings.MaxResponseBytes)
                    return TooLargeResponse(size);

                return Classify(result);
            }
            finally
            {
                attempt.Dispose();
            }
        }

        private static AttemptOutcome TooLargeResponse(long size) =>
            new(QueryOutcome.TooLarge, null, $"Response {size} bytes exceeds the limit {_settings.MaxResponseBytes}.");

        private static AttemptOutcome Classify(AttemptResult result)
        {
            if (result.Error == TransportError.Network)
                return new AttemptOutcome(QueryOutcome.Network, result, result.ErrorMessage ?? "Network error.");

            var code = result.StatusCode;
            var outcome = code switch
            {
                304 => QueryOutcome.NotModified,
                >= 200 and < 300 => QueryOutcome.Success,
                >= 400 and < 500 => QueryOutcome.ClientError,
                >= 500 and < 600 => QueryOutcome.ServerError,
                _ => QueryOutcome.Network
            };
            var message = outcome is QueryOutcome.Success or QueryOutcome.NotModified ? null : $"HTTP {code}.";
            return new AttemptOutcome(outcome, result, message);
        }

        private static bool TryGetRetryAfter(AttemptResult result, out float seconds)
        {
            seconds = 0f;
            if (result?.Headers == null || !TryGetHeader(result.Headers, "Retry-After", out var value))
                return false;

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                return seconds >= 0f;

            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                return false;
            seconds = (float)Math.Max(0.0, (date - DateTimeOffset.UtcNow).TotalSeconds);
            return true;
        }

        private static bool TryGetHeader(IReadOnlyDictionary<string, string> headers, string name, out string value)
        {
            foreach (var header in headers)
            {
                if (!string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
                    continue;
                value = header.Value;
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Скачивание в файл: при успехе временный файл заменяет целевой, иначе удаляется. Успех без временного файла
        /// (пустой ответ) даёт пустой целевой файл — прежнее содержимое не выдаётся за свежее.
        /// Ошибка файловой системы не прерывает завершение: <c>false</c> и текст ошибки, запрос всё равно получит итог.
        /// </summary>
        private static bool TryFinishDownload(Prepared prepared, QueryOutcome outcome, out string error)
        {
            error = null;
            var temp = prepared.TempDownloadPath;
            if (temp == null)
                return true;

            try
            {
                if (outcome != QueryOutcome.Success)
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                    return true;
                }

                if (File.Exists(prepared.DownloadPath))
                    File.Delete(prepared.DownloadPath);
                if (File.Exists(temp))
                    File.Move(temp, prepared.DownloadPath);
                else
                    File.WriteAllBytes(prepared.DownloadPath, Array.Empty<byte>());
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                error = $"Downloaded file could not be finalized ({prepared.DownloadPath}): {e.Message}";
                LogWarning(error);
                return false;
            }
        }

        #endregion

        #region Завершение и освобождение

        /// <summary>
        /// Присвоить итог (один раз): итог → обработчик → «Завершён» → обработчик → ожидающие.
        /// Исключение обработчика (ошибка программиста) не прерывает завершение: запрос доходит до «Завершён»,
        /// ожидающие отпускаются, автоосвобождение выполняется, затем первое исключение пробрасывается.
        /// </summary>
        private static void Complete(Query query, QueryOutcome outcome, AttemptResult result, TimeSpan duration,
            int attempts, string message, string filePath)
        {
            if (query.Outcome.Value != QueryOutcome.None)
                return;

            query.Result = new QueryResult(result?.StatusCode ?? 0, result?.Headers,
                filePath == null ? result?.Body : null, filePath, duration, attempts, message);

            ExceptionDispatchInfo handlerError = null;
            Step(() => query.SetOutcome(outcome), ref handlerError);
            Step(() => query.SetState(QueryState.Completed), ref handlerError);
            query.CompleteAwaiters();

            if (outcome is QueryOutcome.Success or QueryOutcome.NotModified)
                LogInfo($"#{query.Id} → {outcome} ({query.Result.StatusCode}), {duration.TotalSeconds:0.###} s." +
                        BodyForLog(query.Result));
            else
                LogWarning($"#{query.Id} → {outcome} ({query.Result.StatusCode}): {message}" + BodyForLog(query.Result));

            if (query.AutoRelease || query.ReleaseRequested)
                Step(() => ReleaseInternal(query), ref handlerError);

            handlerError?.Throw();
        }

        /// <summary>
        /// Шаг завершения или освобождения: исключение обработчика (ошибка программиста) не прерывает остальные шаги —
        /// запоминается первое, вызывающий пробрасывает его в конце.
        /// </summary>
        private static void Step(Action action, ref ExceptionDispatchInfo firstError)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                firstError ??= ExceptionDispatchInfo.Capture(e);
            }
        }

        internal static void TakeResult(Query query)
        {
            if (query.AutoRelease)
                throw new InvalidOperationException($"[Network] Query #{query.Id} is auto-released; GetResult is not available.");
            if (query.State.Value != QueryState.Completed)
                throw new InvalidOperationException(
                    $"[Network] Query #{query.Id}: GetResult requires a completed, not yet released query (state {query.State.Value}).");
            ReleaseInternal(query);
        }

        internal static void Release(Query query)
        {
            switch (query.State.Value)
            {
                case QueryState.Draft:
                    throw new InvalidOperationException("[Network] Query is not sent; nothing to release.");
                case QueryState.Released:
                    return;
                case QueryState.Completed:
                    ReleaseInternal(query);
                    return;
                default:
                    query.ReleaseRequested = true;
                    query.Cancellation.Cancel();
                    return;
            }
        }

        /// <summary>
        /// Исключить из реестра, перевести в «Освобождён», освободить токен. Исключение подписчика или обработчика
        /// не прерывает освобождение: шаги выполняются все, затем первое исключение пробрасывается.
        /// </summary>
        private static void ReleaseInternal(Query query)
        {
            ExceptionDispatchInfo handlerError = null;
            Step(() => Registry.Remove(query), ref handlerError);
            Step(() => query.SetState(QueryState.Released), ref handlerError);
            query.Cancellation?.Dispose();
            query.Cancellation = null;

            handlerError?.Throw();
        }

        /// <summary>
        /// Выход из приложения: всё незавершённое отменяется, всё освобождается. Исключение обработчика одного
        /// запроса не прерывает освобождение остальных: первое исключение пробрасывается после обхода.
        /// </summary>
        private static void ExitAll()
        {
            ExceptionDispatchInfo handlerError = null;
            var active = new List<Query>(Registry.Active);
            foreach (var query in active)
            {
                Step(() =>
                {
                    query.Cancellation?.Cancel();
                    Complete(query, QueryOutcome.Cancelled, null, TimeSpan.Zero, query.Attempt, "Application exit.", null);
                }, ref handlerError);

                if (query.State.Value != QueryState.Released)
                    Step(() => ReleaseInternal(query), ref handlerError);
            }

            handlerError?.Throw();
        }

        #endregion

        #region Лог

        private static bool DebugEnabled => Settings.Data()?.NetworkDebugMode ?? false;

        private static void LogInfo(string message)
        {
            if (DebugEnabled)
                Log.Print(LogLevel.Common, "[Network] " + message, LogSource);
        }

        private static void LogWarning(string message) => Log.Print(LogLevel.Warning, "[Network] " + message, LogSource);

        private static string BodyForLog(QueryResult result)
        {
            if (!DebugEnabled || result.Body == null || result.Body.Length == 0)
                return string.Empty;
            var text = Encoding.UTF8.GetString(result.Body);
            var max = _settings.MaxLoggedBodyChars;
            return "\n" + (text.Length > max ? text.Substring(0, max) + "…" : text);
        }

        #endregion
    }
}

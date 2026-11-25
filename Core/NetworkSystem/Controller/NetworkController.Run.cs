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
                    if (prepared.Auth != null)
                        authToken ??= await TokenSources[prepared.Auth.TokenSourceKey](false, token);

                    await Registry.AcquireSlotAsync(limit, token);
                    try
                    {
                        query.Attempt++;
                        if (!isAuthRetry)
                            policyAttempts++;
                        isAuthRetry = false;
                        query.SetState(QueryState.Running);

                        var headers = new Dictionary<string, string>(prepared.Headers, StringComparer.OrdinalIgnoreCase);
                        if (prepared.Auth != null && !string.IsNullOrEmpty(authToken))
                            prepared.Auth.ApplyHeaders(authToken, headers);

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
                        authToken = await TokenSources[prepared.Auth.TokenSourceKey](true, token);
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

                FinishDownload(prepared, last.Outcome);
                Complete(query, last.Outcome, last.Result, clock.Elapsed, query.Attempt, last.Message,
                    last.Outcome == QueryOutcome.Success && prepared.DownloadPath != null ? prepared.DownloadPath : null);
            }
            catch (OperationCanceledException)
            {
                FinishDownload(prepared, QueryOutcome.Cancelled);
                Complete(query, QueryOutcome.Cancelled, last.Result, clock.Elapsed, query.Attempt, "Cancelled.", null);
            }
            catch (Exception e)
            {
                // Исключение источника токена или провайдера — ошибка программиста: завершаем запрос и пробрасываем.
                FinishDownload(prepared, QueryOutcome.Cancelled);
                Complete(query, QueryOutcome.Cancelled, last.Result, clock.Elapsed, query.Attempt, e.Message, null);
                throw;
            }
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
                query.SetUploadProgress(1f);
                query.SetDownloadProgress(1f);

                if (result.Body != null && result.Body.LongLength > _settings.MaxResponseBytes)
                    return TooLargeResponse(result.Body.LongLength);

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

        /// <summary>Скачивание в файл: при успехе временный файл заменяет целевой, иначе удаляется.</summary>
        private static void FinishDownload(Prepared prepared, QueryOutcome outcome)
        {
            if (prepared.TempDownloadPath == null || !File.Exists(prepared.TempDownloadPath))
                return;

            if (outcome != QueryOutcome.Success)
            {
                File.Delete(prepared.TempDownloadPath);
                return;
            }

            if (File.Exists(prepared.DownloadPath))
                File.Delete(prepared.DownloadPath);
            File.Move(prepared.TempDownloadPath, prepared.DownloadPath);
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
            try
            {
                query.SetOutcome(outcome);
            }
            catch (Exception e)
            {
                handlerError = ExceptionDispatchInfo.Capture(e);
            }

            try
            {
                query.SetState(QueryState.Completed);
            }
            catch (Exception e)
            {
                handlerError ??= ExceptionDispatchInfo.Capture(e);
            }

            query.CompleteAwaiters();

            if (outcome is QueryOutcome.Success or QueryOutcome.NotModified)
                LogInfo($"#{query.Id} → {outcome} ({query.Result.StatusCode}), {duration.TotalSeconds:0.###} s." +
                        BodyForLog(query.Result));
            else
                LogWarning($"#{query.Id} → {outcome} ({query.Result.StatusCode}): {message}" + BodyForLog(query.Result));

            if (query.AutoRelease || query.ReleaseRequested)
            {
                try
                {
                    ReleaseInternal(query);
                }
                catch (Exception e)
                {
                    handlerError ??= ExceptionDispatchInfo.Capture(e);
                }
            }

            handlerError?.Throw();
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
            try
            {
                Registry.Remove(query);
            }
            catch (Exception e)
            {
                handlerError = ExceptionDispatchInfo.Capture(e);
            }

            try
            {
                query.SetState(QueryState.Released);
            }
            catch (Exception e)
            {
                handlerError ??= ExceptionDispatchInfo.Capture(e);
            }

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
                try
                {
                    query.Cancellation?.Cancel();
                    Complete(query, QueryOutcome.Cancelled, null, TimeSpan.Zero, query.Attempt, "Application exit.", null);
                }
                catch (Exception e)
                {
                    handlerError ??= ExceptionDispatchInfo.Capture(e);
                }

                if (query.State.Value == QueryState.Released)
                    continue;

                try
                {
                    ReleaseInternal(query);
                }
                catch (Exception e)
                {
                    handlerError ??= ExceptionDispatchInfo.Capture(e);
                }
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

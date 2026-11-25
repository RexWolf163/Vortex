using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Vortex.Core.NetworkSystem
{
    public partial class NetworkController
    {
        /// <summary>Всё, что подготовлено при запуске и неизменно на время жизни запроса.</summary>
        private sealed class Prepared
        {
            public HttpMethod Method;
            public string Url;
            public string LogUrl;
            public Dictionary<string, string> Headers;
            public string ContentType;
            public List<BodySegment> Body;
            public IAuthProvider Auth;
            public float Timeout;
            public bool RetryAllowed;
            public RetryPolicy RetryPolicy;
            public string DownloadPath;
            public string TempDownloadPath;
        }

        /// <summary>Запуск запроса: проверки (ошибки программиста/конфигурации — исключения), подготовка, реестр, очередь.</summary>
        internal static void Launch(Query query)
        {
            if (!IsInit)
                throw new InvalidOperationException("[Network] Network system is not initialized (no driver or driver not ready).");

            query.Id = ++_lastQueryId;
            var prepared = Prepare(query, out var immediateOutcome, out var immediateMessage);

            query.Cancellation = query.ExternalToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(query.ExternalToken)
                : new CancellationTokenSource();

            Registry.Add(query);
            query.SetState(QueryState.Queued);
            LogInfo($"#{query.Id} {prepared.Method} {prepared.LogUrl} → queued.");

            if (!NetworkEnabled.Value)
            {
                Complete(query, QueryOutcome.NetworkDisabled, null, TimeSpan.Zero, 0, "Network is disabled.", null);
                return;
            }

            if (immediateOutcome != QueryOutcome.None)
            {
                Complete(query, immediateOutcome, null, TimeSpan.Zero, 0, immediateMessage, null);
                return;
            }

            RunAsync(query, prepared).Forget();
        }

        private static Prepared Prepare(Query query, out QueryOutcome immediateOutcome, out string immediateMessage)
        {
            immediateOutcome = QueryOutcome.None;
            immediateMessage = null;

            var url = query.Url ?? throw new InvalidOperationException($"[Network] Query #{query.Id}: URL is not set.");
            var method = query.MethodOrNull ?? _settings.SimpleMethod;

            // --- Адрес ---
            string baseAddress;
            if (url.HostName != null)
            {
                if (!_hosts.TryGetValue(url.HostName, out baseAddress))
                    throw new InvalidOperationException($"[Network] Query #{query.Id}: unknown host '{url.HostName}'.");
            }
            else
            {
                baseAddress = url.AbsoluteUrl;
            }

            var raw = baseAddress + (url.Path ?? string.Empty);
            if (raw.IndexOf('#') >= 0)
                throw new InvalidOperationException($"[Network] Query #{query.Id}: URL must not contain a fragment '#': {raw}");
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException($"[Network] Query #{query.Id}: invalid http(s) URL '{raw}'.");

            var normalized = NetworkUrl.NormalizeForMasks(uri);
            var isHttp = uri.Scheme == Uri.UriSchemeHttp;
            if (isHttp && !MatchesAny(_settings.HttpAllowList, normalized))
                throw new InvalidOperationException($"[Network] Query #{query.Id}: http is not allowed for '{normalized}'.");

            // --- Провайдеры ---
            var bodyProvider = FirstMatch(_settings.BodyProviderRules, normalized) ?? _settings.DefaultBodyProvider;
            var auth = FirstMatch(_settings.AuthRules, normalized);
            if (auth != null)
            {
                if (isHttp)
                    throw new InvalidOperationException($"[Network] Query #{query.Id}: auth rule matched an http host '{normalized}'; tokens are never sent over http.");
                if (!TokenSources.ContainsKey(auth.TokenSourceKey))
                    throw new InvalidOperationException($"[Network] Query #{query.Id}: token source '{auth.TokenSourceKey}' is not registered.");
            }

            // --- Параметры: из адреса + добавленные (повтор ключа — перезапись с предупреждением) ---
            var parameters = ParseQueryString(uri.Query);
            foreach (var parameter in url.Parameters)
                MergeParameter(parameters, parameter.Key, parameter.Value, query.Id);

            // --- Тело ---
            var body = query.Body;
            string contentType = null;
            List<BodySegment> segments = null;
            var noBodyMethod = method == HttpMethod.Get || method == HttpMethod.Head;

            if (body != null && noBodyMethod)
            {
                if (body is not ObjectBody objectBody)
                    throw new InvalidOperationException($"[Network] Query #{query.Id}: {method} cannot have a {body.GetType().Name}.");
                if (!bodyProvider.CanFlatten)
                    throw new InvalidOperationException($"[Network] Query #{query.Id}: body provider {bodyProvider.GetType().Name} cannot flatten an object into URL parameters.");
                foreach (var pair in bodyProvider.Flatten(objectBody.Value))
                    MergeParameter(parameters, pair.Key, ValueFormatter.Format(pair.Key, pair.Value), query.Id);
            }
            else if (body != null)
            {
                segments = BuildBody(query, body, bodyProvider, out contentType, out immediateOutcome, out immediateMessage);
            }

            // --- Заголовки ---
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in _settings.DefaultHeaders)
                if (header != null && !string.IsNullOrWhiteSpace(header.name))
                    headers[header.name] = header.value ?? string.Empty;
            foreach (var header in query.Headers)
                headers[header.Key] = header.Value;

            if (contentType != null && headers.ContainsKey("Content-Type"))
                throw new InvalidOperationException($"[Network] Query #{query.Id}: Content-Type header conflicts with the body's own content type.");

            var unsettable = Driver.UnsettableHeaders;
            foreach (var name in headers.Keys)
                if (ContainsIgnoreCase(unsettable, name))
                    throw new InvalidOperationException($"[Network] Query #{query.Id}: header '{name}' cannot be set by the driver on this platform.");

            if (!headers.ContainsKey("User-Agent") && !string.IsNullOrEmpty(_settings.UserAgent)
                                                   && !ContainsIgnoreCase(unsettable, "User-Agent"))
                headers["User-Agent"] = _settings.UserAgent;

            // --- Скачивание в файл ---
            if (query.DownloadPath != null && !Driver.SupportsDownloadToFile)
                throw new InvalidOperationException($"[Network] Query #{query.Id}: driver does not support downloading to a file.");

            // --- Повторы ---
            var policy = _settings.DefaultRetryPolicy.Clone();
            query.RetryEdit?.Invoke(policy);
            policy.Validate($"query #{query.Id}");

            var finalUrl = BuildUrl(uri, parameters, null);
            query.FinalUrl = finalUrl;
            query.BodyProvider = bodyProvider;

            return new Prepared
            {
                Method = method,
                Url = finalUrl,
                LogUrl = BuildUrl(uri, parameters, _settings.MaskedLogKeys),
                Headers = headers,
                ContentType = contentType,
                Body = segments,
                Auth = auth,
                Timeout = query.Timeout ?? _settings.DefaultTimeout,
                RetryAllowed = query.RetryAllowed ?? method == HttpMethod.Get,
                RetryPolicy = policy,
                DownloadPath = query.DownloadPath,
                TempDownloadPath = query.DownloadPath == null ? null : query.DownloadPath + ".part"
            };
        }

        private static List<BodySegment> BuildBody(Query query, RequestBody body, IBodyProvider provider,
            out string contentType, out QueryOutcome immediateOutcome, out string immediateMessage)
        {
            immediateOutcome = QueryOutcome.None;
            immediateMessage = null;
            var segments = new List<BodySegment>();

            switch (body)
            {
                case ObjectBody objectBody:
                    segments.Add(BodySegment.FromBytes(provider.Serialize(objectBody.Value)));
                    contentType = provider.ContentType;
                    break;

                case BytesBody bytesBody:
                    segments.Add(BodySegment.FromBytes(bytesBody.Bytes));
                    contentType = bytesBody.ContentType;
                    break;

                case TextBody textBody:
                    segments.Add(BodySegment.FromBytes(Encoding.UTF8.GetBytes(textBody.Text)));
                    contentType = textBody.ContentType;
                    break;

                case FormBody formBody:
                    segments.Add(BodySegment.FromBytes(Encoding.UTF8.GetBytes(EncodePairs(formBody.Fields))));
                    contentType = "application/x-www-form-urlencoded";
                    break;

                case MultipartBody multipart:
                    var boundary = "----VortexBoundary" + Guid.NewGuid().ToString("N");
                    contentType = "multipart/form-data; boundary=" + boundary;
                    BuildMultipart(query, multipart, boundary, segments, ref immediateOutcome, ref immediateMessage);
                    break;

                default:
                    throw new InvalidOperationException($"[Network] Query #{query.Id}: unsupported body {body.GetType().Name}.");
            }

            if (immediateOutcome == QueryOutcome.None)
            {
                long size = 0;
                foreach (var segment in segments)
                    size += segment.Bytes?.LongLength ?? new FileInfo(segment.FilePath).Length;
                if (size > _settings.MaxRequestBodyBytes)
                {
                    immediateOutcome = QueryOutcome.TooLarge;
                    immediateMessage = $"Request body {size} bytes exceeds the limit {_settings.MaxRequestBodyBytes}.";
                }
            }

            return segments;
        }

        private static void BuildMultipart(Query query, MultipartBody multipart, string boundary, List<BodySegment> segments,
            ref QueryOutcome immediateOutcome, ref string immediateMessage)
        {
            foreach (var part in multipart.Parts)
            {
                var head = new StringBuilder();
                head.Append("--").Append(boundary).Append("\r\n");
                head.Append("Content-Disposition: form-data; name=\"").Append(Quote(part.Name)).Append('"');
                if (!part.IsText)
                {
                    head.Append("; filename=\"").Append(Quote(part.FileName ?? part.Name)).Append('"');
                    head.Append("\r\nContent-Type: ").Append(part.MimeType);
                }

                head.Append("\r\n\r\n");
                if (part.IsText)
                    head.Append(part.Text);
                segments.Add(BodySegment.FromBytes(Encoding.UTF8.GetBytes(head.ToString())));

                if (part.Bytes != null)
                {
                    segments.Add(BodySegment.FromBytes(part.Bytes));
                }
                else if (part.FilePath != null)
                {
                    if (!Driver.SupportsFileBodySegments)
                        throw new InvalidOperationException($"[Network] Query #{query.Id}: driver does not support sending files as a stream.");
                    if (!File.Exists(part.FilePath))
                    {
                        immediateOutcome = QueryOutcome.BodyError;
                        immediateMessage = $"Multipart file not found: {part.FilePath}";
                        return;
                    }

                    segments.Add(BodySegment.FromFile(part.FilePath));
                }

                segments.Add(BodySegment.FromBytes(Encoding.UTF8.GetBytes("\r\n")));
            }

            segments.Add(BodySegment.FromBytes(Encoding.UTF8.GetBytes("--" + boundary + "--\r\n")));
        }

        private static string Quote(string value) => value.Replace("\"", "%22").Replace("\r", "").Replace("\n", "");

        #region Адрес

        private static bool MatchesAny(IReadOnlyList<Vortex.Core.MaskSystem.Mask> masks, string normalized)
        {
            foreach (var mask in masks)
                if (mask.IsMatch(normalized))
                    return true;
            return false;
        }

        private static T FirstMatch<T>(IReadOnlyList<MaskBinding<T>> rules, string normalized) where T : class
        {
            foreach (var rule in rules)
                if (rule.Mask.IsMatch(normalized))
                    return rule.Value;
            return null;
        }

        private static List<KeyValuePair<string, string>> ParseQueryString(string query)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(query) || query == "?")
                return result;

            foreach (var pair in query.TrimStart('?').Split('&'))
            {
                if (pair.Length == 0)
                    continue;
                var separator = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(separator < 0 ? pair : pair.Substring(0, separator));
                var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair.Substring(separator + 1));
                result.Add(new KeyValuePair<string, string>(key, value));
            }

            return result;
        }

        private static void MergeParameter(List<KeyValuePair<string, string>> parameters, string key, string value, long queryId)
        {
            for (var i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Key != key)
                    continue;
                LogWarning($"#{queryId}: parameter '{key}' already present in the URL is overwritten.");
                parameters[i] = new KeyValuePair<string, string>(key, value);
                return;
            }

            parameters.Add(new KeyValuePair<string, string>(key, value));
        }

        /// <summary>Итоговый адрес. С <paramref name="maskedKeys"/> — версия для лога со скрытыми значениями.</summary>
        private static string BuildUrl(Uri uri, List<KeyValuePair<string, string>> parameters, IReadOnlyList<string> maskedKeys)
        {
            var builder = new StringBuilder(uri.GetLeftPart(UriPartial.Path));
            for (var i = 0; i < parameters.Count; i++)
            {
                var value = maskedKeys != null && IsMasked(maskedKeys, parameters[i].Key) ? "***" : parameters[i].Value;
                builder.Append(i == 0 ? '?' : '&')
                    .Append(Uri.EscapeDataString(parameters[i].Key)).Append('=')
                    .Append(Uri.EscapeDataString(value));
            }

            return builder.ToString();
        }

        private static string EncodePairs(IReadOnlyList<KeyValuePair<string, string>> pairs)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < pairs.Count; i++)
            {
                if (i > 0)
                    builder.Append('&');
                builder.Append(Uri.EscapeDataString(pairs[i].Key)).Append('=').Append(Uri.EscapeDataString(pairs[i].Value));
            }

            return builder.ToString();
        }

        private static bool ContainsIgnoreCase(IReadOnlyCollection<string> names, string name)
        {
            if (names == null)
                return false;
            foreach (var item in names)
                if (string.Equals(item, name, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool IsMasked(IReadOnlyList<string> maskedKeys, string key)
        {
            foreach (var masked in maskedKeys)
                if (string.Equals(masked, key, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        #endregion
    }
}

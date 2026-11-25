using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;
using Vortex.Core.NetworkSystem;

namespace Vortex.Unity.NetworkSystem.Drivers.Uwr
{
    /// <summary>
    /// Одна попытка на UnityWebRequest. Тело: байты — из памяти; есть файловые сегменты — все сегменты склеиваются во
    /// временный файл и отправляются из него (UnityWebRequest не умеет составное тело из файла). Ответ — в память или
    /// в файл по пути от ядра. Встроенный таймаут Unity = 0: таймаут, отмену и освобождение ведёт ядро.
    /// </summary>
    internal sealed class UwrAttempt : INetworkAttempt
    {
        private readonly UnityWebRequest _request;
        private readonly bool _toFile;
        private string _tempBodyPath;
        private UnityWebRequestAsyncOperation _operation;

        public UwrAttempt(PreparedAttempt attempt, int redirectLimit, string tempFolder)
        {
            _request = new UnityWebRequest(attempt.Url, MethodName(attempt.Method)) { timeout = 0 };
            if (redirectLimit >= 0)
                _request.redirectLimit = redirectLimit;

            foreach (var header in attempt.Headers)
                _request.SetRequestHeader(header.Key, header.Value);

            if (attempt.Body.Count > 0)
                _request.uploadHandler = CreateUploadHandler(attempt, tempFolder);

            _toFile = attempt.DownloadPath != null;
            _request.downloadHandler = _toFile
                ? new DownloadHandlerFile(attempt.DownloadPath) { removeFileOnAbort = true }
                : new DownloadHandlerBuffer();
        }

        public void Start() => _operation = _request.SendWebRequest();

        public bool IsDone => _operation != null && _operation.isDone;

        public float UploadProgress => Math.Max(0f, _request.uploadProgress);

        public float DownloadProgress =>
            string.IsNullOrEmpty(_request.GetResponseHeader("Content-Length")) ? 0f : Math.Max(0f, _request.downloadProgress);

        public long DownloadedBytes => (long)_request.downloadedBytes;

        public AttemptResult GetResult()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var raw = _request.GetResponseHeaders();
            if (raw != null)
                foreach (var header in raw)
                    headers[header.Key] = header.Value;

            var transportFailed = _request.result is UnityWebRequest.Result.ConnectionError
                or UnityWebRequest.Result.DataProcessingError;

            return new AttemptResult(
                _request.responseCode,
                headers,
                _toFile ? null : _request.downloadHandler.data,
                transportFailed ? TransportError.Network : TransportError.None,
                transportFailed ? _request.error : null);
        }

        public void Abort() => _request.Abort();

        public void Dispose()
        {
            _request.Dispose();
            if (_tempBodyPath != null && File.Exists(_tempBodyPath))
                File.Delete(_tempBodyPath);
        }

        private UploadHandler CreateUploadHandler(PreparedAttempt attempt, string tempFolder)
        {
            var hasFiles = false;
            long total = 0;
            foreach (var segment in attempt.Body)
            {
                if (segment.FilePath != null)
                    hasFiles = true;
                else
                    total += segment.Bytes.LongLength;
            }

            UploadHandler handler;
            if (!hasFiles)
            {
                var bytes = new byte[total];
                var offset = 0;
                foreach (var segment in attempt.Body)
                {
                    Buffer.BlockCopy(segment.Bytes, 0, bytes, offset, segment.Bytes.Length);
                    offset += segment.Bytes.Length;
                }

                handler = new UploadHandlerRaw(bytes);
            }
            else
            {
                Directory.CreateDirectory(tempFolder);
                _tempBodyPath = Path.Combine(tempFolder, $"{attempt.QueryId}-{attempt.AttemptNumber}-{Guid.NewGuid():N}.body");
                using (var output = File.Create(_tempBodyPath))
                {
                    foreach (var segment in attempt.Body)
                    {
                        if (segment.FilePath == null)
                        {
                            output.Write(segment.Bytes, 0, segment.Bytes.Length);
                            continue;
                        }

                        using var input = File.OpenRead(segment.FilePath);
                        input.CopyTo(output);
                    }
                }

                handler = new UploadHandlerFile(_tempBodyPath);
            }

            handler.contentType = attempt.ContentType;
            return handler;
        }

        private static string MethodName(HttpMethod method) => method switch
        {
            HttpMethod.Get => UnityWebRequest.kHttpVerbGET,
            HttpMethod.Post => UnityWebRequest.kHttpVerbPOST,
            HttpMethod.Put => UnityWebRequest.kHttpVerbPUT,
            HttpMethod.Delete => UnityWebRequest.kHttpVerbDELETE,
            HttpMethod.Head => UnityWebRequest.kHttpVerbHEAD,
            HttpMethod.Patch => "PATCH",
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };
    }
}

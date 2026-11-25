# NetworkSystem (Core)

**Namespace:** `Vortex.Core.NetworkSystem`
**Assembly:** `ru.vortex.core.networksystem`
**Status:** experimental — the API may change without backward compatibility.

## Purpose

A single entry point for HTTP requests. The controller (`NetworkController`, a `SystemController` with a driver) handles:
- the queue and the concurrent-request limit;
- attempts, retries, authorization;
- outcomes and the registry of active requests.

The driver is a logic-free bridge to the transport. Drivers (UnityWebRequest, a mock simulator) live in a separate package.

- A `Query` is built as a chain and locked on send.
- An outcome is a category, not an exception. Exceptions are thrown only for programmer and configuration errors (fail-fast).
- Retries follow a policy: transient errors only, growing delay with jitter, `Retry-After`.
- The body is sent through a provider (selected by URL mask) and read on demand into any class.
- Authorization uses a mask whitelist. The token comes from game code via a token source; no secrets in settings.
- Environments (dev/stage/prod) and named hosts are defined in settings.

## Dependencies

- `ru.vortex.system`, `ru.vortex.extensions`, `ru.vortex.logger`, `ru.vortex.app`, `ru.vortex.settings`;
- `ru.vortex.core.masksystem`;
- UniTask.

## Usage

```csharp
var query = new Query()
    .SetMethod(HttpMethod.Get)
    .SetHost("api", "/v2/banners")          // named host from settings; or .SetUrl("https://…")
    .AddParam("lang", "ru").AddParam("scale", 1.5f)   // invariant culture: scale=1.5
    .OnOutcome(o => Debug.Log(o))
    .Send();

await query.Completion;
query.GetResult();                           // take ownership: removes from the registry (after completion, once)
if (query.Outcome.Value == QueryOutcome.Success && query.TryRead<BannerConfig>(out var config, out var error))
    Use(config);

// Simple methods: HTTP method from settings; retries only for GET
NetworkController.Send("https://example.com/log", new LogEntry());          // fire-and-forget, auto-release
var q = await NetworkController.SendAsync(UrlData.FromHost("api", "/ping"), null);  // waits, releases, returns Query

// Authorization: the key is set on the auth provider in settings
NetworkController.RegisterTokenSource("partnerApi", async (forceNew, ct) => await Auth.GetToken(forceNew, ct));
```

## Model

```
Query
├── parameters (locked after Send): method, UrlData, headers, body, timeout, retries, auto-release,
│   download to file, cancellation token, handlers OnState / OnOutcome / OnUploadProgress / OnDownloadProgress
├── State    : EnumData<QueryState>    Draft → Queued → Running ⇄ RetryWait → Completed → Released
├── Outcome  : EnumData<QueryOutcome>  None, Success, NotModified, Network, Timeout, ClientError, ServerError,
│                                       TooLarge, BodyError, Cancelled, NetworkDisabled
├── UploadProgress / DownloadProgress : FloatData (unknown size — 0, then 1)
├── Result   : QueryResult             code, headers, body/file, duration, attempts, message
└── TryRead<T>, GetResult, Release, CloneDraft
```

Reactive fields are written by the controller only (owner-lock). Each handler is invoked right after its field changes. Completion order: outcome → outcome handler → `Completed` → state handler → `Completion`.

## Contracts

- `INetworkSettings` — connection settings. Data only; logic (environment, masks, validation) is in the controller.
- `INetworkDriver` — the driver. Provides settings, declares capabilities (`Supports…` properties and the `UnsettableHeaders` set), creates attempt objects.
- `INetworkAttempt` — an attempt object: `Start`, progress, `IsDone`, `GetResult`, `Abort`, `Dispose`. Timeout, cancellation and release are handled by the controller.
- `IBodyProvider` — object ⇄ bytes, flattening for GET.
- `IAuthProvider` — headers from a token.
- `TokenSource` — token issuing (`forceNew` after 401).

## Limitations

- Main thread only.
- The driver is assigned once at startup; there is no runtime driver swap.
- After adding a driver to DriverConfig: Reload → choose the driver for `NetworkController` → Save Config.
- Logs: detailed ones with `DebugSettings → networkLogs`. Values of `MaskedLogKeys` are hidden, bodies are truncated.

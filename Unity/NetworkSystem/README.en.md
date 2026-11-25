# NetworkSystem (Unity)

**Namespace:** `Vortex.Unity.NetworkSystem`
**Assemblies:**
- `ru.vortex.unity.networksystem`;
- `ru.vortex.unity.networksystem.newtonsoft` — compiled only when `com.unity.nuget.newtonsoft-json` is installed.

**Status:** experimental — the API and inspector may change without backward compatibility.

## Purpose

Unity layer of the network module (core — `ru.vortex.core.networksystem`, see its README).

- `NetworkSettingsAsset` — abstract base asset for connection settings. Implements `INetworkSettings`, holds the common fields and inspector sugar. Driver assets inherit it and add their transport parameters.
- `JsonUtilityBodyProvider` — default body provider.
- `NewtonsoftBodyProvider` — Newtonsoft.Json body provider: naming style, null skipping.
- `BearerAuthProvider` — `Authorization: Bearer <token>`, token source key.
- `networkLogs` in `DebugSettings` — detailed module logs.

## UnityWebRequest driver

- `UwrNetworkDriver` — logic-free bridge from the core to UnityWebRequest. Registers itself; the core accepts it only when it is selected in DriverConfig.
- `UwrNetworkSettings` — the driver asset, auto-created in `Resources/Settings`. Adds a redirect limit (32) to the common fields.
- **Body:** bytes are sent from memory. If there are multipart file parts, the whole body is concatenated into a temp file in `temporaryCachePath/VortexNetwork/` and streamed. The file is deleted after the attempt; the folder is cleaned on startup.
  - Concatenation runs synchronously when the attempt is created, so very large files may cause a frame hitch.
- **Unity's built-in timeout = 0** — the core handles timeouts.
- **Errors:** `ConnectionError` and `DataProcessingError` give `Network`; the core classifies HTTP codes.

| Capability | Desktop & mobile | WebGL |
|---|---|---|
| Download to file / file body streaming | yes | no |
| Upload and download progress | yes (download — only with `Content-Length`) | yes |
| Redirect limit | yes | no (browser) |
| Unsettable headers | Accept-Charset, Accept-Encoding, Access-Control-Request-*, Connection, Content-Length, Date, Dnt, Expect, Host, Keep-Alive, Origin, Referer, Te, Trailer, Transfer-Encoding, Upgrade, Via, X-Unity-Version | same + Cookie, Cookie2, User-Agent |

- **WebGL.** WebGL values are to be confirmed on a test build.
- **CORS.** The server must send `Access-Control-*` headers, or the browser blocks the response.

The mock simulator is a separate assembly `ru.vortex.unity.networksystem.mock`, see `Mock/README.en.md`.

**Switching drivers:** DriverConfig → Reload → driver for `NetworkController` → Save Config.

## Asset fields

| Group | Fields |
|---|---|
| Environments | environment list (key → hosts "name → URL"), active environment — dropdown of keys |
| Requests | attempt timeout (30 s), concurrent limit (4), simple-request method (POST), default headers, retry policy (3 attempts; 1 s × 2, max 30 s; jitter 0.2; `Retry-After`; retry on Network, Timeout, 408, 429, 500–599) |
| Limits | request body (10 MB), response (20 MB) |
| Security & logs | http-allowed hosts (masks), masked keys (`Authorization`), logged body length (1024) |
| Body providers | default (JsonUtility), "mask → provider" exceptions |
| Authorization | "mask → auth provider" whitelist |

- Masks are set via `[MaskSelector]` (mask package): error highlighting and a test string.
- Masks are checked against `scheme://host[:port]/path` without parameters.
- User-Agent is built automatically (`Application.productName/version`, platform). A default `User-Agent` header overrides it.

## Limitations

- An empty provider in a rule or as the default, or a missing type (Newtonsoft removed), fails fast on initialization.
- No secrets in the asset: game code registers the token (`NetworkController.RegisterTokenSource`).
- `JsonUtilityBodyProvider` follows JsonUtility rules: fields, not properties; no dictionaries or top-level arrays. Use `NewtonsoftBodyProvider` for such DTOs.

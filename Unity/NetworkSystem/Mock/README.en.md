# NetworkSystem Mock (Unity)

**Namespace:** `Vortex.Unity.NetworkSystem.Mock`
**Assembly:** `ru.vortex.unity.networksystem.mock`
**Status:** experimental — the API and rule format may change without backward compatibility.

## Purpose

A mock driver for the network module: answers with configured rules instead of the network. Use it to test without a server:
- offline;
- server errors;
- slow network;
- retries ("503, 503, 200");
- token refresh ("401 → 200");
- limits.

The core logic is the same with any driver.

## Conditional compilation

- The assembly always compiles. The driver, asset, models and logic are under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- **In a release build the mock driver type does not exist.** If the mock is selected in DriverConfig, no driver connects and the first request fails fast ("network not initialized").
- **`MockNetworkPanel` exists in every build:** no Missing Script, but its methods are empty in release.
- **`MockNetworkSettings`** lives in `Resources/Settings`. In release its type is missing and the asset stays without a script. This does not affect anything.

## Rules (`MockNetworkSettings`)

- **Default reaction** — for requests with no matching rule: 404 and a "no rule" warning.
- **Rules** are checked top to bottom. Rule fields:
  - id — for the panel, non-empty and unique;
  - enabled;
  - URL mask (`[MaskSelector]`, `scheme://host[:port]/path`);
  - method — any or a specific one;
  - **reaction sequence**: attempt `n` of a request gets reaction `n`, then the last one; the retry after 401 counts too;
  - random failure percent — gives a transport error.
- **Reaction:**
  - kind: response / transport error / no response (until the core timeout);
  - code, headers;
  - body: `TextAsset` or text;
  - delay (0.1 s by default);
  - transfer duration — progress moves smoothly 0 → 1.
- **Rule errors** fail fast on initialization: empty or duplicate id, invalid mask, no reactions, code outside 100…599.

## Panel (`MockNetworkPanel`)

A `MonoBehaviourSingleton` with public methods for `UnityEvent`. Build the UI from `UIComponent` buttons + `SetActionComponent`.

| Method | Effect |
|---|---|
| `SetOffline(bool)`, `ToggleOffline()` | all requests get a transport error |
| `SetLatencyMultiplier(float)` | multiplier for delays and transfer |
| `EnableRule(string)`, `DisableRule(string)` | enable or disable a rule on top of the asset |
| `ForceStatus(int)` | all requests get this code with an empty body |
| `ForceTransportError()` | all requests get a transport error |
| `ClearForced()` | clear the forced code and error |
| `ResetAll()` | reset all overrides |

- **Priority:** offline → forced error → forced code → rules → default reaction.
- **Overrides** live only for the current run and are not written to the asset.
- **The active driver is not the mock** — methods log a warning and change nothing.

## Capabilities

- All capabilities are `true`; there are no unsettable headers.
- The reachability hint is "not offline".
- The debug log (`DebugSettings → networkLogs`) shows which rule answered and on which attempt.

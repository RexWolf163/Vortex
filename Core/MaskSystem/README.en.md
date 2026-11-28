# MaskSystem (Core)

**Namespace:** `Vortex.Core.MaskSystem`
**Assembly:** `ru.vortex.core.masksystem`
**Platform:** .NET Standard 2.1+

---

## Purpose

String matching rules ("masks"): answer "does this string match the rule?" and, for some kinds, extract named parts of the string. A match always covers the **whole** string.

Capabilities:

- `Mask` — abstract rule defined by operations only: check (`IsMatch`), match with captures (`Match`), validate own settings (`Validate`)
- `MatchResult` — match flag and read-only captures "name → value"
- `ValidationResult` — settings validity, message and error position in the pattern
- Three static controllers with the matching logic of each kind:
  - `StringEqualController` — preparation of the input copy and the pattern (trim, NFC); string equals a pattern
  - `UrlController` — input parsing into an address; address parts (scheme, host, port, path) match the settings
  - `RegexController` — string matches a .NET regular expression
- Setting enums: `StringCompareMode`, `TrimMode`, `SchemeCheckMode`, `HostCheckMode`, `PortCheckMode`, `PathCheckMode`

Out of scope:

- Mask settings and their storage — Unity package `ru.vortex.unity.masksystem`
- Order of checking mask lists and "first match" — the list holder's responsibility
- Network access: the URL kind only knows URL syntax

---

## Applicability

A mask is a **predicate over a ready string**: the rule is set by the developer (in the inspector), the string is passed by code, the answer is "matches as a whole / no" and, for regex, captures.

**Use it when** all of the following hold:

- the rule is set by the developer, not by the player or an unchecked external source;
- the string is complete (identifier, name, key, address from code or data) — not typed character by character;
- you need a "matches / no" answer for the whole string, optionally with named parts (regex);
- the rule is validated at the consumer's initialization (`Validate`); an invalid one stops it.

Typical cases: "URL mask → provider" rules, allowed host lists, selecting records and assets by name or identifier, splitting a key into parts with captures.

**Do not use it for:**

| Task | Why it does not fit | Use instead |
|---|---|---|
| Player input control (input field, per-character filter) | No check for an unfinished string: "h", "ht"… are always "no match". Player input is outside the threat model (regex has no timeout). No rejection reason to show the player | Input field filters (`Vortex.Unity.UI.TextInputSystem`); for expressions — `RegexController` directly |
| Bringing a value to a storage form (trim, normalization) | A mask only answers "yes / no" and does not return the prepared string | `StringEqualController.TryPrepareInput` or the value owner's own preparation |
| Parsing into a type: numbers, dates, ranges | No such kinds | Parsing (`TryParse`) by the value owner |
| Substring search, all occurrences, replacement | A match always covers the whole string | `System.Text.RegularExpressions` |
| Expressions from an untrusted source or long player input against regex | No match timeout | `Regex` with a timeout in the consumer |
| Content protection: HTML/SQL sanitizing, address reachability, DNS, certificates | The URL kind only knows URL syntax | Dedicated networking and security tools |
| Bulk matching every frame | The URL kind parses the address on every call (allocations) | Result cache in the consumer |
| A mask created by code at runtime | Settings are inspector-only | The kind's controller directly with parameters |
| Matching off the main thread | No synchronization | — |

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Vortex.Core.LoggerSystem` | `Log.Print` before fast-fail exceptions |
| `Vortex.Core.System` | base type of `Log` (`SystemController`) |

---

## Usage

Masks are configured in the inspector through the Unity package. The consumer validates its masks during its own initialization and fails fast on an invalid one:

```csharp
[SerializeReference] private Mask hostRule;

private void Init()
{
    var validation = hostRule.Validate();
    if (!validation.IsValid)
        throw new InvalidOperationException(validation.Message);
}

public bool IsPartnerHost(string url) => hostRule.IsMatch(url);
```

Captures (regex kind):

```csharp
var result = mask.Match("api.partner.com/v2/x");
if (result.IsMatch && result.Captures.TryGetValue("ver", out var version))
    Log.Print(LogLevel.Common, $"API version {version}", this);
```

Controllers can be called directly with prepared data, for example for a custom mask kind:

```csharp
var validation = RegexController.Validate(pattern);
var regex = RegexController.Build(pattern, ignoreCase: false);
var result = RegexController.Match(regex, input);
```

---

## Architecture

```
Mask (abstract, [Serializable])
  ├── IsMatch(string) → bool
  ├── Match(string)   → MatchResult { IsMatch, Captures }
  └── Validate()      → ValidationResult { IsValid, Message, Position }

StringEqualController (static)
  ├── Validate(pattern, trimMode, compareMode, normalize)
  ├── FindInvalidUnicode(value) → index of a lone surrogate or -1
  ├── TryPrepareInput(input, trimMode, normalize, out ready) → input copy after trim and NFC
  ├── PreparePattern(pattern, normalize) → pattern for comparison (NFC per settings)
  └── IsMatch(ready, pattern, compareMode, ignoreCase)

UrlController (static)
  ├── ValidateScheme / ValidateHost / ValidatePort / ValidatePath, FirstInvalid(...)
  ├── ToIdnHost(host) → ASCII form of a validated host
  ├── TryParseInput(input, out uri) → input parsed as a "scheme://…" address
  ├── IsSchemeMatch / IsHostMatch / IsPortMatch / IsPathMatch (over System.Uri)
  └── IsAll(scheme, host, port, path) → all checked parts matched

RegexController (static)
  ├── Validate(pattern)
  ├── Build(pattern, ignoreCase) → Regex anchored to the whole string
  ├── IsMatch(regex, input)
  └── Match(regex, input) → MatchResult with captures
```

The abstraction holds no settings. Concrete mask classes (Unity package) hold the settings and caches and call the controller of their kind; the input is prepared by the controller (`StringEqualController.TryPrepareInput`, `UrlController.TryParseInput`).

---

## Contract

### `Mask`

1. `IsMatch(input)` equals `Match(input).IsMatch` for any string.
2. No match ⇒ captures are empty.
3. `input == null` is a programmer error.
4. The input is not modified.
5. Settings are validated with `Validate` before use; matching on invalid settings is a programmer error (fast-fail).

### Matching rules

| Kind | Rule |
|---|---|
| String equality | The prepared input equals the pattern. `Ordinal` — exact characters; `InvariantCulture` — invariant culture rules (ignorable characters such as U+200B, U+00AD are not counted; canonically equal forms match). Case is a separate flag. An empty pattern matches only an empty string |
| URL | Only checked parts are compared; the address matches if all checked parts match. Scheme and host — case-insensitive. Hosts are compared in ASCII form (`IdnHost`, IDN in punycode). "Host and subdomains" — on a label boundary. The scheme's default port equals an explicit one. Path — escaped form; prefix — on a segment boundary. Query and fragment are not checked |
| Regex | .NET syntax, implicit anchoring of the whole expression (`\A(?:…)\z`), `CultureInvariant`. Named groups — key by name, unnamed — by number; group 0 and groups that did not participate are excluded. No match timeout |

### Settings validation

- String equality: a pattern must be set; a whitespace character on a trimmed edge of the pattern is an error (position — its index); with normalization a pattern that cannot be normalized is an error.
- URL: a value must equal its canonical form in an address (`System.Uri`); otherwise the message suggests the form to use. A host must have a valid ASCII form and equal it or its Unicode round-trip. An IP address cannot be used with "host and subdomains". Port 1–65535. Path starts with `/`.
- Regex: the raw expression and the anchored expression must both compile.
- Unknown enum values are an error.

### Errors

| Situation | Behavior |
|---|---|
| Programmer error or invalid settings passed to a controller | `Log.Print(Error)` + `ArgumentException` / `ArgumentNullException` / `ArgumentOutOfRangeException` |
| Input data that cannot be matched (unparsable URL, invalid Unicode, host without a valid ASCII form) | "no match", no exception |

### Constraints

| Constraint | Reason |
|---|---|
| Main thread only | No synchronization |
| No `RegexOptions.Compiled` | AOT platforms (IL2CPP, WebGL) |
| No device-culture comparison | Results must not depend on the player's language |
| `ValidationResult` is created by controllers only | Factories are `internal` |
| A URL input must be `scheme://…` without `\`, control characters or edge spaces; parsing must keep the written scheme | `System.Uri` is more lenient than an HTTP stack and could read such input differently from the consumer |

---

## Edge Cases

| Situation | Behavior |
|---|---|
| `Log` driver not connected when a controller fails fast | `NullReferenceException` from `Log.Print` instead of the original exception |
| Regex with a trailing `#` comment in `x` mode | Validation error: the comment swallows the closing anchor; end the comment with a newline |
| `IsHostMatch` with a host not obtained via `ToIdnHost` | Comparison may fail: hosts are compared in ASCII form |
| `Is*Match` with an address not obtained via `TryParseInput` | URL input constraints do not apply: the address is compared as is |
| Input host with characters that map to `;`, `,`, `|`, `%` or space in ASCII form | No match |
| `file:///C:/x` on Windows | `AbsolutePath` has no leading `/`; exact/prefix path settings do not reach it |
| Non-standard scheme (`foo://127.1/`) | `System.Uri` does not canonicalize the host; settings in canonical form do not match it |

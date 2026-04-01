# FileSystem

**Namespace:** `Vortex.Unity.FileSystem`
**Assembly:** `ru.vortex.unity.filesystem`

## Purpose

Platform-independent determination and creation of the application's file output directory.

Capabilities:
- Automatic storage path resolution at application startup
- On Android — access to the Downloads folder via Java interop with permission requests
- On other platforms — `_OutputFiles` folder adjacent to the application root
- Directory creation at arbitrary paths

Out of scope:
- File reading and writing
- Permission management (except `WRITE_EXTERNAL_STORAGE` on Android)
- Working with `Application.persistentDataPath`

## Dependencies

No external dependencies. The assembly is standalone.

---

## Architecture

```
FileSystem/
├── Bus/
│   └── File.cs                    # Static API: GetAppPath(), CreateFolders()
└── Controllers/
    └── AndroidPathResolver.cs     # Android interop: Downloads, WRITE_EXTERNAL_STORAGE
```

### File (static class)

File system access bus. Initialized automatically via `[RuntimeInitializeOnLoadMethod]`.

Path resolution:
- Takes `Application.dataPath`, strips the last component
- Replaces it with `_OutputFiles`
- On Android (not in editor) — overrides the path via `AndroidPathResolver.GetAndroidPath()`

The path is computed once and cached.

### AndroidPathResolver (internal)

Active only under `#if UNITY_ANDROID && !UNITY_EDITOR`. Uses `AndroidJavaClass` to call `android.os.Environment.getExternalStoragePublicDirectory("Download")`. Before access, checks and requests `android.permission.WRITE_EXTERNAL_STORAGE`.

---

## API

| Method | Signature | Description |
|--------|-----------|-------------|
| `File.GetAppPath()` | `public static string` | Path to the output directory (cached) |
| `File.CreateFolders(directory)` | `public static void` | Creates directory if it does not exist |

---

## Platform Behavior

| Platform | Path |
|----------|------|
| Windows / macOS / Linux | `{AppRoot}/_OutputFiles` |
| Android (device) | `/storage/emulated/0/Download` (or equivalent) |
| Android (editor) | `{AppRoot}/_OutputFiles` |

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `GetAppPath()` before initialization | Lazy initialization on first call |
| `_path` remains `null` after initialization | Returns empty string |
| `CreateFolders()` — directory exists | Idempotent, no action taken |
| Android — permission already granted | `checkSelfPermission` returns 0, request skipped |
| Android — permission denied | `requestPermissions` invoked, path returned regardless |

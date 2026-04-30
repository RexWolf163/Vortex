# FileSystem

**Namespace:** `Vortex.Unity.FileSystem.Bus`
**Assembly:** `ru.vortex.unity.filesystem`

## Purpose

Platform-independent resolution of the application's file output path and directory creation.

Capabilities:
- Automatic storage path resolution at application startup
- In editor — `_OutputFiles` folder adjacent to the project root
- On device — `Application.persistentDataPath`
- Directory creation at arbitrary paths

Out of scope:
- File reading and writing
- Permission management
- Platform path resolution (beyond `GetAppPath`)

## Dependencies

No external dependencies. The assembly is standalone.

---

## Architecture

```
FileSystem/
├── Bus/
│   └── FileBus.cs                 # Static API: GetAppPath(), CreateFolders()
└── Controllers/
    └── AndroidPathResolver.cs     # [Obsolete] Android interop (not used)
```

### FileBus (static class)

File system access bus. Initialized automatically via `[RuntimeInitializeOnLoadMethod]`.

Path resolution:
- **In editor:** takes `Application.dataPath`, strips the last component, replaces it with `_OutputFiles`
- **On device:** `Application.persistentDataPath`

The path is computed once and cached in `_path`. If `GetAppPath()` is called before initialization, lazy initialization runs.

### AndroidPathResolver (internal, Obsolete)

Marked `[Obsolete]`. All implementation code is commented out. Not in use.

---

## API

| Method | Signature | Description |
|--------|-----------|-------------|
| `FileBus.GetAppPath()` | `public static string` | Path to the output directory (cached) |
| `FileBus.CreateFolders(directory)` | `public static void` | Creates directory if it does not exist |

---

## Platform Behavior

| Platform | Path |
|----------|------|
| Editor (all platforms) | `{ProjectRoot}/_OutputFiles` |
| Device (all platforms) | `Application.persistentDataPath` |

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| `GetAppPath()` before initialization | Lazy initialization on first call |
| `_path` remains `null` after initialization | Returns empty string |
| `CreateFolders()` — directory exists | Idempotent, no action taken |
| `CreateFolders()` — nested directories | `Directory.CreateDirectory` creates the full chain |

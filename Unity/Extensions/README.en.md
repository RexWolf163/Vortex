# Extensions (Unity)

**Namespace:** `Vortex.Unity.Extensions.Abstractions`, `Vortex.Core.Extensions.LogicExtensions` (partial), `Vortex.Unity.Extensions.Editor`
**Assembly:** `ru.vortex.unity.extensions`

## Purpose

Unity extensions for the core: MonoBehaviour singleton abstractions, texture conversion, platform type marking for DeepCopy, editor utilities (asset search, define symbol management, code generation from templates, Odin dropdown).

- `MonoBehaviourSingleton<T>` — MonoBehaviour-based singleton
- `SoData` / `SoDataController` — ScriptableObject abstraction with property reflection
- `TextureExtBase64` — `Texture2D` ↔ Base64 conversion with GZip compression
- `SimpleTypeMarkerExtUnity` — partial extension for platform type marker
- `DefineSymbolManager` — automatic define symbol management based on package presence
- VTP Template System — code template generation and deployment

Out of scope: business logic, UI components, system drivers.

## Dependencies

- `Vortex.Core.Extensions` — `SimpleTypeMarker` (partial), `IsNullOrWhitespace`
- `Vortex.Unity.AppSystem` — `TimeController` (deferred calls in `MonoBehaviourSingleton`)
- `Sirenix.OdinInspector` — `[Button]`, `[InfoBox]`, `[TabGroup]`, `ValueDropdownList` (Editor)

---

## Abstractions

### MonoBehaviourSingleton\<T\>

```
MonoBehaviourSingleton<T> : MonoBehaviour
  where T : MonoBehaviourSingleton<T>

  ├── Instance: T (static, protected)     ← lazy via FindAnyObjectByType
  ├── Awake()                             ← SetInstance (Editor: via TimeController.Call(0))
  └── OnDestroy()                         ← _instance = null, TimeController.RemoveCall
```

MonoBehaviour-based singleton. On `Awake`, registers itself as the sole instance. On duplicate creation — `LogError`. When accessing `Instance` before `Awake` — fallback to `FindAnyObjectByType<T>()`.

In the editor, `SetInstance` is executed via `TimeController.Call(0)` — a deferred call on the next frame. This protects against desync during hot Play Mode restart.

### SoData

Abstract `ScriptableObject`. In the editor, provides a `TestFields` button — prints to console the list of read-only public properties suitable for automatic copying to `SystemModel`.

### SoDataController

Static extension class for `SoData`.

| Method | Description |
|--------|-------------|
| `GetPropertiesList()` | Returns `PropertyInfo[]` — public properties without a setter |
| `PrintFields()` | Editor-only: logs property list to console |

---

## CoreExt

### TextureExtBase64

`Texture2D` ↔ Base64 string conversion with optional GZip compression.

**Namespace:** `Vortex.Core.Extensions.LogicExtensions` (partial Core extension).

#### API

| Method | Description |
|--------|-------------|
| `texture.TextureToBase64(encodingRules, compress)` | Encode texture to Base64 string |
| `texture.Base64ToTexture(base64)` | Restore texture from Base64 string |

**`TextureToBase64` parameters:**
- `encodingRules` — format: `PNG`, `JPEGLow` (25%), `JPEGMedium` (50%), `JPEGHigh` (75%), `JPEGMax` (100%). Default: `PNG`
- `compress` — GZip compression of `byte[]` before Base64 conversion. Default: `false`

**`Base64ToTexture`** automatically detects GZip by magic bytes (`0x1F 0x8B`) — no explicit decompression parameter needed.

#### Usage

```csharp
// Without compression
var base64 = texture.TextureToBase64(TextureEncodingRules.JPEGHigh);
targetTexture.Base64ToTexture(base64);

// With compression
var base64 = texture.TextureToBase64(TextureEncodingRules.JPEGHigh, compress: true);
targetTexture.Base64ToTexture(base64); // auto-detects GZip
```

#### Edge Cases

- `texture == null` → `ArgumentNullException`
- `base64` empty or `null` → error log, returns `false`
- `LoadImage` failed to recognize format → error log, returns `false`
- PNG is already compressed — GZip on top yields minimal gain; for JPEG the gain is more noticeable
- Errors are caught with `try/catch` and `Debug.LogError`, no exceptions thrown externally

### TextureEncodingRules

```csharp
enum TextureEncodingRules { PNG, JPEGLow, JPEGMedium, JPEGHigh, JPEGMax }
```

### SimpleTypeMarkerExtUnity

Partial extension of `SimpleTypeMarker` from Core. Adds `UnityEngine.Object` as a platform primitive — all descendants (`GameObject`, `Sprite`, `Material`, ...) are not cloned in `DeepCopy`, but passed by reference.

---

## Editor

### AssetFinder

Asset search utility via `AssetDatabase`.

| Method | Description |
|--------|-------------|
| `FindAssets<T>(params string[] searchInFolders)` | All assets of type `T` in the project (or in specified folders) |
| `FindAsset<T>(params string[] searchInFolders)` | First asset of type `T` or `null` |

### MenuConfigSearchController

Navigation utility for ScriptableObject assets: `Selection.activeObject` + `PingObject`. Used in `Vortex/Configs/*` menu items.

### DefineSymbolManager

Automatic compilation define symbol management based on package presence in the project.

#### Architecture

```
DefineSymbolManager (static, Editor-only)
  ├── [InitializeOnLoadMethod] Run()        ← subscribes to Events.registeringPackages
  ├── [DidReloadScripts] OnScriptsReloaded  ← rescan
  └── UpdateDefineSymbols()                 ← Client.List → INeedPackage → PlayerSettings

INeedPackage (interface)
  ├── GetPackageName()   → "com.unity.addressables"
  └── GetDefineString()  → "ENABLE_ADDRESSABLES"

AddressablesPreBuildProcessor (IPreprocessBuildWithReport)
  └── OnPreprocessBuild() → UpdateDefineSymbols() + wait for completion (up to 2 sec)
```

Scans all `INeedPackage` implementations via reflection. For each, checks package presence in `Client.List()`. If present — adds the define symbol; if absent — removes it. Applied to current and Standalone platforms.

#### INeedPackage

Interface for declaring a package dependency:

```csharp
public class MyPackageDependency : INeedPackage
{
    public string GetPackageName() => "com.unity.addressables";
    public string GetDefineString() => "ENABLE_ADDRESSABLES";
}
```

### RichTextHelpBox

`EditorGUI.HelpBox` with Rich Text support (`<b>`, `<color>`, `<i>`).

| Method | Description |
|--------|-------------|
| `Create(Rect, string, MessageType)` | HelpBox in specified Rect |
| `Create(string, MessageType, int height)` | HelpBox via `EditorGUILayout` |

### OdinDropdownTool

Wrapper over `OdinSelector<T>` for drawing dropdown fields in custom Inspectors. Requires Odin Inspector.

| Method | Description |
|--------|-------------|
| `DropdownSelector<T>(value, items)` | Dropdown from `IEnumerable<T>` |
| `DropdownSelector<T>(label, value, dropItems, out rect)` | Dropdown from `ValueDropdownList<T>` with label and output Rect |

### DropDawnHandler

Utilities for building `ValueDropdownList` via reflection.

| Method | Description |
|--------|-------------|
| `GetTypesNameList<T>()` | All types implementing `T` (interface or class) as `ValueDropdownList<string>` by `AssemblyQualifiedName` |
| `GetScenes()` | Scenes from Build Settings as `ValueDropdownList<string>` |

---

## Editor/Templates — VTP Template System

Code generation system from `.vtp` text templates.

### .vtp Format

UTF-8 text file. Files are separated by markers:

```
//---{path/FileName.cs}---
file contents...

//---{another/File.cs}---
contents...
```

Placeholders: `{!Key!}` — replaced with values from the substitution dictionary.

### VtpTemplateGenerator

| Method | Description |
|--------|-------------|
| `Generate(templatePath, outputFolder, substitutions)` | Deploy `.vtp` template into files with substitutions |
| `CreateTemplateFromFolder(sourceFolder, outputPath, replacements)` | Create `.vtp` template from a folder with `.cs` files |

### VtpTemplateCreatorWindow

Editor window for creating `.vtp` templates from folders. Menu: **Assets → Vortex → Template Generator**.

- Source folder selection
- Reverse substitution setup (value → placeholder)
- Generates `.vtp` + `TemplateMenu.cs` (context menu script)

### VtpGeneratorWindow

Editor window for deploying templates. Enter class name → substitutes `{!ClassName!}` → generates files.

### Template Context Menus

Each template gets an auto-generated `*TemplateMenu.cs` with a menu item at **Assets → Create → Vortex Templates → {Name}**.

---

## Edge Cases

| Situation | Behavior |
|-----------|----------|
| Two `MonoBehaviourSingleton<T>` on scene | Second logs `LogError`, overwrites `_instance` |
| `Instance` before `Awake` | Fallback to `FindAnyObjectByType` |
| `TextureToBase64` with `compress: true` for PNG | Works, but minimal gain (PNG is already compressed) |
| `Base64ToTexture` with uncompressed data | GZip auto-detect doesn't trigger, data passed to `LoadImage` as-is |
| `DefineSymbolManager` — package removed | Define symbol automatically removed on next scan |
| `DefineSymbolManager` — no `INeedPackage` implementations | Nothing happens |
| VTP template without `//---{...}---` markers | Console warning, no files created |
| VTP placeholder `{!Key!}` missing from dictionary | Console warning, placeholder left as-is |

# Camera System

Orthographic camera management package: positioning, focus tracking, border constraints.

Assembly: `ru.vortex.unity.camera`  
Namespace: `Vortex.Unity.Camera`  
Layer: 2 (Unity)

## Architecture

```
CameraBus (bus)
├── Controllers (static, extension methods)
│   ├── CameraMoveController   — per-frame position and target update
│   ├── CameraFocusController  — focus group management
│   └── CameraBordersController — border management
├── Model
│   ├── CameraModel            — reactive camera data model
│   └── CameraFocusTarget      — focus object group
└── View
    ├── CameraDataStorage      — MonoBehaviour data holder (IDataStorage)
    └── Handlers
        ├── CameraHandler      — base class for camera handlers
        ├── CameraMoveHandler  — smooth camera-to-target movement
        ├── FocusHandler       — declarative focus binding
        └── BordersHandler     — declarative border binding
```

### Data Flow

1. `CameraDataStorage` registers with `CameraBus` on `Awake`
2. `CameraMoveController` subscribes to registration, takes ownership of `Position`/`Target`
3. Every `FixedUpdate` the controller updates position and target from focus data
4. `CameraMoveHandler` (View) receives `OnUpdateData`, computes final position with border constraints, and smoothly moves the camera

## CameraBus

Static camera registration bus. Key is `gameObject.name`.

```csharp
// Registration (automatic in CameraDataStorage.Awake)
CameraBus.Registration(storage);

// Retrieval
var cam = CameraBus.Get("Map Camera");        // logs error if missing
if (CameraBus.TryGet("Map Camera", out var cam)) { }  // silent
var any = CameraBus.GetAny();                 // first available (null if empty)
```

Events:
- `OnRegistration` — camera registered
- `OnRemove` — camera unregistered

## CameraModel

Reactive camera data model. Implements `IReactiveData`.

| Property | Type | Description |
|----------|------|-------------|
| `CameraRect` | `Vector2Data` | Visible area size in world units (width, height) |
| `Position` | `Vector2Data` | Current camera position |
| `Target` | `Vector2Data` | Target position (focus group center) |
| `FocusedObjects` | `IReadOnlyList<CameraFocusTarget>` | Focus group stack, last has priority |
| `Borders` | `IReadOnlyList<RectTransform>` | Border stack, last is active |
| `IsBordered` | `bool` | Whether border constraints are enabled (default: true) |

Ownership: `Position` and `Target` are owned by `CameraMoveController` — modification only through the controller.

## Controllers

### CameraMoveController

Static per-frame update controller. Runs via `TimeController.AddCallback` (FixedUpdate).

**Logic:**
- No focus: `Position` follows `transform.position` (camera controlled externally)
- With focus: `transform.position` is set from `Position` (camera controlled by model)
- `Target` is always computed as the center of mass of the last focus group

```csharp
// Extension method for setting position from View
data.SetPosition(new Vector2(1, 2));
```

### CameraFocusController

Extension methods on `CameraDataStorage` for focus management.

```csharp
var cam = CameraBus.Get("Map Camera");

// Add object to current group (creates group if none)
cam.AddInFocus(transform);
cam.AddInFocus(transforms);  // ICollection<Transform>

// Replace focus with new group
cam.SetNewFocusGroup(transform);
cam.SetNewFocusGroup(transforms);

// Remove
cam.RemoveLastFocusGroup();     // remove last group
cam.RemoveTargetFromFocus(transform);  // remove from all groups
cam.ResetFocus();               // clear all groups
```

**Focus stack:** groups follow LIFO ordering. The camera centers on the last group. When the last group is removed, focus returns to the previous one.

### CameraBordersController

Extension methods on `CameraModel` for border management.

```csharp
// Add/remove border (RectTransform)
camera.Data.AddBorder(rectTransform);
camera.Data.RemoveBorder(rectTransform);
camera.Data.ClearBorders();
```

Borders work like focus — stack-based, last is active.

## View

### CameraDataStorage

Camera MonoBehaviour component. Implements `IDataStorage` for compatibility with `DataStorageView<T>`.

- Automatically registers/unregisters with `CameraBus`
- Updates `CameraRect` when `orthographicSize` changes
- `RequireComponent(Camera)`

### CameraHandler

Abstract base class for handlers that bind to a camera by name. Automatically subscribes to `CameraBus` and reconnects when cameras are registered/removed.

```csharp
public class MyHandler : CameraHandler
{
    protected override void SetData()
    {
        // Camera is available, bind data
    }

    protected override void RemoveData()
    {
        // Unbind data
    }
}
```

Inspector settings:
- `cameraName` — camera GameObject name
- `useAnyIfNotFoundKey` — fallback to first available camera

### CameraMoveHandler

Smooth camera-to-target movement with border constraints. Inherits `DataStorageView<CameraModel>`.

| Parameter | Range | Default | Description |
|-----------|-------|---------|-------------|
| `easeDuration` | 0–3 sec | 1 | Animation time for distant movement |
| `easeType` | EaseType | Linear | Easing function |
| `followingRange` | 0–30 | 5 | Instant follow radius |

**Logic:**
- If distance to target <= `followingRange` — instant move
- If farther — smooth animation via `AsyncTween`
- Position is clamped to the active border (`Borders[^1]`) accounting for camera size

### FocusHandler

Declarative `transform` focus binding. `OnEnable` adds, `OnDisable` removes.

Modes (`FocusMode`):
- `AddToFocus` — add to current group
- `NewFocus` — create new group (replace focus)

### BordersHandler

Declarative `RectTransform` border binding. `SetData` adds, `RemoveData` removes.

## Scene Setup

```
Scene
├── Map Camera                    # GameObject with Camera
│   ├── CameraDataStorage         # data holder
│   └── CameraMoveHandler         # smooth following
│       source → Map Camera (CameraDataStorage)
├── Map
│   ├── Borders                   # RectTransform movement zone
│   │   └── BordersHandler        # cameraName = "Map Camera"
│   └── Objects
│       └── Player
│           └── FocusHandler      # cameraName = "Map Camera"
```

## Edge Cases

- **Camera registered after handler**: `CameraHandler` subscribes to `CameraBus.OnRegistration` and automatically picks up the camera
- **No focus**: camera is not model-driven, `transform.position` is read as-is
- **Borders smaller than camera**: `Rect` with negative size, `Clamp` collapses position to border center
- **Multiple cameras**: each independently registered in `CameraBus` by GameObject name

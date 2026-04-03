using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vortex.Unity.EditorTools.Attributes;

namespace Vortex.Sdk.UIs.SaveLoad.Handlers
{
    /// <summary>
    /// AI generation
    /// </summary>
    public class CameraCaptureHandler : MonoBehaviour
    {
        public static readonly HashSet<CameraCaptureHandler> Handlers = new();

        [SerializeField, Range(0, 10)] private int priority;

        [SerializeField, AutoLink] private Camera camera;
        [SerializeField, AutoLink] private Canvas canvas;

        private RenderTexture _renderTexture;

        private static Material _blendMaterial;
        private static Material BlendMaterial => _blendMaterial ??= new Material(Shader.Find("Hidden/AlphaBlit"));

        // ──────────────────────────────────────────────
        // Lifecycle
        // ──────────────────────────────────────────────

        private void OnEnable()
        {
            Handlers.Add(this);
            RecreateRenderTexture();
        }

        private void OnDisable()
        {
            Handlers.Remove(this);
            ReleaseRenderTexture();
        }

        // ──────────────────────────────────────────────
        // RenderTexture management
        // ──────────────────────────────────────────────

        private void RecreateRenderTexture()
        {
            ReleaseRenderTexture();
            var (w, h) = GetScreenSize();
            _renderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
        }

        private void ReleaseRenderTexture()
        {
            if (_renderTexture == null) return;
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }

        // ──────────────────────────────────────────────
        // Screen size
        // ──────────────────────────────────────────────

        private static (int width, int height) GetScreenSize()
        {
            // Приоритет: первая активная камера → Display → Screen
            var cam = Handlers
                .Where(h => h != null && h.isActiveAndEnabled && h.camera != null)
                .OrderBy(h => h.priority)
                .Select(h => h.camera)
                .FirstOrDefault();

            if (cam != null)
                return (cam.pixelWidth, cam.pixelHeight);

            if (Display.displays.Length > 0)
                return (Display.displays[0].renderingWidth, Display.displays[0].renderingHeight);

            return (Screen.width, Screen.height);
        }

        // ──────────────────────────────────────────────
        // Blit
        // ──────────────────────────────────────────────

        /// <summary>
        /// Блендит layer поверх result через src_over.
        /// </summary>
        private static void BlitLayer(RenderTexture layer, RenderTexture result)
        {
            var temp = RenderTexture.GetTemporary(result.descriptor);
            try
            {
                Graphics.Blit(result, temp);
                BlendMaterial.SetTexture("_MainTex", temp);
                BlendMaterial.SetTexture("_Overlay", layer);
                Graphics.Blit(null, result, BlendMaterial);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(temp);
            }
        }
        // ──────────────────────────────────────────────
        // Render
        // ──────────────────────────────────────────────

        /// <summary>
        /// Рендерит этот слой поверх result.
        /// </summary>
        public void Render(RenderTexture result)
        {
            var (w, h) = GetScreenSize();

            if (_renderTexture == null ||
                _renderTexture.width != w ||
                _renderTexture.height != h)
            {
                RecreateRenderTexture();
            }

            if (camera != null)
                RenderCamera(result);
            else if (canvas != null)
                RenderCanvas(result);
            else
            {
                Debug.LogWarning($"[CameraCaptureHandler] '{name}' has no Camera or Canvas assigned.");
                return;
            }
        }

        private void RenderCamera(RenderTexture result)
        {
            var prevActive = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prevActive;

            var prevTarget = camera.targetTexture;
            camera.targetTexture = _renderTexture;
            camera.Render();
            camera.targetTexture = prevTarget;

            BlitLayer(_renderTexture, result);
        }

        private void RenderCanvas(RenderTexture result)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                RenderOverlayCanvas(result);
            else
                RenderCameraCanvas(result);
        }

        private void RenderOverlayCanvas(RenderTexture result)
        {
            var prevMode = canvas.renderMode;
            var prevCamera = canvas.worldCamera;
            var prevDistance = canvas.planeDistance;

            var tempCam = CreateTempCanvasCamera();

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = tempCam;
            //canvas.planeDistance = 1f;

            Canvas.ForceUpdateCanvases();

            var prevActive = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prevActive;

            tempCam.targetTexture = _renderTexture;
            tempCam.Render();
            tempCam.targetTexture = null;

            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCamera;
            canvas.planeDistance = prevDistance;
            canvas.enabled = false;
            canvas.enabled = true;

            Destroy(tempCam.gameObject);

            BlitLayer(_renderTexture, result);
        }

        private void RenderCameraCanvas(RenderTexture result)
        {
            var canvasCamera = canvas.worldCamera;

            if (canvasCamera == null)
            {
                Debug.LogWarning($"[CameraCaptureHandler] Canvas '{canvas.name}' has no worldCamera assigned.");
                return;
            }

            var prevActive = RenderTexture.active;
            RenderTexture.active = _renderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prevActive;

            var prevTarget = canvasCamera.targetTexture;
            canvasCamera.targetTexture = _renderTexture;
            canvasCamera.Render();
            canvasCamera.targetTexture = prevTarget;

            BlitLayer(_renderTexture, result);
        }

        private Camera CreateTempCanvasCamera()
        {
            var go = new GameObject("__TempCanvasCamera__")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;
            cam.cullingMask = 1 << canvas.gameObject.layer;
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            return cam;
        }

        // ──────────────────────────────────────────────
        // Capture
        // ──────────────────────────────────────────────

        /// <summary>
        /// Делает финальный скриншот, компонуя все активные хендлеры по приоритету (меньше = ниже).
        /// </summary>
        public static Texture2D Capture()
        {
            var list = Handlers
                .Where(h => h != null && h.isActiveAndEnabled)
                .OrderBy(h => h.priority)
                .ToList();

            if (list.Count == 0)
            {
                Debug.LogWarning("[CameraCaptureHandler] No active handlers found.");
                return null;
            }

            var (width, height) = GetScreenSize();

            var resultRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

            RenderTexture.active = resultRT;
            GL.Clear(true, true, Color.clear);

            foreach (var handler in list)
                handler.Render(resultRT);

            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(resultRT);

            return tex;
        }
    }
}
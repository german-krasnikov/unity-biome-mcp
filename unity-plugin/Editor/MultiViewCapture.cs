using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    internal static class MultiViewCapture
    {
        public static string CaptureToFile(GameObject target, int cellSize = 512, int supersample = 2,
            string customAngles = null, float zoom = 1f, Vector3 offset = default, float fixedSize = 0f,
            string highlight = null, bool showColliders = false)
            => CaptureWithManifest(target, cellSize, supersample, customAngles, zoom, offset, fixedSize, highlight, showColliders, out _);

        /// <summary>Full capture with optional manifest. manifest=null when highlight is null/empty.</summary>
        internal static string CaptureWithManifest(GameObject target, int cellSize, int supersample,
            string customAngles, float zoom, Vector3 offset, float fixedSize, string highlight,
            bool showColliders, out string manifest, string outputPath = null)
        {
            manifest = null;
            cellSize = Mathf.Clamp(cellSize, 64, 2048);
            supersample = Mathf.Clamp(supersample, 1, 4);
            if (zoom <= 0f) zoom = 1f;
            var bounds = ComputeBounds(target);
            var center = bounds.center;
            center += offset;
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDim < 2f) maxDim = 2f;
            float distance = maxDim * 3f;

            // Classic projections: Front, Left, Top, Isometric
            var mainCam = Camera.main ?? (Camera.allCameras.Length > 0 ? Camera.allCameras[0] : null);
            var isoDir = new Vector3(1f, 1f, -1f).normalized;

            var views = new (Vector3 pos, Quaternion rot, float sizeMultiplier)[]
            {
                // Top-left: FRONT — camera looks from +Z toward -Z
                (center + Vector3.forward * distance, Quaternion.LookRotation(Vector3.back, Vector3.up), 1.0f),
                // Top-right: LEFT — camera looks from -X toward +X
                (center + Vector3.left * distance,    Quaternion.LookRotation(Vector3.right, Vector3.up), 1.0f),
                // Bottom-left: TOP — camera looks straight down
                (center + Vector3.up * distance,      Quaternion.Euler(90f, 0f, 0f), 1.0f),
                // Bottom-right: ISOMETRIC — above-front-right (~35° elevation)
                (center + isoDir * distance,          Quaternion.LookRotation(-isoDir, Vector3.up), 1.0f),
            };
            if (customAngles != null) ApplyCustomAngles(views, customAngles, center, distance);

            int gridSize = cellSize * 2;
            int hiRes = cellSize * supersample;
            Texture2D composite = null;
            RenderTexture hiResRT = null, cellRT = null;
            GameObject camGO = null;
            var prevActive = RenderTexture.active;

            try
            {
                composite = new Texture2D(gridSize, gridSize, TextureFormat.RGB24, false);
                hiResRT = new RenderTexture(hiRes, hiRes, 24, RenderTextureFormat.ARGB32);
                cellRT  = new RenderTexture(cellSize, cellSize, 0, RenderTextureFormat.ARGB32);

                camGO = new GameObject("_MCP_MultiView");
                camGO.hideFlags = HideFlags.HideAndDontSave;
                var cam = camGO.AddComponent<Camera>();
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = distance * 10f;
                cam.orthographic = true;
                cam.targetTexture = hiResRT;
                CopyFromSceneCamera(cam, mainCam);

                int[] ox = { 0, cellSize, 0, cellSize };
                int[] oy = { cellSize, cellSize, 0, 0 };

                var highlightObjs = MultiViewOverlay.ParseHighlight(highlight);
                var snapStates = new CamState[4];

                for (int i = 0; i < 4; i++)
                {
                    cam.transform.rotation = views[i].rot;
                    var camPos = CameraLookAt(bounds, views[i].rot, distance, out float projOrtho);
                    camPos += offset;
                    float baseOrtho = fixedSize > 0f ? fixedSize : projOrtho * 1.15f * views[i].sizeMultiplier;
                    cam.orthographicSize = baseOrtho / zoom;
                    cam.transform.position = camPos;
                    RenderCamera(cam);
                    // Downscale hi-res → cell
                    Graphics.Blit(hiResRT, cellRT);
                    RenderTexture.active = cellRT;
                    composite.ReadPixels(new Rect(0, 0, cellSize, cellSize), ox[i], oy[i]);
                    // Snapshot camera state (position + rotation + orthoSize) for manifest
                    snapStates[i] = new CamState(camPos, views[i].rot, cam.orthographicSize);
                }

                cam.targetTexture = null;

                MultiViewOverlay.DrawSeparators(composite, cellSize, gridSize);
                if (highlightObjs.Count > 0)
                {
                    MultiViewOverlay.DrawLabels(composite, cellSize);
                    for (int i = 0; i < 4; i++)
                    {
                        OverlayDrawer.DrawBoundingBoxes(composite, cellSize, snapStates[i], i, highlightObjs);
                        if (showColliders)
                            OverlayDrawer.DrawColliderShapes(composite, cellSize, snapStates[i], i, highlightObjs);
                    }
                }

                composite.Apply();
                if (highlightObjs.Count > 0)
                    manifest = MultiViewOverlay.BuildManifest(snapStates, highlightObjs);
                return FileOutputHelper.WritePng(composite.EncodeToPNG(), "multiview", outputPath);
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (camGO) { var c = camGO.GetComponent<Camera>(); if (c) c.targetTexture = null; UnityEngine.Object.DestroyImmediate(camGO); }
                if (hiResRT) { hiResRT.Release(); UnityEngine.Object.DestroyImmediate(hiResRT); }
                if (cellRT)  { cellRT.Release();  UnityEngine.Object.DestroyImmediate(cellRT); }
                if (composite) UnityEngine.Object.DestroyImmediate(composite);
            }
        }

        public static string CaptureSceneOverview(int width, int height, bool topDown, string outputPath = null)
        {
            var allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var bounds = allRenderers.Length > 0 ? allRenderers[0].bounds : new Bounds(Vector3.zero, Vector3.one * 20f);
            foreach (var r in allRenderers) bounds.Encapsulate(r.bounds);
            float maxDim = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z), 1f);
            float dist = maxDim * 2.5f;
            var mainCam = Camera.main ?? (Camera.allCameras.Length > 0 ? Camera.allCameras[0] : null);
            Quaternion rot = topDown
                ? Quaternion.LookRotation(Vector3.down, Vector3.forward)
                : (mainCam != null ? mainCam.transform.rotation : Quaternion.LookRotation(new Vector3(0.5f, 1f, -0.5f).normalized));
            Vector3 dir = rot * Vector3.forward;
            Vector3 pos = topDown ? bounds.center + Vector3.up * dist : bounds.center - dir * dist * 1.5f;
            float orthoSize = maxDim * (topDown ? 0.55f : 0.825f);

            RenderTexture rt = null; Texture2D tex = null; GameObject camGO = null;
            var prevActive = RenderTexture.active;
            try
            {
                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                camGO = new GameObject("_MCP_Overview"); camGO.hideFlags = HideFlags.HideAndDontSave;
                var cam = camGO.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = orthoSize;
                cam.nearClipPlane = 0.01f; cam.farClipPlane = dist * 10f;
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.targetTexture = rt;
                CopyFromSceneCamera(cam, mainCam);
                RenderCamera(cam); cam.targetTexture = null;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
                return FileOutputHelper.WritePng(tex.EncodeToPNG(), "overview", outputPath);
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (camGO) { var c = camGO.GetComponent<Camera>(); if (c) c.targetTexture = null; UnityEngine.Object.DestroyImmediate(camGO); }
                if (rt)  { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static Vector3 CameraLookAt(Bounds bounds, Quaternion camRot, float distance, out float orthoSize)
        {
            // Project 8 bounds corners onto camera's local XY to find visible extents
            var invRot = Quaternion.Inverse(camRot);
            var min = bounds.min;
            var max = bounds.max;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                var local = invRot * corner;
                if (local.x < minX) minX = local.x;
                if (local.x > maxX) maxX = local.x;
                if (local.y < minY) minY = local.y;
                if (local.y > maxY) maxY = local.y;
            }
            // orthoSize = half-height of projected bounds (with padding)
            orthoSize = Mathf.Max((maxX - minX) * 0.5f, (maxY - minY) * 0.5f);
            orthoSize = Mathf.Max(orthoSize, 1f);
            // Camera position: look at bounds.center from distance along -forward
            var forward = camRot * Vector3.forward;
            return bounds.center - forward * distance;
        }

        static void ApplyCustomAngles((Vector3 pos, Quaternion rot, float sizeMultiplier)[] views,
            string angles, Vector3 center, float distance)
        {
            // Format: "ex,ey,ez|ex,ey,ez|ex,ey,ez|ex,ey,ez" — Euler angles per view, pipe-separated
            // Use _ to skip a view: "45,30,0|_|_|90,0,0"
            var parts = angles.Split('|');
            for (int i = 0; i < Mathf.Min(parts.Length, views.Length); i++)
            {
                var p = parts[i].Trim();
                if (p == "_" || string.IsNullOrEmpty(p)) continue;
                var nums = p.Split(',');
                if (nums.Length < 3) continue;
                if (!float.TryParse(nums[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float ex)) continue;
                if (!float.TryParse(nums[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float ey)) continue;
                if (!float.TryParse(nums[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float ez)) continue;
                var rot = Quaternion.Euler(ex, ey, ez);
                var dir = rot * Vector3.forward;
                var dist = distance * views[i].sizeMultiplier;
                views[i].pos = center - dir * dist;
                views[i].rot = rot;
            }
        }

        internal static Bounds ComputeBounds(GameObject go)
        {
            // Only MeshRenderer + SkinnedMeshRenderer — exclude particles, trails, lines
            var allR = go.GetComponentsInChildren<Renderer>();
            Bounds? b = null;
            foreach (var r in allR)
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    if (b == null) b = r.bounds;
                    else { var bb = b.Value; bb.Encapsulate(r.bounds); b = bb; }
                }
            }
            if (b != null) return b.Value;
            var colliders = go.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0) { var cb = colliders[0].bounds; for (int i = 1; i < colliders.Length; i++) cb.Encapsulate(colliders[i].bounds); return cb; }
            return new Bounds(go.transform.position, Vector3.one * 3f);
        }

        static void RenderCamera(Camera cam) => ScreenshotCapture.RenderOffscreen(cam);

        internal static (Vector3 pos, Quaternion rot, float sizeMultiplier) GetStandardView(
            string angle, Vector3 center, float distance)
        {
            var isoDir = new Vector3(1f, 1f, -1f).normalized;
            switch ((angle ?? "front").ToLowerInvariant())
            {
                case "front": return (center + Vector3.forward * distance, Quaternion.LookRotation(Vector3.back, Vector3.up), 1f);
                case "left":  return (center + Vector3.left * distance, Quaternion.LookRotation(Vector3.right, Vector3.up), 1f);
                case "top":   return (center + Vector3.up * distance, Quaternion.Euler(90f, 0f, 0f), 1f);
                case "iso":   return (center + isoDir * distance, Quaternion.LookRotation(-isoDir, Vector3.up), 1f);
                default:
                    var nums = angle.Split(',');
                    if (nums.Length >= 3 &&
                        float.TryParse(nums[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ex) &&
                        float.TryParse(nums[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ey) &&
                        float.TryParse(nums[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ez))
                    {
                        var rot = Quaternion.Euler(ex, ey, ez);
                        return (center - rot * Vector3.forward * distance, rot, 1f);
                    }
                    return (center + Vector3.forward * distance, Quaternion.LookRotation(Vector3.back, Vector3.up), 1f);
            }
        }

        /// <summary>Render a single orthographic view of a target object.</summary>
        internal static string CaptureSingleView(GameObject target, int size, int supersample,
            string angle, float zoom, Vector3 offset, float fixedSize,
            string highlight, bool showColliders, out string manifest, string outputPath = null)
        {
            manifest = null;
            supersample = Mathf.Clamp(supersample, 1, 4);
            if (zoom <= 0f) zoom = 1f;
            var bounds = ComputeBounds(target);
            var center = bounds.center + offset;
            float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (maxDim < 2f) maxDim = 2f;
            float distance = maxDim * 3f;

            var view = GetStandardView(angle, center, distance);
            var mainCam = Camera.main ?? (Camera.allCameras.Length > 0 ? Camera.allCameras[0] : null);

            int hiRes = size * supersample;
            Texture2D tex = null; RenderTexture hiResRT = null, cellRT = null; GameObject camGO = null;
            var prevActive = RenderTexture.active;
            try
            {
                tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                hiResRT = new RenderTexture(hiRes, hiRes, 24, RenderTextureFormat.ARGB32);
                cellRT = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
                camGO = new GameObject("_MCP_SingleView") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGO.AddComponent<Camera>();
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = distance * 10f;
                cam.orthographic = true;
                cam.targetTexture = hiResRT;
                CopyFromSceneCamera(cam, mainCam);

                cam.transform.rotation = view.rot;
                var camPos = CameraLookAt(bounds, view.rot, distance, out float projOrtho);
                camPos += offset;
                cam.orthographicSize = (fixedSize > 0f ? fixedSize : projOrtho * 1.15f) / zoom;
                cam.transform.position = camPos;
                RenderCamera(cam);
                Graphics.Blit(hiResRT, cellRT);
                RenderTexture.active = cellRT;
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                cam.targetTexture = null;

                var highlightObjs = MultiViewOverlay.ParseHighlight(highlight);
                if (highlightObjs.Count > 0)
                {
                    var snap = new CamState(camPos, view.rot, cam.orthographicSize);
                    OverlayDrawer.DrawBoundingBoxes(tex, size, snap, 0, 0, highlightObjs);
                    if (showColliders)
                        OverlayDrawer.DrawColliderShapes(tex, size, snap, 0, 0, highlightObjs);
                    manifest = $"{angle ?? "front"}:{string.Join(",", highlightObjs.ConvertAll(o => o.go.name + "(vis)"))}";
                }
                tex.Apply();
                return FileOutputHelper.WritePng(tex.EncodeToPNG(), "singleview", outputPath);
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (camGO) { var c = camGO.GetComponent<Camera>(); if (c) c.targetTexture = null; UnityEngine.Object.DestroyImmediate(camGO); }
                if (hiResRT) { hiResRT.Release(); UnityEngine.Object.DestroyImmediate(hiResRT); }
                if (cellRT) { cellRT.Release(); UnityEngine.Object.DestroyImmediate(cellRT); }
                if (tex) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static void CopyFromSceneCamera(Camera dst, Camera src)
        {
            if (src != null)
            {
                dst.clearFlags      = src.clearFlags;
                dst.backgroundColor = src.backgroundColor;
                dst.cullingMask     = src.cullingMask;
            }
            else
            {
                dst.clearFlags = RenderSettings.skybox != null
                    ? CameraClearFlags.Skybox
                    : CameraClearFlags.SolidColor;
                dst.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
                dst.cullingMask = ~0;
            }

            // Copy SRP AdditionalCameraData
            if (src == null) return;
            foreach (var srcComp in src.gameObject.GetComponents<Component>())
            {
                if (srcComp == null) continue;
                if (!srcComp.GetType().Name.Contains("AdditionalCameraData")) continue;
                var dstComp = dst.gameObject.AddComponent(srcComp.GetType());
                if (dstComp == null) continue;
                UnityEditor.EditorUtility.CopySerialized(srcComp, dstComp);
                srcComp.GetType().GetProperty("renderType")?.SetValue(dstComp, 0);
                break;
            }
        }
    }
}

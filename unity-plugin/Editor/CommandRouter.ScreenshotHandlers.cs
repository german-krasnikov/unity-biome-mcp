using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor
{
    public static partial class CommandRouter
    {
        private static string BuildScreenshotResponse(string id, string args)
        {
            var camera = JsonHelper.ExtractString(args, "camera");
            var requestedOutputPath = JsonHelper.ExtractString(args, "output_path");
            if (string.IsNullOrEmpty(requestedOutputPath)
                && camera != "multi_view" && camera != "single_view")
                requestedOutputPath = JsonHelper.ExtractString(args, "path");
            FileOutputHelper.ValidatePngOutputPath(requestedOutputPath);

            if (camera == "annotation_frame")
            {
                var annotId = JsonHelper.ExtractString(args, "annotation_id");
                if (!string.IsNullOrEmpty(annotId))
                {
                    var snap = SceneRegionState.GetById(annotId);
                    if (snap != null) SceneRegionState.FrameRegion(snap.Id);
                }
                camera = "scene_view";
            }

            if (camera == "overview" || camera == "overview_game")
            {
                var w = ExtractInt(args, "width", 1280);
                var h = ExtractInt(args, "height", 720);
                var overviewOutputPath = JsonHelper.ExtractString(args, "output_path");
                var fp = MultiViewCapture.CaptureSceneOverview(w, h, topDown: camera == "overview", outputPath: overviewOutputPath);
                return JsonHelper.FormatFileResponse(id, fp);
            }

            if (camera == "multi_view")
            {
                var path = JsonHelper.ExtractString(args, "path");
                if (string.IsNullOrEmpty(path))
                    throw new System.ArgumentException("multi_view requires 'path' — the object to capture");
                var go = ComponentSerializer.FindObject(path);
                if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
                var cellSize    = ExtractInt(args, "width", 512);
                var supersample = ExtractInt(args, "supersample", 2);
                var angles      = JsonHelper.ExtractString(args, "angles");
                float zoom = ExtractFloat(args, "zoom", 1f);
                Vector3 offset = ExtractVector3(args, "offset", Vector3.zero);
                float fixedSize = ExtractFloat(args, "fixed_size", 0f);
                var highlight = JsonHelper.ExtractString(args, "highlight");
                var showColliders = JsonHelper.ExtractString(args, "show_colliders") == "true";
                var multiOutputPath = JsonHelper.ExtractString(args, "output_path");
                var filePath = MultiViewCapture.CaptureWithManifest(go, cellSize, supersample,
                    angles, zoom, offset, fixedSize, highlight, showColliders, out var manifest, multiOutputPath);
                if (!string.IsNullOrEmpty(manifest))
                    return JsonHelper.FormatFileResponseWithData(id, filePath, manifest);
                return JsonHelper.FormatFileResponse(id, filePath);
            }

            if (camera == "single_view")
            {
                var path = JsonHelper.ExtractString(args, "path");
                if (string.IsNullOrEmpty(path))
                    throw new System.ArgumentException("single_view requires 'path' — the object to capture");
                var go = ComponentSerializer.FindObject(path);
                if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
                var size        = ExtractInt(args, "width", 512);
                var supersample = ExtractInt(args, "supersample", 2);
                var angle       = JsonHelper.ExtractString(args, "angle") ?? "front";
                float zoom = ExtractFloat(args, "zoom", 1f);
                Vector3 offset = ExtractVector3(args, "offset", Vector3.zero);
                float fixedSize = ExtractFloat(args, "fixed_size", 0f);
                var highlight = JsonHelper.ExtractString(args, "highlight");
                var showColliders = JsonHelper.ExtractString(args, "show_colliders") == "true";
                var singleOutputPath = JsonHelper.ExtractString(args, "output_path");
                var filePath = MultiViewCapture.CaptureSingleView(go, size, supersample,
                    angle, zoom, offset, fixedSize, highlight, showColliders, out var manifest, singleOutputPath);
                if (!string.IsNullOrEmpty(manifest))
                    return JsonHelper.FormatFileResponseWithData(id, filePath, manifest);
                return JsonHelper.FormatFileResponse(id, filePath);
            }

            var width      = ExtractInt(args, "width", 640);
            var height     = ExtractInt(args, "height", 480);
            var outputPath = JsonHelper.ExtractString(args, "output_path")
                          ?? JsonHelper.ExtractString(args, "path");
            var fpath = ScreenshotCapture.CaptureToFile(width, height, camera, outputPath);
            return JsonHelper.FormatFileResponse(id, fpath);
        }
    }
}

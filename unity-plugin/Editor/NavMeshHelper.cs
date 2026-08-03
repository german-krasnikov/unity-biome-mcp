#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
using System.Text;
using UnityEngine;
using UnityEngine.AI;

namespace UnityMCP.Editor
{
    public static class NavMeshHelper
    {
        private static readonly System.Globalization.CultureInfo IC =
            System.Globalization.CultureInfo.InvariantCulture;

        public static string Execute(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            switch (action)
            {
                case "sample":       return SamplePosition(args);
                case "path":         return CalculatePath(args);
                case "raycast":      return DoRaycast(args);
                case "bake":         return Bake();
                case "status":       return Status();
                case "clear":        return Clear();
                case "get_settings": return GetSettings(args);
                case "set_settings": return SetSettings(args);
                default:             throw new System.ArgumentException($"unknown navmesh action: {action}");
            }
        }

        private static string Bake()
        {
#if UNITYMCP_HAS_AI_NAVIGATION
            var surfaces = Object.FindObjectsOfType<Unity.AI.Navigation.NavMeshSurface>();
            if (surfaces.Length > 0)
            {
                foreach (var s in surfaces) s.BuildNavMesh();
                return $"baked {surfaces.Length} NavMeshSurface(s)";
            }
#endif
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
            return "baked (legacy NavMeshBuilder)";
        }

        private static string Status()
        {
            var tri = NavMesh.CalculateTriangulation();
            return $"triangles:{tri.indices.Length / 3}\nvertices:{tri.vertices.Length}\nareas:{tri.areas.Length}";
        }

        private static string Clear()
        {
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            return "cleared";
        }

        private static string GetSettings(string args)
        {
            int count = NavMesh.GetSettingsCount();
            var sb = new StringBuilder();
            sb.Append("agents:").AppendLine(count.ToString());
            for (int i = 0; i < count; i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                var name = NavMesh.GetSettingsNameFromID(s.agentTypeID);
                sb.Append("name:").AppendLine(name);
                sb.Append("agentRadius:").AppendLine(s.agentRadius.ToString("G4", IC));
                sb.Append("agentHeight:").AppendLine(s.agentHeight.ToString("G4", IC));
                sb.Append("agentClimb:").AppendLine(s.agentClimb.ToString("G4", IC));
                sb.Append("agentSlope:").AppendLine(s.agentSlope.ToString("G4", IC));
                sb.Append("voxelSize:").AppendLine(s.voxelSize.ToString("G4", IC));
                if (i < count - 1) sb.AppendLine("---");
            }
            return sb.ToString().TrimEnd();
        }

        private static string SetSettings(string args)
        {
#if UNITYMCP_HAS_AI_NAVIGATION
            var surfaces = Object.FindObjectsOfType<Unity.AI.Navigation.NavMeshSurface>();
            if (surfaces.Length > 0)
            {
                float agentRadius = JsonHelper.ExtractFloat(args, "agentRadius");
                float agentHeight = JsonHelper.ExtractFloat(args, "agentHeight");
                float agentClimb  = JsonHelper.ExtractFloat(args, "agentClimb");
                float agentSlope  = JsonHelper.ExtractFloat(args, "agentSlope");
                foreach (var s in surfaces)
                {
                    if (agentRadius > 0) s.agentRadius = agentRadius;
                    if (agentHeight > 0) s.agentHeight = agentHeight;
                    if (agentClimb  > 0) s.agentClimb  = agentClimb;
                    if (agentSlope  > 0) s.agentSlope  = agentSlope;
                    UnityEditor.EditorUtility.SetDirty(s);
                }
                return $"updated {surfaces.Length} NavMeshSurface(s)";
            }
#endif
            return "err:no NavMeshSurface found. Legacy baking: set agent settings via Navigation window → Agents tab, then bake.";
        }

        private static string SamplePosition(string args)
        {
            var center   = ValueParser.ParseVector3(JsonHelper.ExtractString(args, "center"));
            var maxDist  = JsonHelper.ExtractFloat(args, "max_distance");
            if (maxDist == 0f) maxDist = 5f;
            var areaMask = (int)JsonHelper.ExtractFloat(args, "area_mask");
            if (areaMask == 0) areaMask = -1;

            if (NavMesh.SamplePosition(center, out var hit, maxDist, areaMask))
                return $"walkable: true\nposition: {V3(hit.position)}\ndistance: {hit.distance.ToString("G4", IC)}";
            return "walkable: false";
        }

        private static string CalculatePath(string args)
        {
            var from     = ValueParser.ParseVector3(JsonHelper.ExtractString(args, "from"));
            var to       = ValueParser.ParseVector3(JsonHelper.ExtractString(args, "to"));
            var areaMask = (int)JsonHelper.ExtractFloat(args, "area_mask");
            if (areaMask == 0) areaMask = -1;

            var path = new NavMeshPath();
            NavMesh.CalculatePath(from, to, areaMask, path);

            var sb = new StringBuilder();
            sb.Append("status: ").AppendLine(path.status.ToString());
            sb.Append("corners: ").Append(path.corners.Length);
            foreach (var c in path.corners)
                sb.AppendLine().Append("  ").Append(V3(c));
            return sb.ToString();
        }

        private static string DoRaycast(string args)
        {
            var from     = ValueParser.ParseVector3(JsonHelper.ExtractString(args, "from"));
            var to       = ValueParser.ParseVector3(JsonHelper.ExtractString(args, "to"));
            var areaMask = (int)JsonHelper.ExtractFloat(args, "area_mask");
            if (areaMask == 0) areaMask = -1;

            if (NavMesh.Raycast(from, to, out var hit, areaMask))
                return $"hit: true\nposition: {V3(hit.position)}\ndistance: {hit.distance.ToString("G4", IC)}\nmask: {hit.mask}";
            return $"hit: false\nposition: {V3(to)}\ndistance: {Vector3.Distance(from, to).ToString("G4", IC)}";
        }

        private static string V3(Vector3 v) =>
            $"({v.x.ToString("G4", IC)}, {v.y.ToString("G4", IC)}, {v.z.ToString("G4", IC)})";
    }
}
#endif

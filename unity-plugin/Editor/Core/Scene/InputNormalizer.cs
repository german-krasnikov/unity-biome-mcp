using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityMCP.Playtest.Core;

namespace UnityMCP.Editor
{
    internal static class InputNormalizer
    {
        // Component name aliases: wrong → correct
        static readonly Dictionary<string, string> ComponentAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "RigidBody", "Rigidbody" },
            { "Rigid Body", "Rigidbody" },
            { "RigidBody2D", "Rigidbody2D" },
            { "BoxCollider2d", "BoxCollider2D" },
            { "CircleCollider2d", "CircleCollider2D" },
            { "PolygonCollider2d", "PolygonCollider2D" },
            { "EdgeCollider2d", "EdgeCollider2D" },
            { "CapsuleCollider2d", "CapsuleCollider2D" },
            { "CharacterController2d", "CharacterController2D" },
            { "NavMesh Agent", "NavMeshAgent" },
            { "Nav Mesh Agent", "NavMeshAgent" },
            { "Mesh Renderer", "MeshRenderer" },
            { "Skinned Mesh Renderer", "SkinnedMeshRenderer" },
            { "Audio Source", "AudioSource" },
            { "Audio Listener", "AudioListener" },
            { "Canvas Group", "CanvasGroup" },
            { "Rect Transform", "RectTransform" },
            { "Line Renderer", "LineRenderer" },
        };

        // Property name aliases: camelCase/friendly → m_SerializedName
        static readonly Dictionary<string, string> PropertyAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "localPosition", "m_LocalPosition" },
            { "position", "m_LocalPosition" },
            { "localRotation", "m_LocalRotation" },
            { "rotation", "m_LocalRotation" },
            { "localScale", "m_LocalScale" },
            { "scale", "m_LocalScale" },
            { "mass", "m_Mass" },
            { "drag", "m_Drag" },
            { "linearDamping", "m_Drag" },
            { "m_LinearDamping", "m_Drag" },
            { "angularDrag", "m_AngularDrag" },
            { "angularDamping", "m_AngularDrag" },
            { "m_AngularDamping", "m_AngularDrag" },
            { "useGravity", "m_UseGravity" },
            { "isKinematic", "m_IsKinematic" },
            { "kinematic", "m_IsKinematic" },
            { "enabled", "m_Enabled" },
            { "tag", "m_TagString" },
            { "layer", "m_Layer" },
            { "name", "m_Name" },
            { "active", "m_IsActive" },
            { "castShadows", "m_CastShadows" },
            { "receiveShadows", "m_ReceiveShadows" },
            { "isTrigger", "m_IsTrigger" },
            { "center", "m_Center" },
            { "size", "m_Size" },
            { "radius", "m_Radius" },
            { "height", "m_Height" },
            { "material", "m_Material" },
            { "color", "m_Color" },
            { "intensity", "m_Intensity" },
            { "range", "m_Range" },
            { "spotAngle", "m_SpotAngle" },
            { "clip", "m_AudioClip" },
            { "volume", "m_Volume" },
            { "loop", "m_Loop" },
            { "playOnAwake", "m_PlayOnAwake" },
            { "speed", "m_Speed" },
            { "gravity", "m_UseGravity" },
            { "collisionDetection", "m_CollisionDetection" },
            { "interpolation", "m_Interpolate" },
            { "constraints", "m_Constraints" },

            // AudioSource — verified against UnityCsReference AudioSourceInspector.cs
            // (branch 6000.0, matching this package's min Unity version). Several
            // fields have NO "m_" prefix — this is not a typo.
            { "audioClip", "m_Resource" },
            { "mute", "Mute" },
            { "pitch", "m_Pitch" },
            { "minDistance", "MinDistance" },
            { "maxDistance", "MaxDistance" },
            { "priority", "Priority" },
            { "dopplerLevel", "DopplerLevel" },

            // Light — verified against UnityCsReference LightEditor.cs (branch 6000.0).
            // Shadow fields are nested under the "m_Shadows" struct.
            { "shadowStrength", "m_Shadows.m_Strength" },
            { "shadowBias", "m_Shadows.m_Bias" },
            { "shadowNormalBias", "m_Shadows.m_NormalBias" },
            { "lightType", "m_Type" },
            { "bounceIntensity", "m_BounceIntensity" },
            { "cookieSize", "m_CookieSize" },
            { "innerSpotAngle", "m_InnerSpotAngle" },
        };

        // Value aliases: named constants → serialized format
        static readonly Dictionary<string, string> ValueAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Color.red", "(1,0,0,1)" },
            { "Color.green", "(0,1,0,1)" },
            { "Color.blue", "(0,0,1,1)" },
            { "Color.white", "(1,1,1,1)" },
            { "Color.black", "(0,0,0,1)" },
            { "Color.yellow", "(1,0.92,0.016,1)" },
            { "Color.cyan", "(0,1,1,1)" },
            { "Color.magenta", "(1,0,1,1)" },
            { "Color.gray", "(0.5,0.5,0.5,1)" },
            { "Color.grey", "(0.5,0.5,0.5,1)" },
            { "Color.clear", "(0,0,0,0)" },
            { "red", "(1,0,0,1)" },
            { "green", "(0,1,0,1)" },
            { "blue", "(0,0,1,1)" },
            { "white", "(1,1,1,1)" },
            { "black", "(0,0,0,1)" },
            { "Vector3.zero", "(0,0,0)" },
            { "Vector3.one", "(1,1,1)" },
            { "Vector3.up", "(0,1,0)" },
            { "Vector3.down", "(0,-1,0)" },
            { "Vector3.left", "(-1,0,0)" },
            { "Vector3.right", "(1,0,0)" },
            { "Vector3.forward", "(0,0,1)" },
            { "Vector3.back", "(0,0,-1)" },
            { "Vector2.zero", "(0,0)" },
            { "Vector2.one", "(1,1)" },
            { "Vector2.up", "(0,1)" },
            { "Vector2.down", "(0,-1)" },
            { "Vector2.left", "(-1,0)" },
            { "Vector2.right", "(1,0)" },
            { "Quaternion.identity", "(0,0,0,1)" },

            // Bool aliases: case-insensitive → Unity serialized format
            { "true",  "True"  },
            { "false", "False" },
            { "yes",   "True"  },
            { "no",    "False" },
            { "on",    "True"  },
            { "off",   "False" },
        };

        public static string NormalizeComponent(string input, GameObject go)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // 1. Exact dict match (case-insensitive)
            if (ComponentAliases.TryGetValue(input, out var alias))
                return alias;

            // 2. Already valid — GetComponent succeeds
            if (go.GetComponent(input) != null)
                return input;

            // 3. Case-insensitive match against actual components on GO
            var actual = go.GetComponents<Component>();
            foreach (var c in actual)
            {
                if (c == null) continue;
                var typeName = c.GetType().Name;
                if (string.Equals(typeName, input, StringComparison.OrdinalIgnoreCase))
                    return typeName;
                var fullName = c.GetType().FullName;
                if (string.Equals(fullName, input, StringComparison.OrdinalIgnoreCase))
                    return typeName;
                var bt = typeName.IndexOf('`');
                if (bt > 0 && string.Equals(typeName.Substring(0, bt), input, StringComparison.OrdinalIgnoreCase))
                    return typeName;
            }

            // 4. Fuzzy match via Levenshtein
            var names = actual.Where(c => c != null).Select(c => c.GetType().Name).Distinct();
            var closest = StringDistance.ClosestMatch(input, names, 3);
            if (closest != null) return closest;

            return input;
        }

        public static string NormalizeProperty(string input, SerializedObject so)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // 1. Already valid
            if (so.FindProperty(input) != null)
                return input;

            // 2. Dict alias
            if (PropertyAliases.TryGetValue(input, out var alias) && so.FindProperty(alias) != null)
                return alias;

            // 3+4. Try m_ prefix and _lowerCamelCase — guard: skip for underscore-prefixed inputs
            //      to prevent double-underscore ("m__field") corruption.
            if (input[0] != '_')
            {
                var mPrefixed = "m_" + char.ToUpper(input[0]) + input.Substring(1);
                if (so.FindProperty(mPrefixed) != null)
                    return mPrefixed;

                // 4. Try _lowerCamelCase convention (dominant Unity private [SerializeField] style)
                var underscored = "_" + char.ToLower(input[0]) + input.Substring(1);
                if (so.FindProperty(underscored) != null)
                    return underscored;
            }

            return input;
        }

        public static string NormalizeValue(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return ValueAliases.TryGetValue(input, out var alias) ? alias : input;
        }
    }
}

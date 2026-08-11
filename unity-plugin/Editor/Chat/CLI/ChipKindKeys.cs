// Public string constants for all built-in chip kind keys.
// Use these instead of raw strings to avoid typos.
namespace UnityMCP.Editor.Chat
{
    /// <summary>String key constants for the 9 built-in chip kinds.</summary>
    public static class ChipKindKeys
    {
        public const string Hierarchy       = "hierarchy";
        public const string Scene           = "scene";
        public const string Script          = "script";
        public const string Prefab          = "prefab";
        public const string Material        = "material";
        public const string Texture         = "texture";
        // Intentional token-economy abbreviation: payload emits [so:...]. Use this constant; never hardcode "scriptableobject".
        public const string ScriptableObject = "so";
        public const string Folder          = "folder";
        public const string Asset           = "asset";
        // External image files dropped from Finder or pasted from clipboard.
        public const string Image           = "image";
        // 3D model assets (.fbx / .obj / .blend / .dae).
        public const string Model           = "model";
        // Audio assets (.wav / .mp3 / .ogg / .aiff).
        public const string Audio           = "audio";
        // Drawn polygon region on the scene. Created by SceneRegionTool, stored in RegionSnapshot.
        // Path = 8-char UUID (e.g., "a1b2c3d4"). Bracket format: [region:a1b2c3d4].
        public const string Region          = "region";
        // Single serialized field of a component. Path = "goPath|CompType|fieldName".
        public const string Field           = "field";
        // A component on a GameObject. Path = "goPath|CompType".
        public const string Component       = "component";
        // Annotated screenshot captured by the annotation editor. Path = absolute PNG path.
        public const string AnnotatedScreenshot = "annotated_screenshot";
        // Claude Code sub-agent referenced via @mention. Path = agent name (file stem, e.g. "senior-developer").
        // Payload format: [agent:name]. Instructs the model to invoke Agent(subagent_type=name).
        public const string Agent = "agent";
    }
}

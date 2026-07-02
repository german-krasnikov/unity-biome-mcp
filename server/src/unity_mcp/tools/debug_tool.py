"""AI-assisted debugging: gather diagnostic context based on symptom."""
from ._annotations import RO as _RO
from ._common import bind

_send = None

# Maps symptom keywords → relevant component types
SYMPTOM_MAP: dict[str, list[str]] = {
    "move": ["NavMeshAgent", "Rigidbody", "CharacterController", "Transform"],
    "attack": ["Animator", "Health", "EnemyAI", "WeaponController"],
    "collid": ["Collider", "Rigidbody", "BoxCollider", "SphereCollider", "CapsuleCollider"],
    "anim": ["Animator", "Animation"],
    "ui": ["Button", "Canvas", "EventSystem", "GraphicRaycaster", "CanvasGroup", "Image"],
    "physics": ["Rigidbody", "Collider", "Joint"],
    "sound": ["AudioSource", "AudioListener"],
    "render": ["MeshRenderer", "Material", "Shader", "Light"],
    "spawn": ["Transform", "Rigidbody"],
    "visible": ["MeshRenderer", "SpriteRenderer", "CanvasRenderer", "Camera"],
    "nav": ["NavMeshAgent", "NavMeshObstacle"],
    "particle": ["ParticleSystem"],
    "camera": ["Camera", "CinemachineVirtualCamera"],
}


def classify_symptom(symptom: str) -> tuple[list[str], list[str]]:
    """Map symptom keywords to (tools, component_types)."""
    symptom_lower = symptom.lower()
    components: list[str] = []
    for keyword, comp_list in SYMPTOM_MAP.items():
        if keyword in symptom_lower:
            components.extend(comp_list)
    tools = ["inspect", "get_console"]
    return tools, list(set(components))


def build_commands(tools: list[str], path: str, components: list[str] | None = None) -> str:
    """Build batch command string from tool list, path, and component types."""
    lines: list[str] = []
    if "inspect" in tools:
        if path:
            comp_str = f" components={','.join(components)}" if components else ""
            lines.append(f"inspect paths={path}{comp_str}")
        else:
            lines.append("get_hierarchy")
    if "screenshot" in tools:
        lines.append("screenshot")
    if "get_console" in tools:
        lines.append("get_console count=10")
    return "\n".join(lines)


def format_diagnostic(result: str, symptom: str, path: str) -> str:
    """Format batch result as structured diagnostic text for LLM analysis."""
    header: list[str] = []
    if symptom:
        header.append(f"symptom: {symptom}")
    if path:
        header.append(f"object: {path}")
    header.append("---")
    return "\n".join(header) + "\n" + result


async def debug(symptom: str = "", path: str = "", gather: str = "") -> str:
    """AI-assisted debug: gather diagnostic context based on symptom.

    symptom: Natural language description ("enemy doesn't move", "button not clickable")
    path: Optional target object path ("/Enemy_01")
    gather: Override comma-separated tool names ("inspect,get_console,screenshot")

    Returns structured diagnostic text for LLM analysis.
    """
    if gather:
        tools = [t.strip() for t in gather.split(",")]
        components: list[str] = []
    else:
        tools, components = classify_symptom(symptom)
    commands = build_commands(tools, path, components)
    result = await _send("batch", {"commands": commands, "on_error": "continue"})
    return format_diagnostic(result, symptom, path)


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(debug)

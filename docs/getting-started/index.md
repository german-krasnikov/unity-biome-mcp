# Getting Started with Unity MCP

Welcome! This guide will help you set up and start using Unity MCP to control your Unity Editor from Claude, Codex, Kimi, or any MCP-compatible AI assistant.

## Prerequisites

- **Unity 6.0.0** or later
- **Python 3.10** or later
- **One of these AI tools:**
  - Claude Code (recommended)
  - Codex CLI
  - Claude Desktop
  - Cursor
  - Windsurf
  - Kimi
  - OpenCode
- **TCP port 9500** available on your machine

## Installation

Unity MCP auto-configures when you add the plugin to your project — no manual setup required.

### Step 1: Add Plugin to Your Project

1. Open **Window → Package Manager**
2. Click **+ → Add package from git URL**
3. Paste: `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`
4. Wait for import, then open any scene

The plugin auto-detects your AI tool (Claude Code, Codex, Cursor, Windsurf, VS Code, or Claude Desktop) and generates the necessary MCP configuration.

### Step 2: Verify Installation (Optional)

To verify everything works, run:

```bash
uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp doctor
```

This checks Python version, TCP connectivity, and plugin status.

## First Steps

### 1. Launch Your Project

Open your Unity project and make sure it compiles cleanly.

### 2. Check the Setup Wizard

In the Unity Editor menu, go to:
```
MCP → Setup Wizard
```

This wizard will:
- Verify the plugin is installed correctly
- Check your Python environment
- Test the TCP connection
- Configure your AI tool (Claude Code, Codex, etc.)

### 3. Run Doctor

Before your first command, run the diagnostic:

```python
await doctor()
```

This checks:
- ✅ Python version
- ✅ Port file health
- ✅ TCP connection to Unity
- ✅ Plugin status
- ✅ Compile state

**Expected output:**
```
✅ All checks passed
```

### 4. Try Your First Command

Get the hierarchy of your current scene:

```python
await get_hierarchy()
```

You should see a text-based tree of all GameObjects in your scene.

## How It Works

Unity MCP uses three communication layers:

```
Your AI Assistant (Claude, Codex, etc.)
              ↓
        MCP Protocol (stdio)
              ↓
    Python MCP Server (port 9500)
              ↓
        TCP Binary Protocol
              ↓
    Unity Editor Plugin
```

### Two Modes

**CLI Mode** — use from Claude Code, Codex CLI, or any terminal:
```bash
export UNITY_MCP_PORT=9500
claude code < script.py
```

**In-Unity Chat** — open the MCP Chat window directly in the editor:
```
MCP → Chat
```

No API key needed — chat spawns the CLI under the hood.

## Token Efficiency

Unity MCP saves 80–95% of tokens by batching operations. Instead of calling individual tools 10 times, combine them into one `batch` call:

```python
# Before: 10 separate calls (~2000 tokens)
await create_object("Enemy")
await set_property("Enemy", "Transform", "position", "0,1,0")
await manage_component("Enemy", "Health", "add")
await set_property("Enemy", "Health", "maxHp", "100")
# ... 6 more calls

# After: 1 batch call (~150 tokens, 93% savings!)
await batch("""
create_object name=Enemy
set_property path=Enemy component=Transform prop=position value=0,1,0
manage_component path=Enemy type=Health action=add
set_property path=Enemy component=Health prop=maxHp value=100
""")
```

Learn more: [Batch Reference](../tools/batch.md)

## What's Next?

- 📚 **[Tools Reference](../tools/index.md)** — Complete list of 120 MCP tools by category
- ⚙️ **[Scene Tools](../tools/scene.md)** — Inspect, modify, and query your scene
- 🎮 **[Object Tools](../tools/objects.md)** — Create, edit, and manage GameObjects
- 🧪 **[PlayTest Tools](../tools/runtime.md)** — Write automated test scenarios
- 🐛 **[Diagnostics](../tools/diagnostics.md)** — Troubleshoot connection issues
- 💬 **[In-Unity Chat](../chat/backends.md)** — Run conversations inside the editor

## Troubleshooting

### "Port 9500 already in use"

Multiple Unity instances? Check which port your project is using:

```bash
ls -la ~/.unity-mcp/ports/
cat ~/.unity-mcp/ports/*.port
```

Then set the port explicitly:

```bash
export UNITY_MCP_PORT=9501
```

### "Plugin not found"

Reinstall the plugin via Package Manager → Add package from git URL:

```
https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin
```

### "Cannot connect to Unity"

1. Is Unity open and project loaded?
2. Run `doctor(fix=True)` to auto-clean stale port files
3. Check compile errors: `await get_compile_errors()`

### "Commands hang or timeout"

Usually means Unity is recompiling or domain-reloading. Wait for compile to finish:

```python
await await_compile(timeout=30)
```

## Learn More

- 📖 **[README](../../README.md)** — Full project documentation
- 🏗️ **[Architecture](../../AI/architecture.md)** — How Unity MCP works
- 🔧 **[Install Guides](../install/)** — Platform-specific instructions
- 💻 **[Tools Reference](../tools/index.md)** — Detailed tool parameters

---

**Questions?** Open an issue on [GitHub](https://github.com/german-krasnikov/unity-kiss-mcp/issues).

**Next:** Jump to [Tools Reference](../tools/index.md) to see what you can do.

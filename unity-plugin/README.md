# Unity Biome MCP Plugin

Unity Editor plugin that provides TCP server for MCP commands.

## Installation

### Option 1: Local Package (Development)

1. Open Unity Editor (2021.3 or later)
2. Go to `Window > Package Manager`
3. Click `+` → `Add package from disk...`
4. Select `package.json` from this directory

### Option 2: Manual Installation

1. Copy the `Editor/` folder to your Unity project's `Assets/` directory
2. Unity will auto-compile the scripts

## Verification

After installation, check the Unity Console. You should see:

```
[MCP] Server started on port 9500
```

If you see this message, the plugin is running correctly.

## Testing Connection

From the `server/` directory, run:

```bash
python tests/manual_integration_test.py
```

This will test:
- TCP connection to Unity
- `ping` command (should return "pong")
- `get_version` command (returns hierarchy version counter)

## Architecture

### Components

- **MCPServer.cs** - TCP listener on port 9500, handles async communication
- **CommandRouter.cs** - Routes commands to appropriate handlers
- **VersionTracker.cs** - Tracks hierarchy changes for cache invalidation

### Message Protocol

Binary framing: `[4-byte BE length][UTF-8 JSON payload]`

Request:
```json
{"id": "0001", "cmd": "ping", "args": {}}
```

Response:
```json
{"id": "0001", "ok": true, "data": "pong"}
```

Error:
```json
{"id": "0001", "ok": false, "err": "Unknown command"}
```

## Supported Commands (Phase 0)

- `ping` - Returns "pong"
- `get_version` - Returns hierarchy version counter

## Troubleshooting

**Console shows "Server error: Address already in use"**
- Another Unity instance is running
- Port 9500 is occupied by another process
- Restart Unity Editor

**No console messages**
- Check if scripts compiled without errors
- Verify package is installed correctly
- Check Unity Console for compilation errors

**Python client can't connect**
- Verify Unity Editor is running
- Check console for "[MCP] Server started" message
- Try restarting Unity Editor

#!/bin/bash
pkill -9 -f unity_mcp 2>/dev/null || true
rm -f ~/.unity-biome-mcp/server-*.lock
for f in ~/.unity-biome-mcp/ports/*.port; do
    pid=$(basename "$f" .port)
    kill -0 "$pid" 2>/dev/null || rm -f "$f"
done
echo "Done. Restart MCP with /mcp"

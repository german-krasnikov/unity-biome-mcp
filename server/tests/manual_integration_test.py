#!/usr/bin/env python3
"""
Manual integration test for Unity Biome MCP Server.

Usage:
    1. Open Unity Editor with the unity-plugin installed
    2. Run: python tests/manual_integration_test.py
    3. Verify ping and get_version commands work

This test requires Unity Editor to be running.
"""
import asyncio
import sys
from pathlib import Path

# Add src to path
sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from unity_mcp.bridge import UnityBridge


async def main():
    bridge = UnityBridge()

    print("Connecting to Unity Editor on port 9500...")
    try:
        await bridge.connect()
        print("✓ Connected")
    except Exception as e:
        print(f"✗ Failed to connect: {e}")
        print("\nMake sure Unity Editor is running with the MCP plugin installed.")
        return 1

    # Test ping
    print("\nTesting ping command...")
    try:
        result = await bridge.send("ping", {})
        if result.get("ok") and result.get("data") == "pong":
            print(f"✓ Ping successful: {result}")
        else:
            print(f"✗ Unexpected response: {result}")
    except Exception as e:
        print(f"✗ Ping failed: {e}")

    # Test get_version
    print("\nTesting get_version command...")
    try:
        result = await bridge.send("get_version", {})
        if result.get("ok"):
            version = result.get("data")
            print(f"✓ Version: {version}")
        else:
            print(f"✗ Error: {result.get('err')}")
    except Exception as e:
        print(f"✗ get_version failed: {e}")

    # Test unknown command (should return error)
    print("\nTesting unknown command (should fail)...")
    try:
        result = await bridge.send("unknown_cmd", {})
        if not result.get("ok"):
            print(f"✓ Error correctly returned: {result.get('err')}")
        else:
            print(f"✗ Should have returned error: {result}")
    except Exception as e:
        print(f"✗ Unexpected exception: {e}")

    await bridge.close()
    print("\n✓ All tests completed")
    return 0


if __name__ == "__main__":
    exit_code = asyncio.run(main())
    sys.exit(exit_code)

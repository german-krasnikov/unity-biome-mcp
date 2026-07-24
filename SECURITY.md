# Security Policy

## Reporting Vulnerabilities

If you discover a security vulnerability in Unity Biome MCP, please report it responsibly:

1. **GitHub Security Advisory** (preferred): Visit the [Security tab](https://github.com/german-krasnikov/unity-biome-mcp/security/advisories) and report privately.
2. **Email**: [german.krasnikov@gmail.com](mailto:german.krasnikov@gmail.com)

Please do not open public GitHub issues for security vulnerabilities.

---

## Security Model

Unity Biome MCP is a **developer tool** designed for local, interactive use. The security model assumes the OS user as the trust boundary.

### Network Isolation
- **Localhost-only**: TCP binds to `127.0.0.1:9500` — no remote connections possible.
- **No authentication**: The protocol is raw JSON over TCP. Any local process can send commands (same-user only).

### Code Execution Safety
- **SecurityScan**: The `execute_code` MCP tool runs a static analysis pass before executing C# code, blocking known dangerous patterns:
  - Application exit (`EditorApplication.Exit`, `Application.Quit`)
  - Process spawning (`System.Diagnostics`)
  - Network I/O (`System.Net`)
  - Assembly manipulation (`System.Reflection`)
  - File system access (`System.IO`) — **blocked outright** by SecurityScan (no bypass path)
- **Permission gating**: High-risk operations (`execute_code`, file write, asset import/export) require user confirmation via permission prompt dialog.

### Data Privacy
- **No cloud services**: The server runs entirely on your machine.
- **No external data transmission**: All communication is localhost TCP only.
- **No telemetry**: No usage tracking, error reporting, or analytics.

---

## Scope

**In scope for security reporting:**
- TCP protocol vulnerabilities
- Code execution bypass techniques
- Privilege escalation pathways
- Data leakage or unintended information disclosure
- Denial-of-service within the MCP protocol

**Out of scope:**
- Social engineering / phishing attacks
- General Unity Editor security issues (report to Unity)
- Third-party dependencies with known CVEs (file issue with detailed reproduction)

---

## Supported Versions

| Version | Status |
|---------|--------|
| Latest release | ✅ Supported |
| Previous release | ⚠️ Limited support |
| Older releases | ❌ Not supported |

---

## Known Limitations

1. **execute_code SecurityScan is pattern-based, not exhaustive**: Sophisticated reflection techniques or assembly loading from encoded bytes may bypass checks. The real control is that only the authenticated Claude session (stdio connection) can invoke this tool.

2. **Port discovery race on domain reload**: If multiple Unity projects are open and one crashes, port discovery may temporarily pick the wrong project. Mitigation: set `UNITY_MCP_PROJECT_DIR` environment variable explicitly.

3. **No cross-project asset isolation at the MCP protocol level**: The assumption is one MCP process per session. Asset operations are scoped to the connected Unity instance.

---

## Best Practices

When using Unity Biome MCP in development:

- **Run only one MCP session per project** (one MCP process per Unity instance).
- **Use `UNITY_MCP_PROJECT_DIR` env var** when managing multiple projects simultaneously.
- **Review `execute_code` outputs** before running complex scripts.
- **Keep Python and plugin versions in sync** — mismatches may cause unexpected behavior.

---

## Attribution

Security reports that result in a fix are credited in the changelog. Thank you for helping keep Unity Biome MCP secure.

"""Report formatting/rendering for MetricsRegistry snapshots."""


def _p95(seq) -> float:
    s = sorted(seq)
    idx = int(0.95 * len(s))
    return s[min(idx, len(s) - 1)]


def format_report(snapshot: dict) -> str:
    """Render a human-readable metrics report from a snapshot dict."""
    s = snapshot
    c = s["counters"]
    lines = [f"=== Unity Biome MCP Metrics (uptime {s['uptime_s']:.0f}s) ==="]

    # Caches
    cache_keys = [k for k in c if k.startswith("fpcache") or k.startswith("diffcache")]
    if cache_keys:
        lines.append("\n[Caches]")
        for prefix in ("fpcache", "diffcache"):
            hit = c.get(f"{prefix}.hit", 0)
            miss = c.get(f"{prefix}.miss", 0)
            if hit + miss == 0:
                continue
            rate = 100 * hit / (hit + miss)
            lines.append(f"  {prefix}: hit={hit} miss={miss} rate={rate:.0f}%")

    # Speculation
    if any(k.startswith("speculation") for k in c):
        hit = c.get("speculation.hit", 0)
        miss = c.get("speculation.miss", 0)
        total = hit + miss
        rate = (100 * hit / total) if total else 0
        lines.append(
            f"\n[Speculation] predict={c.get('speculation.predict', 0)} "
            f"hit={hit} miss={miss} hit_rate={rate:.0f}%"
        )

    # Sampling
    if any(k.startswith("sampling") for k in c):
        obs = s["observations"]
        lat = obs.get("sampling.latency_ms", {})
        lines.append(
            f"\n[Sampling/Haiku] calls={c.get('sampling.calls', 0)} "
            f"success={c.get('sampling.success', 0)} "
            f"timeout={c.get('sampling.timeout', 0)} fail={c.get('sampling.fail', 0)}"
        )
        if lat:
            lines.append(f"  latency p50={lat.get('p50', 0):.0f}ms p95={lat.get('p95', 0):.0f}ms")
        total_cost = s["costs_usd"].get("__total__", 0)
        if total_cost > 0:
            lines.append(f"  cost_usd ~= ${total_cost:.4f}")

    # Lessons
    if any(k.startswith("lessons") for k in c):
        lines.append(
            f"\n[Lessons] recorded={c.get('lessons.recorded', 0)} "
            f"hint_emitted={c.get('lessons.hint_emitted', 0)}"
        )

    # Hinter
    if any(k.startswith("hint.") or k == "hinter.error" for k in c):
        emitted = sum(v for k, v in c.items() if k.startswith("hint.emitted."))
        adopted = sum(v for k, v in c.items() if k.startswith("hint.adopted."))
        ignored = sum(v for k, v in c.items() if k.startswith("hint.ignored."))
        suppressed = sum(v for k, v in c.items() if k.startswith("hint.suppressed."))
        lines.append(
            f"\n[Hinter] emitted={emitted} adopted={adopted} ignored={ignored} "
            f"suppressed={suppressed} error={c.get('hinter.error', 0)}"
        )

    # Top commands by latency
    cmd_timers = {
        k.replace("cmd.", "").replace(".ms", ""): v
        for k, v in s["observations"].items()
        if k.startswith("cmd.")
    }
    if cmd_timers:
        lines.append("\n[Top commands by latency]")
        sorted_cmds = sorted(cmd_timers.items(), key=lambda kv: kv[1].get("p95", 0), reverse=True)[:5]
        for name, stats in sorted_cmds:
            lines.append(f"  {name}: n={stats['n']} p50={stats['p50']:.0f}ms p95={stats['p95']:.0f}ms")

    return "\n".join(lines)

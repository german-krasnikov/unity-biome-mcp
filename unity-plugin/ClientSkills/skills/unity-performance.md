---
name: unity-performance
description: Runtime performance — GC-free coding, draw calls, profiling/rendering/memory MCP tools, Luna budgets. Load when optimizing performance, debugging frame drops, or reviewing GC allocations.
user-invocable: false
---

# Performance Optimization

## Budget Targets (Playable Ads)

| Metric | Target | Hard Limit |
|--------|--------|------------|
| Build size | < 3 MB | 5 MB |
| Frame time | < 16ms (60fps) | 33ms (30fps) |
| Draw calls | < 50 | 100 |
| Triangles | < 50K | 100K |
| GC alloc/frame | 0 B | 1 KB |
| Textures total | < 10 MB RAM | 20 MB |

## GC-Free Patterns

| Source | Fix |
|--------|-----|
| `GetComponent<T>()` in Update | Cache in Awake |
| `FindObjectOfType` | Cache or `Get.Service<T>()` |
| LINQ (`.Where`, `.ToList`) | Manual loop + pre-allocated list |
| String concat (`+`, `$""`) | StringBuilder or pre-format |
| Boxing (`new Vector3()`) | Use struct directly |
| Closure captures in lambda | Extract to method or cache delegate |
| `foreach` on non-struct enumerator | `for (int i = 0; ...)` |

## Draw Call Reduction

Sprite Atlas (-50-80% UI), Static batching (-30-50% static), GPU Instancing (-70% repeated meshes), Shared materials (avoid copies).

## Profiling via MCP

**Gated.** `discover_tools category="RUNTIME"` before first use.

**get_frame_stats** — instant snapshot: fps, cpu, gpu, memory, draw calls. No args. **runtime_only (requires Play Mode). Reach for this FIRST.**

**profile** — CPU/GPU/memory profiling over time. **runtime_only (requires Play Mode).**
```
profile action="start" duration=5.0 mode="burst"         # auto-stop after 5s
profile action="start" mode="manual"                      # explicit stop
profile action="start" mode="triggered" threshold_ms=33.3 # on spike
profile action="stop|status"
profile action="analyze" focus="gc"                       # gc|rendering|physics|cpu
profile action="compare" session="s1" compare_with="s2"
profile action="list_sessions"
```

**get_memory** — memory breakdown: `get_memory include="textures"` (textures|meshes|audio|gc|all).

## Rendering Analysis

**Gated.** `discover_tools category="MEDIA"` before first use.

**render_analyze** — actions: `stats|materials|shaders|lights|batching|overdraw|audit|compare|frame_debug|shadow_audit|probe_audit|light_optimize`. Args: `path` (subtree), `detail="brief|full"`, `baseline_id`, `max_events`.
```
render_analyze action="stats"                  # draw calls, batches, tris, verts
render_analyze action="audit" detail="full"    # full rendering health check
render_analyze action="batching"               # SRP Batcher / static / dynamic / instancing
render_analyze action="frame_debug"            # per-draw-call FrameDebugger data
```

**analyze_lod_culling** — LOD groups + occlusion culling: `analyze_lod_culling focus="lod"` (lod|culling|occlusion).

## Profiling Workflow

```
get_frame_stats                                  # 1. quick health check
profile action="start" duration=5.0              # 2. profile gameplay segment
# ... do work ...
profile action="stop"
profile action="analyze" focus="gc"              # 3. analyze
render_analyze action="audit"                    # 4. rendering deep-dive
get_memory include="textures"                    # 5. memory audit
profile action="compare" session="before" compare_with="after"  # 6. compare
```

## Luna-Specific

- **No real-time shadows** — bake or disable
- **No complex particles** — bake to flipbook
- **No Rigidbody physics** — lightweight custom solutions
- **Max 20 shaders**, no Animator for simple tweens (DOTween)

## Checklist Before Build

- [ ] 0 GC alloc in Update/FixedUpdate
- [ ] All GetComponent cached, no LINQ in hot paths
- [ ] Sprite Atlases, static batching, textures ≤ 1024x1024
- [ ] Object pooling (`PoolingSystem`), no real-time shadows
- [ ] Canvas split (static vs dynamic UI), max 20 shaders

## See Also

- `token-optimization.md` — batch-first patterns, field projection, compression

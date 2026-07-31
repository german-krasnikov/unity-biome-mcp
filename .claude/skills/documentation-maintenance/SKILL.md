---
name: documentation-maintenance
description: "Audit and update all Unity Biome MCP documentation surfaces after code, behavior, workflow, install, tool, UI, release, or public-positioning changes. Use for doc-keeper handoffs, documentation-only revisions, and pre-release staleness checks."
---

# Documentation Maintenance

## Non-Negotiables

1. Code and tests define current behavior. Manifests define package metadata.
   `CHANGELOG.md` records released history. Existing prose is never stronger
   evidence than those sources.
2. Do not infer undocumented behavior. If implementation, tests, and public
   behavior disagree, report the conflict instead of choosing a convenient
   answer.
3. Keep one canonical explanation per fact. Other surfaces summarize and link.
4. Update only impacted content. Preserve unrelated wording, structure, and
   generated markers.
5. Resolve the private publication policy with
   `${CLAUDE_SKILL_DIR}/scripts/resolve-nda-policy.sh`, then read the returned
   file before writing public content or committing. Resolution is fail-closed
   for commit/push work. Keep internal names, machine paths, credentials,
   private URLs, and policy details out of public files and Git metadata.
6. Preserve documentation ownership boundaries. `.claude/skills/` contains
   internal contributor-agent workflows; `unity-plugin/ClientSkills/` contains
   consumer guidance installed into user projects. They may cover related
   topics, but they are never interchangeable link targets.

## Ownership Map

| Surface | Audience and ownership |
|---|---|
| `README.md` | Public entry point: positioning, quick start, concise capability summary, generated stats/changelog excerpt |
| `docs/index.md` | User-documentation homepage (MkDocs Material entry point) |
| `docs/getting-started/`, `docs/install/`, `docs/settings.md` | Onboarding, client setup, ports, permissions, security, troubleshooting |
| `docs/tools/`, `docs/features/`, `docs/chat/`, `docs/plugins/` | User workflows, tool behavior, feature guides, chat, extension API |
| `docs/comparison.md` | Detailed, dated, source-linked competitor comparison; README keeps only a short neutral summary |
| `AI/**/*.md` | Internal agent knowledge: implementation contracts, architecture, edge cases, operational patterns |
| `.claude/skills/**`, `.claude/agents/**` | Internal contributor workflows and roles; not shipped consumer guidance |
| `unity-plugin/ClientSkills/**` | Skills and agents installed into user projects; concise consumer-facing tool guidance |
| `unity-plugin/README.md` | UPM package overview and package-specific setup |
| `CHANGELOG.md` | Canonical release history |
| `unity-plugin/CHANGELOG.md` | Generated full mirror of the canonical changelog shipped with the UPM package |
| `docs/assets/_meta.json`, `.github/badges/`, marker-managed SVG/README blocks | Generated through `scripts/update_readme.py`; never hand-edit generated values |
| `server/pyproject.toml` | Canonical release version; version changes belong to the release workflow |
| `server/uv.lock`, `unity-plugin/package.json`, Python/C# version constants, `_meta.json` version fields | Generated version copies managed by `scripts/sync_versions.py` |

## Writing Docs

- Write standard GitHub-Flavored Markdown (GFM). No renderer-specific syntax.
- First H1 heading = page title. No `title:` front matter needed.
- Images in `docs/assets/`. Reference with `![alt](assets/x.svg)` or `<img src="assets/x.svg">`.
- Collapsible sections: `<details><summary>Title</summary>` (standard HTML).
- Callouts: `> [!NOTE]`, `> [!WARNING]`, `> [!TIP]` (GitHub Alert Syntax).
- Diagrams: ` ```mermaid ` fenced code blocks.
- Links between docs: relative `[text](other-file.md)` or `[text](../section/file.md#anchor)`.
- A build-time hook (`docs/hooks/transform.py`) converts GFM to the hosted site format automatically. Do NOT add renderer-specific attributes or path prefixes manually.

## Workflow

### 1. Establish the Change Set

Inspect all three scopes:

```bash
git status --short
git diff --name-status
git diff --name-status <base>...HEAD
```

Use the caller's base ref. If none is supplied, derive the merge-base from the
current branch and its upstream/default branch. Do not use a fixed commit count.

Read the changed implementation, tests, manifests, and existing canonical docs.
For a documentation-only audit, sample the relevant source and tests rather than
trusting the prose being reviewed.

Before changing a skill reference in `AI/`, classify its owner:

- Keep `.claude/skills/...` for internal contributor-agent instructions.
- Use `unity-plugin/ClientSkills/...` only when the text explicitly describes
  the guidance installed for end users.
- A topical match is not a migration. Do not replace one category with the
  other during link cleanup.
- If a concrete path is stale, update it within the same ownership category
  unless the task explicitly changes that ownership and its regression test.

### 2. Build a Coverage Ledger

Before editing, record one row per changed subsystem:

| Change | User docs | AI docs | Client skills | Generated/package/release surfaces | Action |
|---|---|---|---|---|---|
| Example | paths to inspect | paths to inspect | paths to inspect | paths to inspect | pending |

At completion, every cell must resolve to `updated`, `checked - unchanged`, or
`not applicable` with a reason.

### 3. Apply the Impact Matrix

| Changed area | Surfaces to inspect |
|---|---|
| Python tools, `tool_specs.py`, C# commands/routers | Relevant `docs/tools/` or `docs/features/`; matching `AI/` domain file and `AI/tools-reference.md`; `ClientSkills` reference/domain skill; generated count surfaces |
| Tool actions, parameters, defaults, tiers, safety, Edit/Play Mode rules | Every example and table that names the tool; diagnostics/settings if discoverability or permissions changed |
| TCP, middleware, gating, permissions, ports, connection recovery | `docs/settings.md`, getting-started/install/diagnostics docs; `AI/mcp-server.md`, `AI/tcp-bridge.md`, `AI/connection-tools.md`; relevant client skills |
| Setup Wizard, client installers, routing, supported clients | `docs/getting-started/`, `docs/install/`, `docs/settings.md`, `unity-plugin/README.md`, README quick start, `ClientSkills` install routing |
| Chat UI, providers, chips, annotations | `docs/chat/`, relevant feature docs, `AI/agent-chat.md`, `AI/chat-view.md`, README summary when material |
| Playtest, runtime, composer, DSL | `docs/features/playtest*`, `docs/tools/runtime.md`; matching `AI/` files; playtest client skill and tester agent |
| Plugin API or plugin packaging | `docs/plugins/`, `AI/architecture.md`, plugin API skill, package README/metadata |
| UI Toolkit, animation, particles, shaders, visual identity | Relevant user guide; matching `AI/ui.md`, `AI/animation.md`, `AI/particles.md`, or `AI/shaders.md`; visual assets only when public presentation changed |
| README facts, tests, tool inventory, versions, changelog | Read `update-readme`; regenerate/check metadata, badges, SVGs, README markers; release surfaces only when assigned |
| Public positioning or competitor claims | README summary, `docs/comparison.md`, package description if material; verify current claims from primary public sources and record access date |
| Added, moved, renamed, or removed docs | `docs/index.md`, `mkdocs.yml` `nav:` section (add/remove/rename the entry), nearby index pages, inbound links, anchors, and README links |

The matrix is a minimum, not a substitute for reading the diff. Follow
cross-references discovered in changed files.

### 3.1 Run The ClientSkills Release Gate

For every pre-release documentation audit, read and execute
`.claude/skills/client-skills-maintenance/SKILL.md`, even when
`unity-plugin/ClientSkills/` is unchanged.

- Inspect tool schemas, execution surfaces, parser behavior, installer,
  converter, and agent-boundary changes across the release branch.
- Update only impacted skills and agents.
- If no content change is required, record
  `ClientSkills: checked - unchanged` and list the source contracts reviewed.
- Do not rephrase stable guidance merely to create release churn.
- A release audit is incomplete until the ClientSkills ledger and focused
  validation are resolved.

### 4. Write for the Correct Audience

User documentation:

- Use plain English and task-first headings.
- State prerequisites before steps and the expected result after them.
- Include one minimal, realistic example for non-obvious workflows.
- Put recovery next to the failure it resolves.
- Explain constraints and side effects explicitly.
- Link to the canonical detailed guide instead of repeating it.

Agent knowledge and client skills:

- Prefer contracts, decision rules, exact tool names, mode restrictions, and
  failure recovery over narrative.
- Keep examples minimal and executable.
- Do not copy broad user explanations into `AI/`.

Public presentation:

- Preserve the Unity-native dark theme and restrained Biome/Arcade identity.
- Use bright green accents and motion as enhancement, not as proof or content.
- Keep animated SVGs deterministic, lightweight, readable when static, and
  respectful of `prefers-reduced-motion`.
- Treat curated presentation assets as an approved design surface. Prose
  cleanup does not authorize removing dividers, replacing headers, changing
  aspect ratios materially, or reducing animation intensity. Request explicit
  design approval for those changes and compare against the last approved
  render.
- Avoid unsupported superlatives, vague performance claims, and adversarial
  competitor wording. State scope and limitations so readers can decide.

### 5. Enforce DRY

For each repeated fact, choose the canonical owner:

- Tool metadata: `server/src/unity_mcp/tools/tool_specs.py`
- Runtime/tool behavior: implementation plus tests
- User procedure: the most specific `docs/` guide
- Internal implementation contract: the most specific `AI/` file
- Release history: root `CHANGELOG.md`
- Volatile README numbers: `docs/assets/_meta.json` generated from source

Replace secondary copies with a short summary and link. Duplication is allowed
only when the reader cannot complete a safety-critical step without the local
fact; keep such copies short and verify all of them.

## Conditional References

Read only what the change requires:

- Counts, versions, README markers, badges, or SVG stats:
  `.claude/skills/update-readme/SKILL.md`
- New or changed MCP tool:
  `.claude/skills/new-tool-checklist/SKILL.md`
- Reload, compile, or connection recovery:
  `.claude/skills/reload-recovery/SKILL.md`
- Release/changelog/version work:
  `.claude/skills/create-release/SKILL.md` and `.claude/skills/finish-task/SKILL.md`
- Client tool inventory:
  `.claude/skills/client-skills-maintenance/SKILL.md`
- Domain behavior:
  the matching skill under `.claude/skills/`

## Validation

Always:

```bash
git diff --check
```

- Resolve every changed local link and heading anchor.
- Compare changed examples with current signatures, defaults, and mode rules.
- Check changed files for internal names, absolute machine paths, credentials,
  private URLs, and stale product names.
- Review the final diff for accidental rewrites and duplicated explanations.
- For presentation changes, compare the previous and proposed layouts at
  desktop and narrow widths. Record asset dimensions and inspect two frames of
  every animation; source-level keyframe checks are not visual acceptance.

Conditional:

| Trigger | Required validation |
|---|---|
| README metadata or generated surfaces changed | `server/.venv/bin/python scripts/update_readme.py --check-facts` and `server/.venv/bin/python scripts/update_readme.py --check` |
| README generator code/tests changed in the implementation | `server/.venv/bin/python -m pytest scripts/tests -q` |
| SVG changed | `xmllint --noout docs/assets/*.svg` |
| README/SVG changes were pushed or released | Open the default-branch GitHub README and each raw embedded asset; verify section order, widths, text wrapping, animation, reduced-motion fallback, branch refs, and cache freshness. If not yet published, report this live check as pending |
| ClientSkills changed | Run the relevant installer/conversion structural tests; verify referenced tool names against `tool_specs.py` |
| `AI/` skill references changed | Run `server/.venv/bin/python -m pytest scripts/tests/test_client_skills.py -q`; review the diff for internal-to-consumer or consumer-to-internal substitutions |
| Changelog or versions changed | Use the release skill's parity checks; verify root/plugin changelog ownership and manifest versions |
| External comparison changed | Cite direct primary sources, state the comparison date, and distinguish verified facts from inference |
| `docs/**` or `mkdocs.yml` changed | `mkdocs build --strict` (dry-run, no deploy) |
| `docs/hooks/transform.py` changed | `mkdocs build --strict` — verify hook transforms work correctly |

Do not run broad Python or Unity suites merely because prose changed. Use the
verification evidence from the implementation handoff; run targeted checks only
when documentation tooling or generated facts depend on them.

## Definition of Done

- The coverage ledger has no unresolved cells.
- The mandatory ClientSkills release gate is resolved as updated or
  checked-unchanged with evidence.
- Every behavioral claim is grounded in code/tests or a cited primary source.
- User docs, agent knowledge, client skills, package docs, and generated
  surfaces agree where their scopes overlap.
- Internal `.claude/skills/` references and packaged `ClientSkills` references
  retain their distinct audiences and ownership.
- Canonical ownership is clear and unnecessary repetition is removed.
- Changed navigation, links, examples, generated markers, and SVGs validate.
- Public content and Git metadata pass the privacy/NDA policy.
- The final report lists updated and checked-but-unchanged surfaces.
- Nothing is committed unless the caller or invoking workflow requested it.

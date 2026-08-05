# Tool Quality Dashboard

Current tool quality metrics from automated linter analysis.
Data is updated on every CI run; missing data means CI has not run yet.

<script id="quality-data" type="application/json">
{
  "latest": {
    "date": "not yet available",
    "version": "-",
    "commit": "-",
    "toolsmith_errors": 0,
    "toolsmith_warnings": 0,
    "toolsmith_avg_score": 0,
    "mcplint_errors": 0,
    "mcplint_warnings": 0
  },
  "history": []
}
</script>

<div id="quality-banner"></div>

---

## Overview

<div class="quality-grid">
  <div class="quality-card">
    <div class="quality-card-value" id="card-score">--</div>
    <div class="quality-card-label">Avg Score</div>
  </div>
  <div class="quality-card">
    <div class="quality-card-value quality-card-value--error" id="card-errors">--</div>
    <div class="quality-card-label">Total Errors</div>
  </div>
  <div class="quality-card">
    <div class="quality-card-value quality-card-value--warn" id="card-warnings">--</div>
    <div class="quality-card-label">Total Warnings</div>
  </div>
</div>

---

## Score History

<div id="sparkline-wrap">
  <p><em>No history yet — appears after the first CI run.</em></p>
</div>

---

## Linter Breakdown

<div id="linter-breakdown">
  <div class="admonition note">
    <p class="admonition-title">No data yet</p>
    <p>Metrics will appear here after the first CI pipeline run.</p>
  </div>
</div>

---

## How It Works

!!! info "Automated quality gates"
    - **mcp-tool-card-linter** — validates every MCP tool card for schema, docstring quality, bool defaults, and spec registration. Produces a score per tool (0–100).
    - **mcp-lint** — checks cross-client protocol compatibility (argument names, types, required vs optional fields).

!!! tip "CI integration"
    Both linters run on every pull request. Errors block merge; warnings are tracked for trend analysis.
    A release gate that blocks on critical issues is planned for a future phase.

<style>
.quality-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 1rem;
  margin: 1.25rem 0;
}
.quality-card {
  background: var(--md-code-bg-color);
  border: 1px solid var(--md-default-fg-color--lightest);
  border-radius: .5rem;
  padding: 1.25rem 1rem;
  text-align: center;
}
.quality-card-value {
  font-size: 2.25rem;
  font-weight: 700;
  line-height: 1;
  color: var(--md-primary-fg-color);
  font-variant-numeric: tabular-nums;
}
.quality-card-value--error { color: #ef5350; }
.quality-card-value--warn  { color: #ff9800; }
.quality-card-label {
  font-size: .75rem;
  color: var(--md-default-fg-color--light);
  text-transform: uppercase;
  letter-spacing: .05em;
  margin-top: .5rem;
}
#sparkline-wrap svg { display: block; width: 100%; max-width: 640px; }
#sparkline-wrap p   { font-size: .75rem; color: var(--md-default-fg-color--light); margin-top: .4rem; }
#quality-banner .admonition { margin-bottom: 0; }
#linter-breakdown table { width: 100%; }
</style>

<script>
(function () {
  var raw = document.getElementById('quality-data');
  if (!raw) return;

  var data;
  try { data = JSON.parse(raw.textContent); } catch (e) { return; }

  var l = data.latest || {};
  var history = data.history || [];
  var noData = l.date === 'not yet available' || !l.date;

  /* ── Health banner ── */
  var banner = document.getElementById('quality-banner');
  if (banner) {
    var type = 'note', title = 'No data yet';
    var msg  = 'CI has not run yet. Metrics will appear after the first pipeline run.';
    if (!noData) {
      var score  = l.toolsmith_avg_score || 0;
      var errors = (l.toolsmith_errors  || 0) + (l.mcplint_errors  || 0);
      var warns  = (l.toolsmith_warnings|| 0) + (l.mcplint_warnings || 0);
      if      (score >= 90 && errors === 0) { type = 'success'; title = 'Healthy'; }
      else if (score >= 70 || errors < 5)   { type = 'warning'; title = 'Needs Attention'; }
      else                                  { type = 'danger';  title = 'Critical Issues'; }
      msg = 'Score: ' + score + '/100 · '
          + errors + ' error' + (errors !== 1 ? 's' : '') + ' · '
          + warns  + ' warning' + (warns  !== 1 ? 's' : '') + ' · '
          + 'commit ' + l.commit + ' · ' + l.date;
    }
    var adm = document.createElement('div');
    adm.className = 'admonition ' + type;
    var t = document.createElement('p');
    t.className = 'admonition-title';
    t.textContent = title;
    var p = document.createElement('p');
    p.textContent = msg;
    adm.appendChild(t);
    adm.appendChild(p);
    banner.textContent = '';
    banner.appendChild(adm);
  }

  /* ── Metric cards ── */
  function setText(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
  }
  if (!noData) {
    var avgScore   = l.toolsmith_avg_score || 0;
    var totalErr   = (l.toolsmith_errors   || 0) + (l.mcplint_errors   || 0);
    var totalWarn  = (l.toolsmith_warnings  || 0) + (l.mcplint_warnings  || 0);
    setText('card-score',    avgScore + '/100');
    setText('card-errors',   totalErr);
    setText('card-warnings', totalWarn);
  }

  /* ── Sparkline ── */
  var sparkWrap = document.getElementById('sparkline-wrap');
  if (sparkWrap && history.length > 0) {
    var pts  = history.slice(-30);
    var W = 640, H = 80, pad = 10;
    var xStep = (W - pad * 2) / Math.max(pts.length - 1, 1);

    var coords = pts.map(function (p, i) {
      var x = pad + i * xStep;
      var y = pad + (1 - (p.toolsmith_avg_score / 100)) * (H - pad * 2);
      return [x, y];
    });

    var lastPt = pts[pts.length - 1];
    var lastScore = lastPt ? (lastPt.toolsmith_avg_score || 0) : 0;
    var lineColor = lastScore >= 90 ? '#4caf50' : lastScore >= 70 ? '#ff9800' : '#ef5350';

    /* area fill path */
    var areaPath = coords.map(function (c, i) { return (i === 0 ? 'M' : 'L') + c[0] + ',' + c[1]; }).join(' ')
                 + ' L' + coords[coords.length-1][0] + ',' + (H - pad)
                 + ' L' + coords[0][0] + ',' + (H - pad) + ' Z';

    var polyline = coords.map(function (c) { return c[0] + ',' + c[1]; }).join(' ');

    var dots = coords.map(function (c) {
      return '<circle cx="' + c[0] + '" cy="' + c[1] + '" r="3" fill="' + lineColor + '"/>';
    }).join('');

    sparkWrap.innerHTML =
      '<svg viewBox="0 0 ' + W + ' ' + H + '" aria-label="Score history sparkline">'
      + '<path d="' + areaPath + '" fill="' + lineColor + '" fill-opacity="0.12"/>'
      + '<polyline points="' + polyline + '" fill="none" stroke="' + lineColor + '" stroke-width="2" stroke-linejoin="round"/>'
      + dots
      + '</svg>'
      + '<p>' + pts.length + ' run' + (pts.length !== 1 ? 's' : '') + ' shown (last 30)</p>';
  }

  /* ── Linter breakdown ── */
  var breakdown = document.getElementById('linter-breakdown');
  if (breakdown) {
    if (noData) {
      /* keep static placeholder */
    } else {
      breakdown.innerHTML =
        '<table>'
        + '<thead><tr><th>Linter</th><th>Errors</th><th>Warnings</th><th>Avg Score</th></tr></thead>'
        + '<tbody>'
        + '<tr><td>mcp-tool-card-linter</td>'
        +     '<td>' + (l.toolsmith_errors   || 0) + '</td>'
        +     '<td>' + (l.toolsmith_warnings  || 0) + '</td>'
        +     '<td>' + (l.toolsmith_avg_score || 0) + '/100</td></tr>'
        + '<tr><td>mcp-lint</td>'
        +     '<td>' + (l.mcplint_errors   || 0) + '</td>'
        +     '<td>' + (l.mcplint_warnings  || 0) + '</td>'
        +     '<td>—</td></tr>'
        + '</tbody></table>';
    }
  }
})();
</script>

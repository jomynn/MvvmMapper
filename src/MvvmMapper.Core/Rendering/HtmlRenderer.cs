using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using MvvmMapper.Core.Analysis;
using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Rendering;

public sealed class HtmlRenderer(ILogger<HtmlRenderer> logger) : IRenderer
{
    public AnalysisReport? AnalysisReport { get; set; }
    private const string Template = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>MVVM Map Report</title>
        <style>
        *{box-sizing:border-box;margin:0;padding:0}
        body{font-family:system-ui,sans-serif;background:#f5f5f5;color:#333}
        .header{background:#1e3a5f;color:#fff;padding:1.5rem 2rem}
        .header h1{font-size:1.5rem;margin-bottom:.25rem}
        .header .meta{font-size:.85rem;opacity:.8}
        .summary{display:flex;gap:1rem;padding:1rem 2rem;background:#fff;border-bottom:1px solid #ddd}
        .stat{background:#f0f4ff;border-radius:8px;padding:.75rem 1.25rem;text-align:center}
        .stat .n{font-size:1.5rem;font-weight:700;color:#1e3a5f}
        .stat .l{font-size:.75rem;color:#666}
        .controls{padding:.75rem 2rem;background:#fff;border-bottom:1px solid #ddd;display:flex;gap:1rem;align-items:center}
        .controls input{padding:.4rem .75rem;border:1px solid #ccc;border-radius:4px;font-size:.9rem;width:280px}
        .controls select{padding:.4rem .75rem;border:1px solid #ccc;border-radius:4px;font-size:.9rem}
        .tabs{display:flex;gap:0;padding:0 2rem;background:#fff;border-bottom:1px solid #ddd}
        .tab{padding:.6rem 1.25rem;cursor:pointer;border-bottom:3px solid transparent;font-size:.9rem;color:#666}
        .tab.active{border-bottom-color:#1e3a5f;color:#1e3a5f;font-weight:600}
        .panel{display:none;padding:1.5rem 2rem}
        .panel.active{display:block}
        table{width:100%;border-collapse:collapse;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,.1)}
        th{background:#1e3a5f;color:#fff;text-align:left;padding:.6rem .75rem;font-size:.85rem}
        td{padding:.55rem .75rem;font-size:.85rem;border-bottom:1px solid #eee}
        tr:last-child td{border-bottom:none}
        tr:hover td{background:#f9f9ff}
        .badge{display:inline-block;padding:.15rem .5rem;border-radius:3px;font-size:.75rem;font-weight:600}
        .High{background:#d4edda;color:#155724}
        .Medium{background:#fff3cd;color:#856404}
        .Low{background:#f8d7da;color:#721c24}
        .shared{background:#cce5ff;color:#004085}
        code{font-family:monospace;font-size:.8rem;background:#f0f0f0;padding:.1rem .3rem;border-radius:3px}
        .empty{color:#999;font-style:italic;padding:2rem;text-align:center}
        </style>
        </head>
        <body>
        <div class="header">
          <h1>MVVM Map Report</h1>
          <div class="meta">Generated: {{GENERATED_AT}} &nbsp;|&nbsp; Solution: {{SOLUTION_PATH}}</div>
        </div>
        <div class="summary">
          <div class="stat"><div class="n">{{VIEW_COUNT}}</div><div class="l">Views</div></div>
          <div class="stat"><div class="n">{{VM_COUNT}}</div><div class="l">ViewModels</div></div>
          <div class="stat"><div class="n">{{ENDPOINT_COUNT}}</div><div class="l">Endpoints</div></div>
          <div class="stat"><div class="n">{{EDGE_COUNT}}</div><div class="l">Edges</div></div>
          <div class="stat"><div class="n">{{SHARED_VM_COUNT}}</div><div class="l">Shared VMs</div></div>
        </div>
        {{ANALYSIS_SECTION}}
        <div class="controls">
          <input type="text" id="search" placeholder="Search..." oninput="filterRows()">
          <select id="confFilter" onchange="filterRows()">
            <option value="all">All confidence</option>
            <option value="High">High only</option>
            <option value="HighMedium">High + Medium</option>
          </select>
        </div>
        <div class="tabs">
          <div class="tab active" onclick="showTab('view')">By View</div>
          <div class="tab" onclick="showTab('vm')">By ViewModel</div>
          <div class="tab" onclick="showTab('endpoint')">By Endpoint</div>
        </div>
        <div id="panel-view" class="panel active">
          <table id="tbl-view">
            <thead><tr><th>View</th><th>ViewModel</th><th>Confidence</th><th>Reason</th><th>Endpoints Reachable</th></tr></thead>
            <tbody>{{VIEW_ROWS}}</tbody>
          </table>
        </div>
        <div id="panel-vm" class="panel">
          <table id="tbl-vm">
            <thead><tr><th>ViewModel</th><th>Bound Views</th><th>Fan-in</th><th>Shared?</th></tr></thead>
            <tbody>{{VM_ROWS}}</tbody>
          </table>
        </div>
        <div id="panel-endpoint" class="panel">
          <table id="tbl-endpoint">
            <thead><tr><th>Verb</th><th>Route</th><th>Via Method</th><th>Reached From Views</th></tr></thead>
            <tbody>{{ENDPOINT_ROWS}}</tbody>
          </table>
        </div>
        <script>
        function showTab(name){
          document.querySelectorAll('.tab').forEach((t,i)=>{t.classList.toggle('active',['view','vm','endpoint'][i]===name)});
          document.querySelectorAll('.panel').forEach(p=>{p.classList.toggle('active',p.id==='panel-'+name)});
        }
        function filterRows(){
          const q=document.getElementById('search').value.toLowerCase();
          const cf=document.getElementById('confFilter').value;
          document.querySelectorAll('tbody tr').forEach(row=>{
            const text=row.textContent.toLowerCase();
            const conf=row.dataset.conf||'';
            const matchQ=!q||text.includes(q);
            const matchC=cf==='all'||(cf==='High'&&conf==='High')||(cf==='HighMedium'&&(conf==='High'||conf==='Medium'));
            row.style.display=(matchQ&&matchC)?'':'none';
          });
        }
        </script>
        <!-- Open .md files in a Mermaid viewer for diagrams. -->
        </body>
        </html>
        """;

    public string Format => "html";

    public async Task RenderAsync(MvvmGraph graph, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var viewCount = graph.Nodes.Values.Count(n => n.Kind == NodeKind.View);
        var vmCount = graph.Nodes.Values.Count(n => n.Kind == NodeKind.ViewModel);
        var endpointCount = graph.Nodes.Values.Count(n => n.Kind == NodeKind.Endpoint);
        var edgeCount = graph.Edges.Count;

        var sharedVms = graph.Nodes.Values
            .Where(n => n.Kind == NodeKind.ViewModel)
            .Where(vm => graph.EdgesTo(vm.Id).Count(e => e.Kind == EdgeKind.BindsTo) >= 2)
            .ToList();
        var sharedVmCount = sharedVms.Count;

        var html = Template
            .Replace("{{GENERATED_AT}}", WebUtility.HtmlEncode(DateTime.UtcNow.ToString("o")))
            .Replace("{{SOLUTION_PATH}}", WebUtility.HtmlEncode(outputDirectory))
            .Replace("{{VIEW_COUNT}}", viewCount.ToString())
            .Replace("{{VM_COUNT}}", vmCount.ToString())
            .Replace("{{ENDPOINT_COUNT}}", endpointCount.ToString())
            .Replace("{{EDGE_COUNT}}", edgeCount.ToString())
            .Replace("{{SHARED_VM_COUNT}}", sharedVmCount.ToString())
            .Replace("{{VIEW_ROWS}}", BuildViewRows(graph))
            .Replace("{{VM_ROWS}}", BuildVmRows(graph, sharedVms.Select(n => n.Id).ToHashSet()))
            .Replace("{{ENDPOINT_ROWS}}", BuildEndpointRows(graph))
            .Replace("{{ANALYSIS_SECTION}}", BuildAnalysisSection(AnalysisReport));

        var outputFile = Path.Combine(outputDirectory, "report.html");
        await File.WriteAllTextAsync(outputFile, html, Encoding.UTF8, cancellationToken);
        logger.LogInformation("Wrote {File}", outputFile);
    }

    private static string BuildViewRows(MvvmGraph graph)
    {
        var sb = new StringBuilder();
        var bindEdges = graph.Edges
            .Where(e => e.Kind == EdgeKind.BindsTo)
            .OrderBy(e => e.FromId);

        foreach (var edge in bindEdges)
        {
            if (!graph.Nodes.TryGetValue(edge.FromId, out var fromNode)) continue;
            if (!graph.Nodes.TryGetValue(edge.ToId, out var toNode)) continue;

            var endpoints = graph.ReachableEndpoints(edge.FromId)
                .Select(ep => WebUtility.HtmlEncode($"{ep.Verb} {ep.Route}"))
                .ToList();
            var endpointCell = endpoints.Count > 0
                ? string.Join(", ", endpoints)
                : "<em>none</em>";

            sb.AppendLine($"""
                <tr data-conf="{WebUtility.HtmlEncode(edge.Confidence.ToString())}">
                  <td><code>{WebUtility.HtmlEncode(fromNode.DisplayName)}</code></td>
                  <td><code>{WebUtility.HtmlEncode(toNode.DisplayName)}</code></td>
                  <td><span class="badge {WebUtility.HtmlEncode(edge.Confidence.ToString())}">{WebUtility.HtmlEncode(edge.Confidence.ToString())}</span></td>
                  <td>{WebUtility.HtmlEncode(edge.Reason)}</td>
                  <td>{endpointCell}</td>
                </tr>
                """);
        }

        if (sb.Length == 0)
        {
            sb.AppendLine("<tr><td colspan=\"5\" class=\"empty\">No View→ViewModel bindings found.</td></tr>");
        }

        return sb.ToString();
    }

    private static string BuildVmRows(MvvmGraph graph, HashSet<string> sharedVmIds)
    {
        var sb = new StringBuilder();
        var vms = graph.Nodes.Values
            .OfType<ViewModelNode>()
            .OrderBy(v => v.DisplayName);

        foreach (var vm in vms)
        {
            var boundViews = graph.EdgesTo(vm.Id)
                .Where(e => e.Kind == EdgeKind.BindsTo)
                .Select(e => graph.Nodes.TryGetValue(e.FromId, out var n) ? n.DisplayName : e.FromId)
                .OrderBy(name => name)
                .ToList();

            var fanIn = boundViews.Count;
            var isShared = sharedVmIds.Contains(vm.Id);
            var sharedCell = isShared
                ? "<span class=\"badge shared\">SHARED</span>"
                : "";

            var viewsCell = boundViews.Count > 0
                ? string.Join(", ", boundViews.Select(WebUtility.HtmlEncode))
                : "<em>none</em>";

            sb.AppendLine($"""
                <tr>
                  <td><code>{WebUtility.HtmlEncode(vm.DisplayName)}</code></td>
                  <td>{viewsCell}</td>
                  <td>{fanIn}</td>
                  <td>{sharedCell}</td>
                </tr>
                """);
        }

        if (sb.Length == 0)
        {
            sb.AppendLine("<tr><td colspan=\"4\" class=\"empty\">No ViewModels found.</td></tr>");
        }

        return sb.ToString();
    }

    private static string BuildAnalysisSection(AnalysisReport? report)
    {
        if (report is null) return string.Empty;

        bool hasOrphans = report.OrphanedViews.Count > 0
            || report.OrphanedViewModels.Count > 0
            || report.UnreachableEndpoints.Count > 0;
        bool hasShared = report.SharedViewModels.Count > 0;

        if (!hasOrphans && !hasShared) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("<div style=\"padding:1rem 2rem;background:#fffbe6;border-bottom:1px solid #ffe58f\">");
        sb.AppendLine("  <strong style=\"display:block;margin-bottom:.5rem;color:#614700\">Analysis</strong>");

        if (hasOrphans)
        {
            if (report.OrphanedViews.Count > 0)
            {
                foreach (var v in report.OrphanedViews)
                    sb.AppendLine($"  <span style=\"display:inline-block;margin:.2rem .3rem;padding:.2rem .6rem;background:#f8d7da;color:#721c24;border-radius:4px;font-size:.8rem\">Orphan View: {WebUtility.HtmlEncode(v.DisplayName)}</span>");
            }
            if (report.OrphanedViewModels.Count > 0)
            {
                foreach (var vm in report.OrphanedViewModels)
                    sb.AppendLine($"  <span style=\"display:inline-block;margin:.2rem .3rem;padding:.2rem .6rem;background:#f8d7da;color:#721c24;border-radius:4px;font-size:.8rem\">Orphan VM: {WebUtility.HtmlEncode(vm.DisplayName)}</span>");
            }
            if (report.UnreachableEndpoints.Count > 0)
            {
                foreach (var ep in report.UnreachableEndpoints)
                    sb.AppendLine($"  <span style=\"display:inline-block;margin:.2rem .3rem;padding:.2rem .6rem;background:#f8d7da;color:#721c24;border-radius:4px;font-size:.8rem\">Unreachable Endpoint: {WebUtility.HtmlEncode(ep.DisplayName)}</span>");
            }
        }

        if (hasShared)
        {
            foreach (var vm in report.SharedViewModels)
                sb.AppendLine($"  <span style=\"display:inline-block;margin:.2rem .3rem;padding:.2rem .6rem;background:#cce5ff;color:#004085;border-radius:4px;font-size:.8rem\">Shared VM: {WebUtility.HtmlEncode(vm.DisplayName)} (fan-in={vm.FanIn})</span>");
        }

        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static string BuildEndpointRows(MvvmGraph graph)
    {
        var sb = new StringBuilder();
        var endpoints = graph.Nodes.Values
            .OfType<EndpointNode>()
            .OrderBy(e => e.Verb)
            .ThenBy(e => e.Route);

        foreach (var endpoint in endpoints)
        {
            // Find methods that hit this endpoint
            var hittingMethods = graph.EdgesTo(endpoint.Id)
                .Where(e => e.Kind == EdgeKind.Hits)
                .Select(e => graph.Nodes.TryGetValue(e.FromId, out var n) ? n.DisplayName : e.FromId)
                .OrderBy(name => name)
                .ToList();

            var methodCell = hittingMethods.Count > 0
                ? string.Join(", ", hittingMethods.Select(m => $"<code>{WebUtility.HtmlEncode(m)}</code>"))
                : "<em>none</em>";

            // Find views that can reach this endpoint
            var reachingViews = graph.Nodes.Values
                .OfType<ViewNode>()
                .Where(v => graph.ReachableEndpoints(v.Id).Any(ep => ep.Id == endpoint.Id))
                .Select(v => v.DisplayName)
                .OrderBy(name => name)
                .ToList();

            var viewsCell = reachingViews.Count > 0
                ? string.Join(", ", reachingViews.Select(WebUtility.HtmlEncode))
                : "<em>none</em>";

            sb.AppendLine($"""
                <tr>
                  <td>{WebUtility.HtmlEncode(endpoint.Verb)}</td>
                  <td><code>{WebUtility.HtmlEncode(endpoint.Route)}</code></td>
                  <td>{methodCell}</td>
                  <td>{viewsCell}</td>
                </tr>
                """);
        }

        if (sb.Length == 0)
        {
            sb.AppendLine("<tr><td colspan=\"4\" class=\"empty\">No endpoints found.</td></tr>");
        }

        return sb.ToString();
    }
}

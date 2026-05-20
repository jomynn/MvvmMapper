using MvvmMapper.Core.Graph;

namespace MvvmMapper.Core.Analysis;

public sealed class AnalysisRunner(
    OrphanDetector orphanDetector,
    FanOutAnalyzer fanOutAnalyzer,
    EndpointImpactAnalyzer endpointImpactAnalyzer)
{
    public AnalysisReport Run(MvvmGraph graph)
    {
        var (orphanViews, orphanVMs, orphanEndpoints) = orphanDetector.Detect(graph);
        var sharedVMs = fanOutAnalyzer.Analyze(graph);
        var impacts = endpointImpactAnalyzer.Analyze(graph);

        return new AnalysisReport(orphanViews, orphanVMs, orphanEndpoints, sharedVMs, impacts);
    }
}

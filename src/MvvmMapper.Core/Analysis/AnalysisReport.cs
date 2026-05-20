namespace MvvmMapper.Core.Analysis;

public sealed record AnalysisReport(
    IReadOnlyList<OrphanedNode> OrphanedViews,
    IReadOnlyList<OrphanedNode> OrphanedViewModels,
    IReadOnlyList<OrphanedNode> UnreachableEndpoints,
    IReadOnlyList<SharedViewModelInfo> SharedViewModels,
    IReadOnlyList<EndpointImpact> EndpointImpacts);

public sealed record OrphanedNode(string NodeId, string DisplayName, string Kind);

public sealed record SharedViewModelInfo(
    string ViewModelId,
    string DisplayName,
    int FanIn,
    IReadOnlyList<string> BoundViewIds);

public sealed record EndpointImpact(
    string EndpointId,
    string Verb,
    string Route,
    IReadOnlyList<string> ReachableFromViewIds);

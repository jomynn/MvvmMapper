namespace MvvmMapper.Core.Graph;

public enum EdgeKind { BindsTo, Invokes, Contains, Calls, Hits, ComposedOf, Implements }

public enum Confidence { High, Medium, Low }

public record Edge(
    string FromId,
    string ToId,
    EdgeKind Kind,
    Confidence Confidence,
    string Reason);

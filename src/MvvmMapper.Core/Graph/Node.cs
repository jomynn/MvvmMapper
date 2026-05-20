using System.Text.Json.Serialization;

namespace MvvmMapper.Core.Graph;

public enum NodeKind { View, ViewModel, Method, Service, Endpoint }

[JsonDerivedType(typeof(ViewNode), "view")]
[JsonDerivedType(typeof(ViewModelNode), "viewModel")]
[JsonDerivedType(typeof(MethodNode), "method")]
[JsonDerivedType(typeof(ServiceNode), "service")]
[JsonDerivedType(typeof(EndpointNode), "endpoint")]
public abstract record Node(
    string Id,
    string DisplayName,
    NodeKind Kind,
    string SourceFile,
    int? SourceLine);

public record ViewNode(
    string Id,
    string DisplayName,
    string SourceFile,
    int? SourceLine,
    string XamlNamespace)
    : Node(Id, DisplayName, NodeKind.View, SourceFile, SourceLine);

public record ViewModelNode(
    string Id,
    string DisplayName,
    string SourceFile,
    int? SourceLine,
    string FullyQualifiedName)
    : Node(Id, DisplayName, NodeKind.ViewModel, SourceFile, SourceLine);

public record MethodNode(
    string Id,
    string DisplayName,
    string SourceFile,
    int? SourceLine,
    string OwningType)
    : Node(Id, DisplayName, NodeKind.Method, SourceFile, SourceLine);

public record ServiceNode(
    string Id,
    string DisplayName,
    string SourceFile,
    int? SourceLine,
    string InterfaceType)
    : Node(Id, DisplayName, NodeKind.Service, SourceFile, SourceLine);

public record EndpointNode(
    string Id,
    string DisplayName,
    string SourceFile,
    int? SourceLine,
    string Verb,
    string Route)
    : Node(Id, DisplayName, NodeKind.Endpoint, SourceFile, SourceLine);

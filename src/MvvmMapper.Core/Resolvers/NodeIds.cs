using MvvmMapper.Core.Parsing;

namespace MvvmMapper.Core.Resolvers;

internal static class NodeIds
{
    public static string ForView(XamlDocument doc) =>
        doc.XClass is not null
            ? $"view:{doc.XClass}"
            : $"view:{doc.FilePath}";

    public static string ForViewFile(string filePath) => $"view:{filePath}";

    public static string ForViewModel(string fullyQualifiedName) => $"vm:{fullyQualifiedName}";

    public static string ForMethod(string owningType, string methodName) =>
        $"method:{owningType}.{methodName}";

    public static string ForEndpoint(string verb, string route) =>
        $"endpoint:{verb.ToUpperInvariant()}:{route}";
}
